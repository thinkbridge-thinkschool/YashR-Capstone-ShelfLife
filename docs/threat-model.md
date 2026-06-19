# ShelfLife — STRIDE-Lite Threat Model

> **Scope:** ShelfLife capstone — API host (ASP.NET Core 10), Azure SQL, Service Bus, Key Vault, and the Overdue background worker.  
> **Date:** 2026-06-19 | **Author:** Security Pass (Day 27)

---

## 1. System Overview

```
Browser / Mobile Client
        │ HTTPS (TLS 1.2+)
        ▼
┌─────────────────────────────────┐
│  App Service  (ShelfLife.Api)   │  ← Managed Identity
│  • Identity / Catalog           │
│  • Lending / Insights           │
│  • Notifications (handler)      │
└──────┬────────────────┬─────────┘
       │ VNet (private) │ VNet (private)
       ▼                ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  Azure SQL   │  │  Key Vault   │  │ Service Bus  │
│  (ShelfLife) │  │  (secrets)   │  │  (events)    │
└──────────────┘  └──────────────┘  └──────┬───────┘
                                           │
                              ┌────────────▼────────────┐
                              │  OverdueWorker  +        │
                              │  OutboxRelayWorker       │
                              └─────────────────────────┘
```

**Data flows:**
| # | From | To | Transport | Auth |
|---|------|----|-----------|------|
| DF-1 | Browser | App Service | HTTPS | JWT (Entra ID) |
| DF-2 | App Service | Azure SQL | VNet private endpoint | Managed Identity (AAD Default) |
| DF-3 | App Service | Key Vault | VNet private endpoint | Managed Identity (RBAC) |
| DF-4 | App Service | Service Bus | TLS | Managed Identity (RBAC) |
| DF-5 | OutboxRelayWorker | Service Bus | TLS | Managed Identity |
| DF-6 | OverdueWorker | Azure SQL | VNet private endpoint | Managed Identity |

---

## 2. Trust Boundaries

| Boundary | Description |
|----------|-------------|
| **TB-1** | Public internet → App Service (WAF not deployed; TLS termination at App Service) |
| **TB-2** | App Service compute → Azure data plane (VNet via private endpoint after Day 27 fix) |
| **TB-3** | Azure data plane internal (SQL ↔ Service Bus) — never directly connected |
| **TB-4** | GitHub Actions CI/CD → Azure (OIDC federated credential; secrets scoped to `production` environment) |

---

## 3. STRIDE Threat Catalogue

### S — Spoofing

| ID | Component | Threat | Current Mitigations | Residual Risk | Fix Applied |
|----|-----------|--------|---------------------|---------------|-------------|
| S-01 | `POST /api/v1/identity/login` | Credential stuffing — attacker fires thousands of username/password pairs to harvest valid JWTs | bcrypt hashing makes offline cracking expensive; HTTPS prevents sniffing | **Medium** — no account lockout, no rate limit on auth path | Rate limiter: 10 req/min per IP on `/identity/*` |
| S-02 | `POST /api/v1/identity/register` | Mass account creation to exhaust DB rows or obtain tokens for downstream abuse | bcrypt; email field stored | **Medium** before fix | Rate limiter: same 10 req/min policy |
| S-03 | JWT `sub` claim | Tampered or replayed token after Entra key rotation | Tokens signed with Entra private key; JWKS auto-rotates; 8 h expiry | Low | — |
| S-04 | Managed Identity token | Attacker on same App Service plan steals IMDS token | App Service isolates MI per-app; token bound to resource audience | Very Low | — |

### T — Tampering

| ID | Component | Threat | Current Mitigations | Residual Risk | Fix Applied |
|----|-----------|--------|---------------------|---------------|-------------|
| T-01 | SQL Database (public endpoint) | Attacker with network access runs arbitrary DML if SQL firewall is misconfigured | Firewall "Allow Azure Services" is broad — any Azure IP qualifies | **High** before fix | Private endpoint + `publicNetworkAccess: Disabled` |
| T-02 | Outbox table | Malicious process inserts crafted `OutboxMessage` rows to inject unexpected events | DB access via MI only (no shared password); application-level schema validation | Low | — |
| T-03 | Request body — `AddBookByIsbnCommand` | Oversized or malformed JSON body causing parser DoS or unexpected domain state | EF Core parameterised queries prevent SQL injection; ISBN validated in domain | Low | 64 KB global request body limit |
| T-04 | `pageSize` query param on `/insights/*` | Passing `pageSize=2147483647` causes unbounded SQL `FETCH NEXT` — excessive memory/CPU | EF Core `Take(pageSize)` translates directly | **Medium** before fix | `Math.Clamp(pageSize, 1, 100)` |

### R — Repudiation

