# teardown-stack.ps1
# Deletes the Deployment Stack AND all managed resources in one command.
# Because all resources were created through the stack, nothing is orphaned.
#
# Usage:
#   ./scripts/teardown-stack.ps1 -Env dev
#   ./scripts/teardown-stack.ps1 -Env prod

param(
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'prod')]
    [string]$Env
)

$rg        = "rg-shelflife-$Env"
$stackName = "ShelfLife-$Env"

Write-Host "Deleting Deployment Stack '$stackName' and ALL managed resources..." -ForegroundColor Red
Write-Host "Resource group: $rg" -ForegroundColor Red
Write-Host ""

az stack group delete `
    --name               $stackName `
    --resource-group     $rg `
    --action-on-unmanage deleteAll `
    --yes

if ($LASTEXITCODE -eq 0) {
    Write-Host "Stack and all managed resources deleted." -ForegroundColor Green
} else {
    Write-Error "Teardown failed — check the portal for partial state."
    exit 1
}
