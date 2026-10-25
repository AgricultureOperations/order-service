# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Order Service is an ASP.NET Core Web API (.NET 9) microservice of the AgricultureOperations platform. It owns sales orders: order lines with server-calculated totals, a status workflow, soft delete, configurable order numbers and per-permission authorization. The maintainer API is `api/v2/orders`. The deprecated `api/v1/orders` is kept read-only for frontends deployed before v2. Tokens are issued by auth-service (issuer `auth-service`, audience `orders-api`).

## Architectural Pattern

**Hexagonal Architecture (Ports & Adapters)** keeps domain logic, application use cases, infrastructure concerns, and API delivery mechanisms separate. Dependencies point inward to `Domain`, and `Domain` references nothing. Keep EF Core, ASP.NET, and other framework types out of `Domain` and `Application`. The clock is the BCL `TimeProvider`, injected as a singleton, so tests can pin the time.

Project references: `Application → Domain`; `Infrastructure → Domain, Application`; `Api → Infrastructure, Domain` (it reaches `Application` transitively).

### Active ports

| Kind | Port | Adapter |
|---|---|---|
| Driven | `Domain/Ports/Driven/IOrderPersistencePort` (create with idempotency key, get, paged `List(OrderListQuery)`, legacy `GetAll`, `UpdateOrder(order, expectedVersion)`, statuses) | `Infrastructure/Adapters/Driven/OrderPersistenceAdapter` (EF Core → PostgreSQL) |
| Driven | `IOrderNumberGeneratorPort.Next(year)` / `Peek(prefix, year)` | `OrderNumberGeneratorAdapter`: `orders.number.prefix` setting + an upsert on `OrderNumberCounters` (prefix, year) |
| Driven | `IUnitOfWorkPort.InTransaction(work)` | `UnitOfWorkAdapter`: one EF transaction on the request's scoped `OrderDbContext` (nested calls join it; rollback clears the change tracker) |
| Driven | `ISettingsPersistencePort` | `SettingsPersistenceAdapter` (`Settings` table) |
| Driving | `Application/UseCases/OrderUseCases.Interfaces.cs`: `ICreateOrderUseCase`, `IGetOrderByIdUseCase`, `IListOrdersUseCase`, `IGetOrdersUseCase` (legacy), `IUpdateOrderUseCase`, `IChangeOrderStatusUseCase` (`OrderTransition` Confirm/Ship/Deliver/Cancel), `IDeleteOrderUseCase` (soft), `IGetOrderStatusesUseCase`, `IGet/IUpdateOrderSettingsUseCase` | `Api/Controllers/OrdersController` (v2), `LegacyOrdersController` (v1, GET only), `OrderSettingsController`, `OrderStatusesController` |

`src/Api/Program.cs` is the only composition root. Every port→adapter binding and use case is registered there with `AddScoped`, so register any new one there too.

## Domain Rules (`src/Domain/Entities/`)

