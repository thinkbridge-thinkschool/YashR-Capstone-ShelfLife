# ── Day 32 Production Deployment Runbook ─────────────────────────────────────
# Run this script the evening before or morning of your Day 32 demo.
#
# What it does:
#   1. Validates the SQL admin password is set
#   2. Deploys the full prod stack via azd (VNet, SQL, Service Bus, App Service, Key Vault)
#   3. Deploys the application code
#   4. Warms up the API to eliminate cold-start on demo day
#   5. Prints the live URL and a pre-demo checklist
#
# Usage:
#   $env:SQL_ADMIN_PASSWORD = "YourSqlPassword123!"
#   ./scripts/day32-deploy-prod.ps1
# ─────────────────────────────────────────────────────────────────────────────

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Helpers ───────────────────────────────────────────────────────────────────
function Write-Step($msg)  { Write-Host "`n[$([char]9654)] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "  [OK] $msg"            -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "  [!!] $msg"            -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "  [XX] $msg"            -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "=================================================" -ForegroundColor Magenta
Write-Host "  ShelfLife  --  Day 32 Production Deployment"    -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

# ── Step 1: Validate SQL password ─────────────────────────────────────────────
Write-Step "Checking prerequisites"

if (-not $env:SQL_ADMIN_PASSWORD) {
    Write-Host ""
    Write-Warn "SQL_ADMIN_PASSWORD is not set in this session."
    Write-Host "  Set it now (it will not be stored anywhere):" -ForegroundColor Gray
    Write-Host '  $env:SQL_ADMIN_PASSWORD = "YourPassword123!"' -ForegroundColor Yellow
    Write-Host ""
    $pwd = Read-Host "  Enter SQL admin password" -AsSecureString
    $env:SQL_ADMIN_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pwd)
    )
}
Write-Ok "SQL_ADMIN_PASSWORD is set"

# Check az CLI is logged in
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) { Write-Fail "Not logged in to Azure. Run: az login" }
Write-Ok "Logged in as: $($account.user.name) ($($account.name))"

# Check azd is available
if (-not (Get-Command azd -ErrorAction SilentlyContinue)) {
    Write-Fail "azd CLI not found. Install from: https://aka.ms/azd"
}
Write-Ok "azd CLI found"

# ── Step 2: Select prod environment ───────────────────────────────────────────
Write-Step "Selecting prod environment"

$envList = azd env list --output json 2>$null | ConvertFrom-Json
$prodEnv = $envList | Where-Object { $_.Name -eq 'prod' }

if (-not $prodEnv) {
    Write-Warn "azd prod environment not found — creating it now"
    azd env new prod
    azd env set AZURE_LOCATION southeastasia --environment prod
    azd env set AZURE_SUBSCRIPTION_ID 6fd36877-8e0f-4ced-8f31-4b036b566b9b --environment prod
}

azd env select prod
Write-Ok "prod environment selected"

# Store the password so the postprovision hook (deploy-stack.ps1) can read it
azd env set SQL_ADMIN_PASSWORD $env:SQL_ADMIN_PASSWORD --environment prod
Write-Ok "SQL password stored in azd prod environment"

# ── Step 3: Provision infrastructure ──────────────────────────────────────────
Write-Step "Provisioning prod infrastructure (VNet + SQL + Service Bus + App Service + Key Vault)"
Write-Warn "This takes ~5-8 minutes. Do NOT cancel."
Write-Host ""

azd provision --environment prod --no-prompt
if ($LASTEXITCODE -ne 0) { Write-Fail "azd provision failed. Check the output above." }
Write-Ok "Infrastructure provisioned"

# ── Step 4: Deploy application code ───────────────────────────────────────────
Write-Step "Deploying application code to prod App Service"

azd deploy --environment prod --no-prompt
if ($LASTEXITCODE -ne 0) { Write-Fail "azd deploy failed. Check the output above." }
Write-Ok "Application deployed"

# ── Step 5: Get live URL and warm up ──────────────────────────────────────────
Write-Step "Retrieving live URL"

$apiUrl = az webapp show `
    --name shelflife-prod-api `
    --resource-group rg-shelflife-prod `
    --query defaultHostName --output tsv

if (-not $apiUrl) { Write-Fail "Could not retrieve app URL." }
$liveUrl = "https://$apiUrl"
Write-Ok "Live URL: $liveUrl"

Write-Step "Warming up the API (eliminating cold-start before demo)"
Write-Host "  Sending 3 warm-up requests to /health ..." -ForegroundColor Gray

$warmed = $false
for ($i = 1; $i -le 5; $i++) {
    try {
        $resp = Invoke-WebRequest -Uri "$liveUrl/health" -UseBasicParsing -TimeoutSec 30
        if ($resp.StatusCode -eq 200) {
            Write-Ok "Health check passed (attempt $i) — app is warm"
            $warmed = $true
            break
        }
    } catch {
        Write-Host "  Attempt $i: not ready yet, retrying in 10s..." -ForegroundColor Gray
        Start-Sleep -Seconds 10
    }
}

if (-not $warmed) {
    Write-Warn "Health check did not return 200 after 5 attempts."
    Write-Warn "Check App Insights for startup errors before the demo."
}

# ── Step 6: Verify private endpoints are reachable ────────────────────────────
Write-Step "Verifying prod networking"

$sqlPe = az network private-endpoint show `
    --name shelflife-prod-sql-pe `
    --resource-group rg-shelflife-prod `
    --query provisioningState --output tsv 2>$null

if ($sqlPe -eq 'Succeeded') {
    Write-Ok "SQL private endpoint: Succeeded"
} else {
    Write-Warn "SQL private endpoint state: $sqlPe — check rg-shelflife-prod in portal"
}

$kvPe = az network private-endpoint show `
    --name shelflife-prod-kv-pe `
    --resource-group rg-shelflife-prod `
    --query provisioningState --output tsv 2>$null

if ($kvPe -eq 'Succeeded') {
    Write-Ok "Key Vault private endpoint: Succeeded"
} else {
    Write-Warn "Key Vault private endpoint state: $kvPe — check rg-shelflife-prod in portal"
}

# ── Final summary ──────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=================================================" -ForegroundColor Magenta
Write-Host "  Deployment complete" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  Live URL   : $liveUrl"                          -ForegroundColor Cyan
Write-Host "  Swagger UI : $liveUrl/swagger"                  -ForegroundColor Cyan
Write-Host "  Health     : $liveUrl/health"                   -ForegroundColor Cyan
Write-Host ""
Write-Host "  Day 32 pre-demo checklist:" -ForegroundColor Yellow
Write-Host "  [ ] Open $liveUrl/swagger — confirm API responds"
Write-Host "  [ ] Run one end-to-end request (add a book, create a loan)"
Write-Host "  [ ] Open App Insights in portal — confirm traces are flowing"
Write-Host "  [ ] Copy live URL into your postmortem document"
Write-Host ""
Write-Host "  After demo — teardown prod to stop billing:" -ForegroundColor Gray
Write-Host "  ./scripts/teardown-stack.ps1 -Env prod"       -ForegroundColor Gray
Write-Host ""
