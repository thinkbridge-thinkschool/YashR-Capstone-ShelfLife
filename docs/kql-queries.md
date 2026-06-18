# Day 26 — App Insights KQL Queries

All queries run against the workspace-based Application Insights instance
(`shelflife-{env}-ai`). Open **Logs** in the Azure portal and paste each block.

---

## 1. Request latency — p50 / p99 by endpoint (last 24 h)

```kql
requests
| where timestamp > ago(24h)
| where success == true
| summarize
    p50_ms  = percentile(duration, 50),
    p99_ms  = percentile(duration, 99),
    count   = count()
  by name
| order by p99_ms desc
```

**What to look for:** Any endpoint whose `p99_ms` is disproportionately high
compared to its `p50_ms` (large gap = occasional slow outliers, not a median
problem). Add `| where name startswith "/api/lending"` to scope to a module.

---

## 2. Request latency — p50 / p99 over time (5-min buckets)

```kql
requests
| where timestamp > ago(6h)
| summarize
    p50_ms = percentile(duration, 50),
    p99_ms = percentile(duration, 99)
  by bin(timestamp, 5m), name
| render timechart
```

---

## 3. Dependency call breakdown

Shows every outbound call (SQL, Service Bus, HTTP) grouped by target and type,
with average duration, p99, failure rate, and call count.

```kql
dependencies
| where timestamp > ago(24h)
| summarize
    avg_ms         = avg(duration),
    p99_ms         = percentile(duration, 99),
    failure_rate   = round(countif(success == false) * 100.0 / count(), 2),
    total_calls    = count()
  by type, target, name
| order by total_calls desc
```

**Columns of interest:**
- `type` — `SQL`, `azure service bus`, `HTTP`
- `target` — SQL server FQDN, Service Bus namespace, external host
- `failure_rate` — should be 0 under normal conditions

---

## 4. Error rate by endpoint (5-min rolling buckets)

```kql
requests
| where timestamp > ago(1h)
| summarize
    total        = count(),
    errors       = countif(success == false)
  by bin(timestamp, 5m), name
| extend error_rate_pct = round(errors * 100.0 / total, 2)
| where error_rate_pct > 0
| order by timestamp desc, error_rate_pct desc
```

---

## 5. Alert rule — error rate > 5 % over any 5-min window

Paste this as the **Log search** query when creating an Azure Monitor alert rule
(signal type: **Custom log search**, threshold: `> 0` rows returned).

```kql
requests
| where timestamp > ago(5m)
| summarize
    total  = count(),
    errors = countif(success == false)
| extend error_rate = errors * 100.0 / total
| where error_rate > 5
```

**Alert rule settings:**
| Field | Value |
|-------|-------|
| Evaluation frequency | 5 minutes |
| Aggregation granularity | 5 minutes |
| Operator | Greater than |
| Threshold | 0 (any row = alert fires) |
| Severity | 2 – Warning |

---

## 6. Distributed trace — follow a single operation across API and Worker

Each request lands in App Insights with an `operation_Id` (the W3C `traceId`).
Copy it from the **End-to-end transaction** view and run:

```kql
union requests, dependencies, traces, exceptions
| where operation_Id == "<paste-operation-id-here>"
| project
    timestamp,
    cloud_RoleName,
    itemType,
    name,
    duration,
    success,
    operation_Id,
    operation_ParentId,
    message
| order by timestamp asc
```

**How the stitching works:**

```
HTTP POST /api/lending/borrow   (ShelfLife.Api)
  └─ db.execute SELECT …        (SQL — child of API span)
  └─ db.execute INSERT …        (SQL — child of API span)
  └─ ServiceBus.Send            (Service Bus — child of API span)
       traceparent written to message ApplicationProperties

OverdueWorker.ProcessCycle      (ShelfLife.OverdueWorker)
  └─ db.execute SELECT …        (SQL — child of worker span)
  └─ ServiceBus.Send            (Service Bus — child of worker span,
                                  traceparent propagated to next consumer)
```

`cloud_RoleName` lets you filter by service; `operation_ParentId` reconstructs
the parent–child tree.  In **Application Map**, the topology renders as:

```
[ShelfLife.Api] ──SQL──► [Azure SQL]
                └──SB───► [Service Bus: lending.overdue]

[ShelfLife.OverdueWorker] ──SQL──► [Azure SQL]
                           └──SB───► [Service Bus: lending.overdue]
```

---

## 7. Exceptions by role / endpoint

```kql
exceptions
| where timestamp > ago(24h)
| summarize count() by cloud_RoleName, outerMessage, type
| order by count_ desc
```

---

## 8. Slow SQL queries (> 500 ms)

```kql
dependencies
| where timestamp > ago(24h)
| where type == "SQL"
| where duration > 500
| project timestamp, cloud_RoleName, name, target, duration, success
| order by duration desc
```