- **`Order` is the aggregate.** Every change goes through a method: `Create`, `UpdateDetails`, `Confirm`, `Ship`, `Deliver`, `Cancel(reason)`, `SoftDelete`, `ApplyPaymentBalance`. Setters are private. A broken rule throws `DomainValidationException` (400) or `DomainConflictException` (409).
- **Totals are calculated, never accepted.** Each `OrderLine` computes `LineGross = round(qty × price)`, `LineSubtotal = gross − discount` and `LineTax = round(subtotal × taxRate)`. The order sums them. Rounding is to cents, half away from zero (`Money.Round`). Quantity allows 3 decimals, price and discount 2, and `taxRate` is a fraction from 0 to 1. The frontend's `utils/order-money.ts` mirrors these rules for its preview.
- **Lines are a product snapshot** (`ProductId`, `Sku`, `ProductName`, `Unit`, price). order-service can't call product-service, so it validates the snapshot but doesn't verify it against the catalog.
- **The customer is an auth-service user with the `customer` role.** The request sends `customer: { id, name, taxId, email, phone }` as the browser read it from auth-service `/api/v1/customers`. The order stores `CustomerId` (required for new orders, checked in `OrderRequestMapper` before a number is taken) plus a snapshot of the other fields (`CustomerName`, `CustomerTaxId`, `CustomerEmail`, `CustomerPhone`). The snapshot keeps the data as it was at order time, and order-service can't verify the id because services don't call each other. `CustomerId` is null only on pre-v2 rows.
- **Status machine** (`OrderStatusIds`, append-only): Pending(1) → Confirmed(6) → Shipped(3) → Delivered(4). Pending or Confirmed → Cancelled(5) requires a reason and `AmountPaid == 0`. Lines can only be edited while Pending, and `Confirm` needs at least one line. **Paid(2) is legacy**: it stays seeded, but nothing transitions to it.
- **Payment state is separate from the order status.** `AmountPaid`, `BalanceDue`, `PaymentStatus` (Unpaid / PartiallyPaid / Paid / Refunded) and `PaymentBalanceVersion` are a copy of a balance that the future payment-service will own. Only `ApplyPaymentBalance(amountPaid, balanceDue, status, version)` changes them, and it ignores a version that isn't newer. No HTTP route sets them.
- **Soft delete.** `SoftDelete` is only allowed for Pending or Cancelled orders without payments. It sets `IsDeleted`/`DeletedAt`/`DeletedBy`, and a global query filter hides the row from every query. Use `IgnoreQueryFilters()` only for audits and tests.
- **Audit.** `CreatedBy`, `UpdatedBy` and `DeletedBy` come from the JWT `sub` claim (`ClaimsPrincipalExtensions.UserId()`), never from a request body.
- **Optimistic concurrency.** `Order.Version` (uint) is PostgreSQL's `xmin`. Clients send back the `version` they read on PUT and on every transition. A mismatch throws `ConcurrencyConflictException` (409).

## Order Numbers

