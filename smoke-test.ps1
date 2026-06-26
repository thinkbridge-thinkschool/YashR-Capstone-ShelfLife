# ShelfLife API Smoke Test
# Usage: .\smoke-test.ps1 [-Base http://localhost:5000]
param([string]$Base = "http://localhost:5000")

$global:pass = 0
$global:fail = 0

function Pass($label) {
    Write-Host "  [PASS] $label" -ForegroundColor Green
    $global:pass++
}
function Fail($label, $detail) {
    Write-Host "  [FAIL] $label  =>  $detail" -ForegroundColor Red
    $global:fail++
}

function Req {
    param($Method, $Path, $Body, $Token)
    $headers = @{ "Content-Type" = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $uri = "$Base$Path"
    try {
        $params = @{
            Method          = $Method
            Uri             = $uri
            Headers         = $headers
            UseBasicParsing = $true
            ErrorAction     = "Stop"
        }
        if ($Body) { $params["Body"] = ($Body | ConvertTo-Json -Compress) }
        $r = Invoke-WebRequest @params
        $parsed = $null
        try { $parsed = $r.Content | ConvertFrom-Json } catch {}
        return @{ Status = [int]$r.StatusCode; Body = $parsed; Raw = $r.Content }
    } catch {
        $code = 0
        try { $code = [int]$_.Exception.Response.StatusCode } catch {}
        $raw = ""
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $sr = New-Object System.IO.StreamReader($stream)
            $raw = $sr.ReadToEnd()
        } catch {}
        return @{ Status = $code; Body = $null; Raw = $raw; Err = $_.Exception.Message }
    }
}

function Chk($label, $r, $expected) {
    if ($r.Status -eq $expected) {
        Pass $label
        return $true
    } else {
        $detail = "HTTP $($r.Status) (expected $expected)"
        if ($r.Raw) { $detail += "  raw: $($r.Raw.Substring(0, [Math]::Min(120, $r.Raw.Length)))" }
        elseif ($r.Err) { $detail += "  err: $($r.Err)" }
        Fail $label $detail
        return $false
    }
}

Write-Host ""
Write-Host "ShelfLife Smoke Test  ->  $Base" -ForegroundColor Cyan
Write-Host ("=" * 55)

# -------------------------------------------------------
# 1. IDENTITY
# -------------------------------------------------------
Write-Host ""
Write-Host "[Identity]"

$ts         = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$testEmail  = "smoketest$ts@example.com"

# Register new member
$r = Req POST "/api/v1/identity/register" @{ email = $testEmail; password = "Test@1234"; fullName = "Smoke Tester" }
Chk "POST /register -> 201" $r 201 | Out-Null
$memberId = if ($r.Body) { $r.Body.id } else { $null }

# Librarian login
$r = Req POST "/api/v1/identity/login" @{ email = "librarian@shelflife.dev"; password = "Librarian@123" }
Chk "POST /login (librarian) -> 200" $r 200 | Out-Null
$libToken = if ($r.Body) { $r.Body.token } else { $null }

# Member login
$r = Req POST "/api/v1/identity/login" @{ email = $testEmail; password = "Test@1234" }
Chk "POST /login (member) -> 200" $r 200 | Out-Null
$memToken = if ($r.Body) { $r.Body.token } else { $null }

# Decode JWT to get member sub (memberId)
$memSubId = $null
if ($memToken) {
    try {
        $parts   = $memToken.Split('.')
        $payload = $parts[1]
        $pad     = 4 - ($payload.Length % 4)
        if ($pad -ne 4) { $payload = $payload + ("=" * $pad) }
        $bytes   = [Convert]::FromBase64String($payload)
        $json    = [System.Text.Encoding]::UTF8.GetString($bytes)
        $memSubId = ($json | ConvertFrom-Json).sub
    } catch {}
}

# GET members (librarian)
$r = Req GET "/api/v1/identity/members?page=1&pageSize=10" -Token $libToken
Chk "GET /members (librarian) -> 200" $r 200 | Out-Null

# GET members without token -> 401
$r = Req GET "/api/v1/identity/members?page=1&pageSize=10"
if ($r.Status -eq 401) { Pass "GET /members (no auth) -> 401" } else { Fail "GET /members (no auth) -> 401" "got HTTP $($r.Status)" }

# -------------------------------------------------------
# 2. CATALOG
# -------------------------------------------------------
Write-Host ""
Write-Host "[Catalog]"

# GET books (any authenticated user)
$r = Req GET "/api/v1/catalog/books?page=1&pageSize=10" -Token $memToken
Chk "GET /books (member auth) -> 200" $r 200 | Out-Null

# POST book manually (librarian) — body must use publicationYear, not year
$r = Req POST "/api/v1/catalog/books/manual" @{
    title           = "Smoke Test Book $ts"
    author          = "Test Author"
    publicationYear = 2024
} -Token $libToken
Chk "POST /books/manual (librarian) -> 201" $r 201 | Out-Null
$bookId = if ($r.Body) { $r.Body.id } else { $null }

