# ── Cleanup Old Projects ──────────────────────────────────────────────────────
# Deletes resource groups from older Thinkschool exercises that are no longer
# needed. Your capstone (rg-shelflife-dev, rg-shelflife-prod) is NOT touched.
#
# Groups deleted:
#   rg-dev          — Container Apps + Container Registry experiment (piece33)
#   thinkschool-rg  — Quotes Static Web App + Service Bus (eastasia)
#
# Safe to run because:
#   - All code for these projects is preserved in git
#   - Neither group is referenced by ShelfLife Bicep or azd config
#   - Container Registry images are listed before deletion so you can pull any
#     you want to keep locally
#
# Usage:
#   ./scripts/cleanup-old-projects.ps1
# ─────────────────────────────────────────────────────────────────────────────

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step($msg) { Write-Host "`n[$([char]9654)] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  [OK] $msg"            -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "  [!!] $msg"            -ForegroundColor Yellow }
function Write-Info($msg) { Write-Host "  [i]  $msg"            -ForegroundColor Gray }

Write-Host ""
Write-Host "=================================================" -ForegroundColor Yellow
Write-Host "  ShelfLife  --  Old Project Cleanup"             -ForegroundColor Yellow
Write-Host "=================================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "  This script will DELETE the following resource groups:"
Write-Host "    - rg-dev          (southeastasia)"  -ForegroundColor Red
Write-Host "    - thinkschool-rg  (eastasia)"        -ForegroundColor Red
Write-Host ""
Write-Host "  Your capstone resources are NOT touched:"
Write-Host "    - rg-shelflife-dev   SAFE" -ForegroundColor Green
Write-Host "    - rg-shelflife-prod  SAFE" -ForegroundColor Green
Write-Host ""

# ── Safety check — confirm capstone RGs still exist ───────────────────────────
Write-Step "Verifying capstone resource groups are intact before proceeding"

foreach ($rg in @('rg-shelflife-dev', 'rg-shelflife-prod')) {
    $state = az group show --name $rg --query properties.provisioningState --output tsv 2>$null
    if ($state -eq 'Succeeded') {
        Write-Ok "$rg exists and is healthy"
    } else {
        Write-Warn "$rg not found or unhealthy — state: $state"
        Write-Warn "Verify your capstone environment before running cleanup."
    }
}

# ── List Container Registry images before deleting ────────────────────────────
Write-Step "Listing Container Registry images in rg-dev (pull any you want to keep)"

$registryExists = az acr show --name cr5qulxll7yezxo --resource-group rg-dev `
    --query name --output tsv 2>$null

if ($registryExists) {
    Write-Info "Images in cr5qulxll7yezxo:"
    az acr repository list --name cr5qulxll7yezxo --output table 2>$null
    Write-Host ""
    Write-Warn "If you want to keep any image, pull it now:"
    Write-Host '  docker pull cr5qulxll7yezxo.azurecr.io/<repo>:<tag>' -ForegroundColor Gray
} else {
    Write-Info "Container Registry not found or already deleted — skipping"
}

# ── Confirmation prompt ────────────────────────────────────────────────────────
Write-Host ""
Write-Host "─────────────────────────────────────────────────" -ForegroundColor DarkGray
$confirm = Read-Host "  Type YES to confirm deletion of rg-dev and thinkschool-rg"
if ($confirm -ne 'YES') {
    Write-Host "  Cancelled — no resources were deleted." -ForegroundColor Yellow
    exit 0
}

# ── Delete rg-dev ─────────────────────────────────────────────────────────────
Write-Step "Deleting rg-dev (Container Apps + Container Registry project)"

$rgDevExists = az group show --name rg-dev --query name --output tsv 2>$null
if ($rgDevExists) {
    az group delete --name rg-dev --yes --no-wait
    Write-Ok "rg-dev deletion initiated (running in background)"
} else {
    Write-Info "rg-dev not found — already deleted"
}

# ── Delete thinkschool-rg ─────────────────────────────────────────────────────
Write-Step "Deleting thinkschool-rg (Quotes app project)"

$rgTsExists = az group show --name thinkschool-rg --query name --output tsv 2>$null
if ($rgTsExists) {
    az group delete --name thinkschool-rg --yes --no-wait
    Write-Ok "thinkschool-rg deletion initiated (running in background)"
} else {
    Write-Info "thinkschool-rg not found — already deleted"
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=================================================" -ForegroundColor Green
Write-Host "  Cleanup initiated" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Deletions run asynchronously. Check status in ~2 minutes:"
Write-Host '  az group list --output table' -ForegroundColor Gray
Write-Host ""
Write-Host "  Expected remaining groups after cleanup:"
Write-Host "    rg-shelflife-dev          (capstone dev)"    -ForegroundColor Green
Write-Host "    rg-shelflife-prod         (capstone prod)"   -ForegroundColor Green
Write-Host "    LogAnalyticsDefaultResources  (Azure-managed)" -ForegroundColor Gray
Write-Host "    NetworkWatcherRG              (Azure-managed)" -ForegroundColor Gray
Write-Host ""
Write-Host "  Estimated monthly credit saving: ~`$10-15/month" -ForegroundColor Cyan
Write-Host ""