- Format `{prefix}-{yyyy}-{000000}` (`OrderNumberPrefix.Format`). The year is the creation year in UTC, and the counter widens past 6 digits after 999999.
- The prefix lives in the `Settings` table (key `orders.number.prefix`, seeded `ORD` through `HasData`). It's edited through `PUT /api/v1/order-settings` (`settings:edit`) and validated as `^[A-Z][A-Z0-9]{1,9}$` (input is upper-cased). A change applies only to new orders.
- **The counter is the `OrderNumberCounters` table, keyed by (`Prefix`, `Year`)** with `LastValue`. Each prefix restarts at 000001 every year, and going back to an earlier prefix continues its counter for that year. `Next` runs `INSERT … ON CONFLICT (Prefix, Year) DO UPDATE SET LastValue = LastValue + 1 RETURNING LastValue`, which locks the row until the transaction ends.
- **Numbering and the insert share one transaction.** `CreateOrderUseCase` wraps `Next` + `Order.Create` + `CreateOrder` in `IUnitOfWorkPort.InTransaction`, and `Next` throws if no transaction is open. Concurrent creates for the same prefix and year queue on the row lock (also across containers), so numbers are unique and **gapless**: a failed insert, including a lost Idempotency-Key race (`IdempotencyKeyConflictException`, after which the use case reloads the winner's order), rolls the increment back. Validation runs before the transaction, so bad requests never take the lock.
- `Peek(prefix, year)` reads the next number without reserving it. `GET /api/v1/order-settings` returns it as `nextNumber`.
- Use `ToListAsync()`, not `SingleAsync()`, on the upsert's `SqlQuery`: EF would wrap the `INSERT … RETURNING` in a subquery, which PostgreSQL rejects.

## API (all routes require a Bearer JWT; errors are `{ message }`)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/v2/orders?page&pageSize&search&statusId&paymentStatus&from&to&sort&order` | `orders:view` | `{ data: OrderSummaryResponse[], meta: { page, pageSize, total, totalPages } }`. `pageSize` ≤ 100. `search` is an `ILIKE` on number, customer name or tax ID (wildcards escaped). `from` is inclusive and `to` exclusive (ISO instants). `sort` is `createdAt` (default), `orderNumber`, `total` or `customerName`, and `order` is `asc` or `desc` (default `desc`) |
| `GET /api/v2/orders/{id}` | `orders:view` | `OrderResponse` with lines, totals and timeline timestamps |
| `POST /api/v2/orders` | `orders:create` | 201 + `Location`. Optional `Idempotency-Key` header (≤ 100 chars, stored in `IdempotencyKeys`): a repeat, even a concurrent one, returns the same order |
| `PUT /api/v2/orders/{id}` | `orders:edit` | Replaces customer (`{ id, name, taxId, email, phone }`), notes and lines. Pending only, and needs `version` |
| `POST /api/v2/orders/{id}/confirm`, `/ship`, `/deliver`, `/cancel` | `orders:edit` | Body `{ version }` (`cancel` also needs `reason`). Returns the order |
| `DELETE /api/v2/orders/{id}` | `orders:delete` | Soft delete, 204 |
| `GET /api/v1/orders`, `/api/v1/orders/{id}` | `orders:view` | **Deprecated**, legacy shape `{ id, customerId, total, createdAt, statusId, status }` for frontends deployed before v2. The v1 writes were removed. Delete these routes once no deployed frontend reads them |
| `GET`, `PUT /api/v1/order-settings` | `settings:view` / `settings:edit` (admin only by default in auth-service) | `{ orderNumberPrefix, nextNumber, updatedAt, updatedBy }` |
| `GET /api/v1/order-statuses` | `orders:view` | Status lookup |

- **Authorization (RBAC Phase 2).** One policy per permission code (`Api/Authorization/Permissions.cs`: `orders:*` and `settings:view|edit`), reading the JWT `permissions` claim. `MapInboundClaims = false` keeps `sub` and `permissions` as issued. A token without the code gets **403** `{ message }` (`JsonAuthorizationResultHandler`). A missing, invalid or expired token gets **401** `{ message: "Unauthorized" }` plus `WWW-Authenticate: Bearer`. Never return 401 for "not allowed": the frontend logs out on any 401.
- **Errors.** `ErrorHandlingMiddleware` maps `KeyNotFoundException` → 404, `DomainConflictException` → 409, `DomainValidationException`/`ArgumentException` → 400, and anything else → 500 "Internal Server Error" (logged). Model-binding errors are 400 `{ message }` (`InvalidModelStateResponseFactory`). CORS runs before the middleware, so error responses keep their CORS headers.
- **Responses are DTOs** (`Application/DTOs/OrderResponses.cs`, mapped by `OrderResponseMapper`). Never serialize entities. The frontend mirrors them in `features/orders/interfaces/`, so change both together.

## Key Folder Responsibilities

- `src/Domain/Entities/`: `Order`, `OrderLine`, `OrderStatus` + `OrderStatusIds`/`OrderStatusNames`, `PaymentStatus`, `CustomerSnapshot`, `Setting` + `SettingKeys`, `OrderNumberPrefix`, `Money`.
- `src/Domain/Exceptions/`: `DomainValidationException`, `DomainConflictException`, `ConcurrencyConflictException`, `IdempotencyKeyConflictException`.
- `src/Domain/Ports/Driven/`: the driven ports (persistence, order numbers, settings, unit of work) plus `OrderListQuery`/`PagedResult<T>`.
- `src/Application/DTOs/`: request contracts (`OrderRequests.cs`), response DTOs and mappers.
- `src/Application/UseCases/`: one class per operation, each with a single `Execute(...)` method.
- `src/Infrastructure/Persistence/`: `OrderDbContext` (fluent config, `OrderStatus` and `Settings` seeds, owned `OrderLines`, `xmin`, soft-delete filter), `OrderNumberCounter` and `IdempotencyRecord`.
- `src/Infrastructure/Migrations/`: EF Core migrations, PostgreSQL-specific. `OrderMaintainerV2` is additive and backfills legacy rows: numbers per year oldest first (`ORD-2025-000001`, `ORD-2026-000001`, …), the matching `OrderNumberCounters` rows, `Subtotal`/`BalanceDue` from `total`, and Paid(2) → Confirmed(6) with `PaymentStatus = Paid`.
- `src/Api/`: controllers, `Authorization/`, `Middleware/`, `Program.cs`.

## Database Notes

- The legacy `Orders.total` column keeps its name and unbounded `numeric` type (`HasColumnName("total")`). Renaming or narrowing it would break the old container during a deploy. New money columns are `numeric(18,2)`, quantity `numeric(18,3)` and tax rate `numeric(5,4)`.
- The `OrderStatus` table keeps its singular name (`ToTable("OrderStatus")`), even though the DbSet is `OrderStatuses`.
- `migrations remove` connects to the database. Without one, delete the new migration files and `git checkout` the snapshot instead.
- Before committing a migration, review `Up()` and `Down()` (see `../.claude/rules/architecture.md`). Data fixes in `Down()` must run before a `DeleteData` on `OrderStatus`, because its FK cascades to `Orders`.

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run --project src/Api        # applies migrations on startup; needs a reachable PostgreSQL DB
docker build -t order-service .     # restores/publishes src/Api only; tests and appsettings are excluded
```

EF Core migrations (the DbContext is in Infrastructure; the startup config is in Api):
```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
```

## Test

Both test projects are in `order-service.sln` and target net9.0, so `dotnet test` runs everything (CI too):
```bash
dotnet test                                                       # all
dotnet test test/Application/Application.Tests.csproj             # domain + use cases (xUnit + Moq, no DB)
dotnet test test/Api/Api.Tests.csproj                             # API + EF + migrations (needs Docker)
dotnet test test/Api/Api.Tests.csproj --filter "FullyQualifiedName~MigrationTests"
```
- `test/Application/`: `Domain/OrderTests.cs` covers totals, rounding, every allowed and forbidden transition, soft delete, payment balance versioning and value objects. `UseCases/OrderUseCaseTests.cs` covers the use cases against a mocked port, with `Support/TestData.cs` (`FixedClock`).
- `test/Api/` boots the real `Program` through `WebApplicationFactory` against a throwaway `postgres:15-alpine` container started by **Testcontainers**. Configuration comes from env vars set in `Support/ApiFactory.cs`, because `Program.cs` reads config before the factory could inject it. Tokens are minted with the test secret (`ApiFactory.Token(permissions)`). The classes share one database in the `"api"` collection and run serially, so tests that change shared state (the prefix) restore it. They cover 401 vs 403 per permission (`settings:*` included), the lifecycle, 409s, idempotency (including concurrent requests), gapless concurrent numbering, a failed create not consuming a number, per-prefix counters, the year rollover (`YearRolloverTests` swaps `TimeProvider` with `WithWebHostBuilder`), soft delete, list filters, the legacy v1 shape, and the migration backfill on a pre-v2 database.

## Configuration

- `appsettings.*` is gitignored, so local config files are not in the repo. Required keys: `ConnectionStrings:DefaultConnection`, `ConnectionStrings:ServerPort`, `ConnectionStrings:FrontendHost` (CORS origin), and `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`. Startup throws if any `Jwt:*` key is missing. In CI and deployed environments these come from env vars (`ConnectionStrings__ServerPort`, `Jwt__Secret`, …).
- The listen port comes from `ConnectionStrings:ServerPort` through `UseUrls` (8080 in dev and compose). The Dockerfile's `EXPOSE 8080` is documentation only.
- The database is PostgreSQL. The leftover SQLite package reference and `*.db` files can be ignored.

## CI/CD

`.github/workflows/ci-cd.yml` runs on pushes to `main`: build and test (both test projects; the integration tests need the runner's Docker for Testcontainers), then push `order-service:{sha,latest}` to Docker Hub, then deploy through a Render deploy hook. Migrations run when the new container starts.

**Deploy order for v2:** deploy order-service first. It still serves `GET /api/v1/orders` to the old frontend. Then deploy the frontend, which calls `/api/v2/orders`. auth-service must already issue the `permissions` claim, including `customers:*` and `settings:*` (migration `003_customer_fields` + seed `grants:customers-v1`), and serve `/api/v1/customers`. Otherwise every order request is 403 and the order form can't pick a customer.
