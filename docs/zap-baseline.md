# ShelfLife — OWASP ZAP Baseline Summary

> **Tool:** OWASP ZAP 2.14 — Baseline Scan (`zap-baseline.py`)  
> **Target:** `http://localhost:8080` (Docker Compose stack)  
> **Date:** 2026-06-19 | **Author:** Security Pass (Day 27)  
> **Command:**
> ```bash
> docker run --network host ghcr.io/zaproxy/zaproxy:stable \
>   zap-baseline.py -t http://localhost:8080 -r zap-report.html
> ```

---

## Scan Summary

| Risk | Count Before | Count After Fix |
|------|-------------|-----------------|
| High | 0 | 0 |
| Medium | 4 | 0 |
| Low | 3 | 1 |
| Informational | 2 | 1 |
| **Total** | **9** | **2** |

---

## Findings and Fixes

### MEDIUM-1 — Missing Anti-Clickjacking Header

| Field | Value |
|-------|-------|
| **Alert** | Anti-clickjacking Header |
| **CWE** | CWE-1021 |
| **WASC** | WASC-15 |
| **URL** | All responses |
| **Evidence** | No `X-Frame-Options` or `Content-Security-Policy: frame-ancestors` header present |

**Risk:** An attacker embeds the API's Swagger UI (dev) or any HTML response in an `<iframe>` on a malicious page (clickjacking).

**Fix applied — `Program.cs` security headers middleware:**
```csharp
ctx.Response.Headers.Append("X-Frame-Options",         "DENY");
ctx.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
```

**Verified:** ZAP re-scan shows `X-Frame-Options: DENY` on all responses. Alert cleared.

---

### MEDIUM-2 — X-Content-Type-Options Header Missing

| Field | Value |
|-------|-------|
| **Alert** | X-Content-Type-Options Header Missing |
| **CWE** | CWE-693 |
| **WASC** | WASC-15 |
| **URL** | All responses |
| **Evidence** | `X-Content-Type-Options` header not present |

**Risk:** Browser MIME-type sniffing can cause a JSON response to be interpreted as HTML/script by older browsers, enabling stored-XSS-like behaviour.

**Fix applied:**
```csharp
ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
```

**Verified:** Alert cleared after fix.

---

### MEDIUM-3 — Content Security Policy (CSP) Header Not Set

| Field | Value |
|-------|-------|
| **Alert** | Content Security Policy (CSP) Header Not Set |
| **CWE** | CWE-693 |
| **WASC** | WASC-15 |
| **URL** | All responses |

**Risk:** Without CSP, injected scripts (if a future HTML endpoint is added) can execute without browser-level restriction.

**Fix applied:**
```csharp
ctx.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
```

This is the correct policy for a pure JSON API — deny everything by default.

**Verified:** Alert cleared after fix.

---

### MEDIUM-4 — Missing Rate Limiting on Authentication Endpoints

| Field | Value |
|-------|-------|
| **Alert** | (Custom check) Credential Stuffing Vector |
| **CWE** | CWE-307 |
| **URL** | `POST /api/v1/identity/login`, `POST /api/v1/identity/register` |
| **Evidence** | 1000 sequential POST requests to `/login` completed without any 429 response |

**Risk:** Attacker can run automated credential stuffing or password spray campaigns with no server-side throttle.

**Fix applied — `Program.cs`:**
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("identity", cfg =>
    {
        cfg.Window      = TimeSpan.FromMinutes(1);
        cfg.PermitLimit = 10;          // 10 auth attempts per client per minute
        cfg.QueueLimit  = 0;           // reject immediately, no queuing
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Applied to the identity route group:
app.MapGroup("/api/v1/identity")
   .MapIdentityEndpoints()
   .RequireRateLimiting("identity");
```

**Verified:** 11th request within a 60-second window receives `HTTP 429 Too Many Requests`.

---

### LOW-1 — Referrer-Policy Header Not Set

| Field | Value |
|-------|-------|
| **Alert** | Referrer-Policy Header Not Set |
| **CWE** | CWE-200 |
| **URL** | All responses |

**Risk:** Browsers send the full `Referer` header including query parameters, potentially leaking sensitive URL fragments to third-party servers referenced in responses.

**Fix applied:**
```csharp
ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
```

**Verified:** Alert cleared after fix.

---

### LOW-2 — Server Version Disclosure (Server header)

| Field | Value |
|-------|-------|
| **Alert** | Server Leaks Version Information via "Server" HTTP Response Header |
| **CWE** | CWE-200 |
| **Evidence** | `Server: Kestrel` present on all responses |

**Risk:** Exposes the web server type and version to attackers, aiding targeted exploit selection.

**Fix applied — `Program.cs`:**
```csharp
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
});
```

**Verified:** `Server` header absent from all responses after fix.

---

### LOW-3 — Permissions-Policy Header Not Set

| Field | Value |
|-------|-------|
| **Alert** | Permissions-Policy Header Not Set |
| **URL** | All responses |

**Risk:** Without a Permissions-Policy header, browser features (geolocation, camera, microphone) remain available to any code running in the browsing context.

**Fix applied:**
```csharp
ctx.Response.Headers.Append("Permissions-Policy", "geolocation=(), camera=(), microphone=()");
```

**Verified:** Alert cleared after fix.

---

### INFORMATIONAL-1 — Swagger UI Exposed (Dev only)

| Field | Value |
|-------|-------|
| **Alert** | Exposed API Documentation |
| **URL** | `GET /swagger/index.html` |
| **Evidence** | Swagger UI accessible in Development environment |

**Assessment:** Acceptable — Swagger is guarded by `if (app.Environment.IsDevelopment())`. In production (`ASPNETCORE_ENVIRONMENT=Production`), the `/swagger` path returns 404. No action required.

---

### INFORMATIONAL-2 — Unbounded Pagination (Residual)

| Field | Value |
|-------|-------|
| **Alert** | (Custom check) Large Page Size Accepted |
| **URL** | `GET /api/v1/insights/popular-titles?page=1&pageSize=999999` |
| **Evidence** | Before fix: server accepted arbitrary `pageSize`; SQL executed `FETCH NEXT 999999 ROWS` |

**Fix applied — `InsightsEndpoints.cs`:**
```csharp
private const int MaxPageSize = 100;

page     = Math.Max(1, page);
pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
```

**Verified:** `pageSize=999999` is silently clamped to 100. Query plan shows `FETCH NEXT 100 ROWS ONLY`.

---

## Remaining Accepted Risk

| Finding | Reason Accepted |
|---------|----------------|
| No HSTS preloading | App Service `httpsOnly: true` + `app.UseHsts()` sets the header; preload list submission is a separate ops process |
| No WAF / DDoS Standard | Requires Application Gateway Premium or Azure Front Door — out of budget scope for capstone |
| Account lockout absent | Deferred to Day 28 — requires `FailedAttempts` column in `Members` table |

---

## Reproducibility

To re-run the baseline scan locally:

```bash
# 1. Start the stack
docker compose up -d

# 2. Wait for API to be healthy
curl -s http://localhost:8080/swagger/index.html | grep -i title

# 3. Run ZAP baseline
docker run --network host ghcr.io/zaproxy/zaproxy:stable \
  zap-baseline.py \
  -t http://localhost:8080 \
  -r /zap/wrk/zap-report.html \
  -J /zap/wrk/zap-report.json

# 4. Open zap-report.html in a browser
```
