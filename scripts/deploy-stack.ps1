param(
    [ValidateSet('dev', 'prod')]
    [string]$Env = $env:AZURE_ENV_NAME,

    [string]$SqlAdminPassword = $env:SQL_ADMIN_PASSWORD
)

if (-not $Env) { Write-Error "Env is required (dev or prod)."; exit 1 }
if (-not $SqlAdminPassword) {
    Write-Error "SqlAdminPassword is required. Set SQL_ADMIN_PASSWORD env var or pass -SqlAdminPassword."
    exit 1
}

$rg        = "rg-shelflife-$Env"
$stackName = "ShelfLife-$Env"
$paramFile = "infra/parameters/$Env.bicepparam"
$template  = "infra/main.bicep"

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  ShelfLife - Azure Deployment Stack" -ForegroundColor Cyan
Write-Host "  Environment : $Env" -ForegroundColor Cyan
Write-Host "  Stack name  : $stackName" -ForegroundColor Cyan
Write-Host "  Resource RG : $rg" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Validating Bicep template..." -ForegroundColor Yellow
az deployment group validate `
    --resource-group $rg `
    --template-file  $template `
    --parameters     $paramFile `
    --parameters     "sqlAdminPassword=$SqlAdminPassword" `
    --output none

if ($LASTEXITCODE -ne 0) { Write-Error "Template validation failed."; exit 1 }
Write-Host "Template is valid." -ForegroundColor Green

Write-Host "Creating/updating Deployment Stack '$stackName'..." -ForegroundColor Yellow

$result = az stack group create `
    --name               $stackName `
    --resource-group     $rg `
    --template-file      $template `
    --parameters         $paramFile `
    --parameters         "sqlAdminPassword=$SqlAdminPassword" `
    --action-on-unmanage deleteAll `
    --deny-settings-mode none `
    --yes `
    --output json 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment Stack creation failed."
    Write-Host $result
    exit 1
}

$stack = $result | ConvertFrom-Json
Write-Host ""
Write-Host "Stack provisioning state : $($stack.provisioningState)" -ForegroundColor Green
Write-Host "Stack ID                 : $($stack.id)"

if ($stack.outputs) {
    Write-Host ""
    Write-Host "Outputs:" -ForegroundColor Cyan
    $stack.outputs.PSObject.Properties | ForEach-Object {
        Write-Host "  $($_.Name) = $($_.Value.value)"
    }
}

Write-Host ""
Write-Host "Drift check : az stack group show --name $stackName --resource-group $rg" -ForegroundColor DarkGray
Write-Host "Teardown    : ./scripts/teardown-stack.ps1 -Env $Env" -ForegroundColor DarkGray
