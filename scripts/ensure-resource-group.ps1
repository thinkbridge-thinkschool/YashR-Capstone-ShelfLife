# ensure-resource-group.ps1
# azd preprovision hook — idempotently creates the resource group for the current environment.
# azd exports AZURE_ENV_NAME and AZURE_LOCATION before running hooks.

$envName  = $env:AZURE_ENV_NAME
$location = $env:AZURE_LOCATION

if (-not $envName)  { Write-Error "AZURE_ENV_NAME is not set."; exit 1 }
if (-not $location) { $location = 'southeastasia' }

$rg = "rg-shelflife-$envName"

Write-Host "Ensuring resource group '$rg' in '$location'..." -ForegroundColor Cyan
az group create --name $rg --location $location --output none

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Resource group '$rg' is ready." -ForegroundColor Green