# POST copy (librarian)
$copyId = $null
if ($bookId) {
    $r = Req POST "/api/v1/catalog/books/$bookId/copies" @{ barcode = "BC$ts" } -Token $libToken
    Chk "POST /books/{id}/copies (librarian) -> 201" $r 201 | Out-Null
    $copyId = if ($r.Body) { $r.Body.id } else { $null }
} else {
    Fail "POST /books/{id}/copies" "skipped (no bookId)"
}

# POST books without auth -> 401
$r = Req POST "/api/v1/catalog/books/manual" @{ title="x"; author="x"; isbn="x"; year=2024; genre="x" }
if ($r.Status -eq 401) { Pass "POST /books/manual (no auth) -> 401" } else { Fail "POST /books/manual (no auth) -> 401" "got HTTP $($r.Status)" }

# -------------------------------------------------------
# 3. LENDING
# -------------------------------------------------------
Write-Host ""
Write-Host "[Lending]"

# Borrow book (librarian issues loan to new member)
$loanId = $null
if ($bookId -and $memSubId) {
    $r = Req POST "/api/v1/lending/loans" @{ memberId = $memSubId; bookTitleId = $bookId } -Token $libToken
    Chk "POST /loans (librarian) -> 201" $r 201 | Out-Null
    $loanId = if ($r.Body) { $r.Body.loanId } else { $null }
} else {
    Fail "POST /loans" "skipped (bookId=$bookId memberId=$memSubId)"
}

# GET all loans (librarian)
$r = Req GET "/api/v1/lending/loans?page=1&pageSize=10&activeOnly=false" -Token $libToken
Chk "GET /loans (librarian) -> 200" $r 200 | Out-Null

# GET active loans (librarian)
$r = Req GET "/api/v1/lending/loans?page=1&pageSize=10&activeOnly=true" -Token $libToken
Chk "GET /loans?activeOnly=true -> 200" $r 200 | Out-Null

# GET holds (librarian)
$r = Req GET "/api/v1/lending/holds?page=1&pageSize=10" -Token $libToken
Chk "GET /holds (librarian) -> 200" $r 200 | Out-Null

# GET my-loans (member)
if ($memToken) {
    $r = Req GET "/api/v1/lending/my-loans?page=1&pageSize=10&activeOnly=false" -Token $memToken
    Chk "GET /my-loans (member) -> 200" $r 200 | Out-Null

    # Verify the borrowed loan actually appears for the member
    if ($loanId -and $r.Body) {
        $found = $r.Body.items | Where-Object { $_.loanId -eq $loanId }
        if ($found) { Pass "  loan visible in GET /my-loans" } else { Fail "  loan visible in GET /my-loans" "loanId $loanId not in response (total=$($r.Body.totalCount))" }
    }

    $r = Req GET "/api/v1/lending/my-holds?page=1&pageSize=10" -Token $memToken
    Chk "GET /my-holds (member) -> 200" $r 200 | Out-Null
} else {
    Fail "GET /my-loans (member)" "skipped (no member token)"
    Fail "GET /my-holds (member)" "skipped (no member token)"
}

# Return the loan
if ($loanId) {
    $r = Req POST "/api/v1/lending/loans/$loanId/return" -Token $libToken
    Chk "POST /loans/{id}/return -> 200" $r 200 | Out-Null
} else {
    Fail "POST /loans/{id}/return" "skipped (no loanId)"
}

# -------------------------------------------------------
# 4. INSIGHTS
# -------------------------------------------------------
Write-Host ""
Write-Host "[Insights]"

$r = Req GET "/api/v1/insights/popular-titles?page=1&pageSize=10" -Token $libToken
Chk "GET /insights/popular-titles (librarian) -> 200" $r 200 | Out-Null

$r = Req GET "/api/v1/insights/overdue-loans?page=1&pageSize=10" -Token $libToken
Chk "GET /insights/overdue-loans (librarian) -> 200" $r 200 | Out-Null

$r = Req GET "/api/v1/insights/member-activity?page=1&pageSize=10" -Token $libToken
Chk "GET /insights/member-activity (librarian) -> 200" $r 200 | Out-Null

# Insights without auth -> 401
$r = Req GET "/api/v1/insights/popular-titles?page=1&pageSize=10"
if ($r.Status -eq 401) { Pass "GET /insights/* (no auth) -> 401" } else { Fail "GET /insights/* (no auth) -> 401" "got HTTP $($r.Status)" }

# -------------------------------------------------------
# SUMMARY
# -------------------------------------------------------
Write-Host ""
Write-Host ("=" * 55)
$total = $global:pass + $global:fail
$color = if ($global:fail -eq 0) { "Green" } else { "Yellow" }
Write-Host "Results: $($global:pass)/$total passed" -ForegroundColor $color
if ($global:fail -gt 0) {
    Write-Host "$($global:fail) endpoint(s) failed - see [FAIL] lines above" -ForegroundColor Red
}