| ID | Component | Threat | Current Mitigations | Residual Risk | Fix Applied |
|----|-----------|--------|---------------------|---------------|-------------|
| R-01 | Lending borrow/return | Member denies borrowing — no tamper-evident audit trail | Serilog request log + App Insights traces record timestamp and user claim | Medium — logs mutable by admin | No code fix (out-of-scope: immutable audit log requires append-only store) |
| R-02 | Librarian catalog mutations | Librarian denies adding a book — no per-action audit | Same Serilog/OTel traces | Medium | Same note |

### I — Information Disclosure

| ID | Component | Threat | Current Mitigations | Residual Risk | Fix Applied |
|----|-----------|--------|---------------------|---------------|-------------|
| I-01 | Azure SQL public endpoint | SQL server FQDN resolves publicly; firewall bypass via Azure IP could expose DB | TLS 1.2; AD Default auth (no password); firewall restricts IPs | **High** before fix | Private endpoint; `publicNetworkAccess: Disabled`; firewall rule removed |
| I-02 | Key Vault public endpoint | App Insights connection string reachable from any Azure IP if KV firewall misconfigured | RBAC; soft delete; TLS | **Medium** before fix | Private endpoint; `publicNetworkAccess: Disabled` |
| I-03 | Stack traces in error responses | Unhandled exceptions return ASP.NET developer exception page details | Developer exception page disabled in prod (`ASPNETCORE_ENVIRONMENT=Production`) | Low | — |
| I-04 | `Server: Kestrel` response header | Reveals runtime — aids fingerprinting | Default Kestrel behaviour | Low | `AddServerHeader = false` in Kestrel options |
| I-05 | Swagger UI in production | OpenAPI schema reveals all endpoint paths and shapes — reconnaissance aid | `app.UseSwagger()` guarded by `IsDevelopment()` check | Low | — (already mitigated) |
| I-06 | JWT signing key in `appsettings.json` | Dev HS256 symmetric key committed to repo | Dev-only; production uses Entra ID JWKS (asymmetric, key never in config) | Low (prod) | — |

### D — Denial of Service

| ID | Component | Threat | Current Mitigations | Residual Risk | Fix Applied |
|----|-----------|--------|---------------------|---------------|-------------|
| D-01 | All endpoints | Unauthenticated request flood overwhelms Kestrel thread pool | Azure App Service autoscale (when configured); TLS overhead limits raw SYN floods | **High** before fix | Fixed-window rate limiter: 10 req/min (identity), 60 req/min (API) per IP; HTTP 429 on breach |
| D-02 | `/api/v1/insights/*` paging | `page=0&pageSize=999999` executes `SELECT TOP 999999` — saturates SQL I/O | None | **Medium** before fix | Page clamped to [1, 100]; page defaulted to 1 |
| D-03 | Request body size | Multi-megabyte JSON bodies cause memory pressure during deserialization | ASP.NET Core default 28 MB limit | Medium | Kestrel `MaxRequestBodySize = 65_536` (64 KB) |

### E — Elevation of Privilege

| ID | Component | Threat | Current Mitigations | Residual Risk | Fix Applied |
|----|-----------|--------|---------------------|---------------|-------------|
| E-01 | `/api/v1/catalog/*` | Authenticated Member submits Librarian-level request | `RequireAuthorization("Librarian")` enforces `roles` claim | Low | — |
| E-02 | `/api/v1/insights/*` | Same — Member reads aggregated data | `RequireAuthorization("Librarian")` | Low | — |
| E-03 | `Guid.Parse(user.FindFirstValue("sub"))` | Malformed `sub` claim causes unhandled `FormatException` → 500 leaks stack frame | Entra-issued tokens always have valid GUIDs | Low (Entra path); Medium (dev HS256 path) | `Guid.TryParse` guard → 401 on bad claim |
| E-04 | CI/CD pipeline | Compromised GitHub Actions runner escalates to Azure prod via OIDC credential | Secrets scoped to `production` environment; manual-trigger only on CD | Low | — |

---

## 4. Risk Summary

| Priority | Threats | Fix |
|----------|---------|-----|
| **Critical (fix now)** | I-01, I-02, T-01 | Private endpoints (IaC change) |
| **High** | D-01, S-01, S-02 | Rate limiting (code change) |
| **Medium** | D-02, D-03, T-04, E-03 | Input limits + safe parse (code change) |
| **Low** | I-04, R-01, R-02 | Server header removed; audit log deferred |

---

## 5. Mitigations Not In Scope

- **WAF (Web Application Firewall):** App Service Standard tier doesn't include Azure WAF; would require Application Gateway Premium or Azure Front Door.
- **DDoS Protection:** Standard Azure DDoS plan adds cost; Basic is included by default.
- **Immutable Audit Log:** Requires append-only storage (e.g., Azure Table Storage with immutability policy) — deferred.
- **Account Lockout:** Requires a failed-attempt counter in `Members` table — deferred to Day 28+.
- **Secret Rotation:** Key Vault Event Grid + Azure Automation runbook — deferred.
