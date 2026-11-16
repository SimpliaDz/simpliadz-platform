# Scheduled inventory warnings

Each API instance checks the tenant assigned to it. A background worker wakes every minute and runs a scan when the tenant's saved schedule is due. The scan interval can be 5 to 1,440 minutes. The schedule is disabled by default until an authorized manager configures it. A database lease prevents two API instances assigned to the same tenant from scanning at the same time. An authorized manager can also trigger a scan manually.

The scan maintains two warning types:

- **LowStock:** a configured variant/location is at or below its reorder point, including zero stock.
- **Ageing:** a variant/location with positive stock has been stocked for at least the configured number of days. Age starts at its first positive stock movement after the balance was last zero. It resets when the balance reaches zero and stock is later added again.

Warnings are refreshed on each scan and resolved when their condition no longer holds. Their IDs, variant and location, quantity, threshold or stock entry date, and detection times can be queried. This is a per-variant/location approximation; it does not track the age of individual receipt batches.

| Endpoint | Permission | Purpose |
| --- | --- | --- |
| `GET /inventory-warnings/settings` | `inventory-warnings.view` | Read schedule and thresholds. |
| `PUT /inventory-warnings/settings` | `inventory-warnings.manage` | Enable or disable checks and set interval and age threshold. |
| `POST /inventory-warnings/run` | `inventory-warnings.manage` | Start an immediate scan if no other scan holds the lease. |
| `GET /inventory-warnings?locationId=...&kind=...&activeOnly=true` | `inventory-warnings.view` | Query current or resolved warnings. |

Example settings body:

```json
{
  "enabled": true,
  "lowStockEnabled": true,
  "ageingEnabled": true,
  "intervalMinutes": 60,
  "ageThresholdDays": 90
}
```

Apply the `Inventory_ScheduledWarnings` migration to each tenant database before enabling the worker. The job lives in the API process; if an API instance is stopped, its tenant's scan resumes when the instance starts again. A dedicated worker host can be added later without changing the tenant database model.
