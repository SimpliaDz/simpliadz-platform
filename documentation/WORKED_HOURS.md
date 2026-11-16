# Worked hours

Worked hours use the existing Organization module and tenant database. The organization profile now has a `timeZoneId`, defaulting to `Africa/Algiers`, which an authorized manager can update through `PUT /organization`. Each work session stores UTC timestamps and one user ID. Daily reports convert the timestamps to the organization's local time zone and split overnight work at local midnight.

V1 rules:

- Staff can have only one open work session at a time. The database enforces this with a filtered unique index.
- Staff can clock in and clock out for themselves. Clock-out must follow clock-in and cannot happen twice.
- Managers with `work-sessions.manage` can correct start and end times with a reason. Each correction records the old and new times, actor, and timestamp. Correction rows cannot be edited or deleted through the normal data context. The business audit trail also records the correction.
- Reports include completed sessions only. Breaks, absences, and payroll calculations are outside V1.
- A report date range is limited to 366 days, allowing a yearly report.

| Endpoint | Access | Purpose |
| --- | --- | --- |
| `POST /work-sessions/clock-in` | Authenticated user | Open own session. |
| `POST /work-sessions/{id}/clock-out` | Session owner | Close own session. |
| `GET /work-sessions/me` | Authenticated user | List own sessions. |
| `GET /work-sessions` | `work-sessions.view` | List staff sessions. |
| `PUT /work-sessions/{id}` | `work-sessions.manage` | Correct times with reason. |
| `GET /work-sessions/{id}/corrections` | `work-sessions.view` | View correction history. |
| `GET /reports/work-hours?userId=...&fromDate=2026-11-01&toDate=2026-11-30` | `work-sessions.report` | Daily and period totals. |

Correction body:

```json
{
  "clockInAt": "2026-11-14T21:00:00Z",
  "clockOutAt": "2026-11-15T01:30:00Z",
  "reason": "Corrected from attendance record"
}
```

Apply the `Organization_WorkedHours` migration per tenant before using these endpoints. HR access is configured with the existing role and permission endpoints; a separate HR project is not required for this MVP.
