# Grants the App Service Managed Identity access to Azure SQL.
# Run ONCE after the first Bicep deployment, as a user who is the Entra ID admin
# of the SQL server (or who has db_owner on the ShelfLife database).
#
# Prerequisites:
#   az login   (the signed-in identity must be the Entra ID admin of the SQL server)
#   The SQL server firewall must allow your client IP.
#
# Usage:
#   .\scripts\setup-sql-mi.ps1 -ResourceGroup rg-shelflife-dev -EnvironmentName dev

param(
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $EnvironmentName
)

$prefix    = "shelflife-$EnvironmentName"
$sqlServer = "$prefix-sql"
$appName   = "$prefix-api"
$database  = "ShelfLife"

$sqlFqdn = (az sql server show -g $ResourceGroup -n $sqlServer --query fullyQualifiedDomainName -o tsv)
if (-not $sqlFqdn) { throw "SQL server '$sqlServer' not found in '$ResourceGroup'." }

Write-Host "SQL server : $sqlFqdn"
Write-Host "MI identity: $appName"
Write-Host ""

# NOTE: sqlcmd -P has a 128-char limit and silently fails with long access tokens.
# Use System.Data.SqlClient with the AccessToken property instead — no length limit.
$token = (az account get-access-token --resource https://database.windows.net | ConvertFrom-Json).accessToken

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = "Server=tcp:$sqlFqdn,1433;Initial Catalog=$database;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$conn.AccessToken = $token
$conn.Open()

$sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$appName')
BEGIN
    CREATE USER [$appName] FROM EXTERNAL PROVIDER;
    ALTER ROLE db_datareader ADD MEMBER [$appName];
    ALTER ROLE db_datawriter ADD MEMBER [$appName];
    ALTER ROLE db_ddladmin   ADD MEMBER [$appName];
    PRINT 'Created MI user: $appName';
END
ELSE
    PRINT 'MI user already exists: $appName — no changes made.';
"@

$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$cmd.ExecuteNonQuery() | Out-Null
$conn.Close()

Write-Host "Done. App Service MI can now authenticate to SQL via 'Authentication=Active Directory Default'."
Write-Host "No password is stored anywhere in app settings."
