# CRM module: implementation guide

## Boundaries and storage

CRM has four projects: `Core` holds `LoyaltyProgram`, `LoyaltyCard`, `LoyaltyReward`, and append-only `LoyaltyTransaction`; `Contracts` holds response DTOs; `UseCases` defines `ILoyaltyService`; `Infrastructure` owns the EF Core service, installer, context, and migration. Its tables and migration history live in the tenant database's `crm` schema. Tenant isolation comes from `ITenantContext.ConnectionString` and the `Crm` module flag.

CRM does not read Sales tables directly. Sales exposes `ILoyaltySalesReader` from `Sales.Contracts`, implemented in Sales Infrastructure. It supplies only customer active state and completed sale/return snapshots. Card `CustomerId` and transaction source IDs are cross-module references without cross-schema foreign keys. Both modules therefore need to be enabled for the CRM routes.

The program row has a unique scope key so there can be only one program per tenant. Card numbers and customer IDs are unique. A card has a SQL row version; point changes and their ledger rows are saved in one EF transaction. Unique `(SourceType, SourceId)` prevents a sale, return, or client-supplied redemption ID from changing points twice. The DbContext rejects editing or deleting ledger entries and hard-deleting CRM records.

## HTTP API

All routes require a tenant and the `Crm` module. The migrator seeds `crm.view`, `crm.manage`, and `crm.redeem` into the permission catalog and local Admin role. Other staff roles need explicit assignment.

| Route | Permission | Purpose |
| --- | --- | --- |
| `GET /crm/loyalty/program` | `crm.view` | Read program or `null` before configuration. |
| `PUT /crm/loyalty/program` | `crm.manage` | Set name, `spendAmountPerPoint`, and active flag; currency is DZD. |
| `POST /crm/loyalty/cards` | `crm.manage` | Enroll an active Sales customer by `customerId`. |
| `GET /crm/loyalty/customers/{customerId}/card` | `crm.view` | Read card and reconcile any unsynchronized sales. |
| `GET /crm/loyalty/cards/{cardId}` | `crm.view` | Read card by ID and reconcile sales. |
| `GET /crm/loyalty/cards/by-number/{cardNumber}` | `crm.view` | Exact scan/number lookup and balance. |
| `PUT /crm/loyalty/cards/{cardId}/active` | `crm.manage` | Activate or deactivate a card. |
| `GET /crm/loyalty/cards/{cardId}/transactions?page=1&pageSize=50` | `crm.view` | Read immutable point history, newest first. |
| `GET /crm/loyalty/rewards?includeInactive=false` | `crm.view` | List rewards. |
| `GET /crm/loyalty/rewards/{id}` | `crm.view` | Read a reward. |
| `POST /crm/loyalty/rewards` | `crm.manage` | Create a reward with `name` and positive `pointsCost`. |
| `PUT /crm/loyalty/rewards/{id}` | `crm.manage` | Change name/cost or active flag. |
| `POST /crm/loyalty/cards/{cardId}/redemptions` | `crm.redeem` | Debit points for `rewardId` using caller-generated `redemptionId`. |
| `POST /crm/loyalty/sales/{saleId}/sync` | `crm.manage` | Reconcile a completed sale and its returns; safe to retry. |

Sale completion and return processing call CRM sync after the Sales transaction commits. If that separate CRM write fails, the sale/return still succeeds, the API logs the failure, and card lookup or explicit sync retries it. CRM read is therefore a reconciliation point. The CRM ledger is atomic with its card balance, but **Sales and CRM are not one transaction**. This is a deliberate consistency limit; a transactional outbox is the future path if immediate, guaranteed accrual becomes required.

The earning calculation is `floor(sale.Total / spendAmountPerPoint)`. The rate is stored on the earning event so a later program change does not alter its return calculation. Returns subtract points based on the remaining sale amount after cumulative refunds. A zero-point sale or return still gets a ledger event to mark its source processed. A sale completed before card enrollment is not back-credited. Customer-facing balances may be negative after a return of points already redeemed.

## Run and verify

The installed module is listed in `SimpliaDz.ModuleCatalog`, mapped by the API, and explicitly enabled by `Crm: true` under the local tenant flags. Run the migrator before starting the updated API:

```powershell
dotnet run --project src/SimpliaDz.DatabaseMigrator -- --tenant demo
$env:Tenancy__ActiveTenantId = "demo"
dotnet run --project src/SimpliaDz.Api
```

Add `--seed-demo` after the tenant arguments to create an example DZD program, one gift reward, and a card for the demo sale customer. The seed is repeatable and does not grant points for sales completed before card enrollment. It also gives the demo cashier view and redemption permissions.

For the SQL integration test, set `SIMPLIADZ_INTEGRATION_CONNECTIONSTRING` to a local SQL Server connection with database creation permission, then run `dotnet test tests/SimpliaDz.Crm.Tests/SimpliaDz.Crm.Tests.csproj -c Release`. It creates and removes its own database and covers enrollment, sale/return idempotency, redemption retries, negative balances, and append-only history.
