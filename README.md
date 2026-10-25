# 🌱 AgriOps Order Service

Order Service is the orders microservice of the **AgricultureOperations (AgriOps)** platform. It manages sales orders: lines with server-calculated totals, a status workflow (Pending → Confirmed → Shipped → Delivered, or Cancelled), soft delete, configurable order numbers (`ORD-2026-000001`) and per-permission authorization. The API is `/api/v2/orders`; `/api/v1/orders` is kept read-only for older frontends.

It is built with **ASP.NET Core Web API on .NET 9** and **PostgreSQL** (EF Core + Npgsql), and follows a **Hexagonal Architecture (Ports & Adapters)**. That keeps the domain logic independent of frameworks, the database and the HTTP layer.

---

## 🌐 Place in the AgriOps Platform

AgriOps is made of four independent services, each in its own repository with its own CI/CD:

| Service | Stack | Role |
|---|---|---|
| [auth-service](https://github.com/AgricultureOperations/auth-service) | Express 5 + TypeScript + SQLite | Users, login, JWT issuance, RBAC, customers (users with the `customer` role) |
| **order-service** (this repo) | ASP.NET Core (.NET 9) + PostgreSQL | Sales orders: lines, status workflow, soft delete, order numbering |
| product-service | NestJS 11 + Prisma + PostgreSQL | Product catalog |
| [agriops-web](https://github.com/AgricultureOperations/frontend) | React 18 + Vite | Back-office SPA for every backend |

```
frontend ──(login, users, customers)──▶ auth-service    ──issues JWT──┐
    │                                                                 │ shared secret/issuer/audience
    ├──(Bearer JWT, orders)───────────▶ order-service   ◀─validates───┤
    └──(Bearer JWT, products)─────────▶ product-service ◀─validates───┘
```

The order form combines data from all three backends in the browser: it picks the customer from auth-service's `/api/v1/customers` and the products from product-service, then sends order-service their ids together with a snapshot of their data.

- **Tokens are issued by auth-service, not here.** This service validates HS256 tokens against `Jwt:Secret`, `Jwt:Issuer` (`auth-service`) and `Jwt:Audience` (`orders-api`). These values must match auth-service's `JWT_SECRET`, `JWT_ISSUER` and `JWT_AUDIENCE`. If any of them differ, every request returns 401.
- The backends never call each other. The browser is the only client.
- This service owns the `Orders`, `OrderLines`, `OrderStatus`, `Settings`, `OrderNumberCounters` and `IdempotencyKeys` tables exclusively. Each order references its **customer** by auth-service id (a user with the `customer` role), with no foreign key or existence check, and keeps a snapshot of the customer's data at order time. Product data is a snapshot too. Orders written before v2 have no customer id.
- Authorization reads the JWT `permissions` claim: `orders:view|create|edit|delete` and `settings:view|edit` (403 without it).

---

## 🛠 Tech Stack

- C#, .NET 9, ASP.NET Core Web API
- Entity Framework Core 9 + Npgsql (PostgreSQL)
- JWT Bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- xUnit + Moq (unit), WebApplicationFactory + Testcontainers PostgreSQL (integration)
- Docker (multi-stage), GitHub Actions, Docker Hub, Render

---

## 🧱 Architecture

Dependencies point inward to `Domain`, and `Domain` references nothing. EF Core, ASP.NET and other framework types stay out of `Domain` and `Application`.

```
        ┌───────────────┐
        │      Api      │  controllers, JWT, CORS, DI (Program.cs)
        └──────┬────────┘
               │ uses
               ▼
        ┌───────────────┐
        │  Application  │  use cases (inbound ports), DTOs
        └──────┬────────┘
               │ uses
               ▼
        ┌───────────────┐
        │    Domain     │  entities, driven ports
        └───────────────┘
               ▲
               │ implements ports
        ┌───────────────┐
        │ Infrastructure│  EF Core adapter, DbContext, migrations
        └───────────────┘
```

Project references: `Application → Domain`; `Infrastructure → Domain, Application`; `Api → Infrastructure, Domain`.

### Active ports

| Kind | Port | Adapter |
|---|---|---|
| Driven (outbound) | `Domain/Ports/Driven/IOrderPersistencePort` | `Infrastructure/Adapters/Driven/OrderPersistenceAdapter` (EF Core → PostgreSQL) |
| Driven (outbound) | `IOrderNumberGeneratorPort` | `OrderNumberGeneratorAdapter` (prefix setting + `OrderNumberCounters` per prefix and year) |
| Driven (outbound) | `IUnitOfWorkPort` | `UnitOfWorkAdapter` (one transaction for numbering + insert) |
| Driven (outbound) | `ISettingsPersistencePort` | `SettingsPersistenceAdapter` |
| Driving (inbound) | `Application/UseCases/OrderUseCases.Interfaces.cs` (create, get, list, update, change status, soft delete, statuses, settings) | `Api/Controllers/{Orders,LegacyOrders,OrderSettings,OrderStatuses}Controller` (HTTP) |

- There is no `Ports/Driving` folder yet. The use case interfaces serve as the inbound ports.
- Each use case is one interface plus one class with a single `Execute(...)` method.
- `src/Api/Program.cs` is the **only composition root**. Every port → adapter and use case binding is registered there with `AddScoped`.
- `Order` is the aggregate: private setters, and every change goes through a method (`UpdateDetails`, `Confirm`, `Ship`, `Deliver`, `Cancel`, `SoftDelete`, `ApplyPaymentBalance`) that enforces the invariants. Totals are always recalculated from the lines (cents, half away from zero).

### Request flow (create order)

```
Client ─▶ OrderController (Api) ─▶ CreateOrderUseCase (Application) ─▶ Order (Domain)
       ─▶ IOrderPersistencePort (port) ─▶ OrderPersistenceAdapter (Infrastructure) ─▶ PostgreSQL
```

---

## 📁 Project Structure

```bash
order-service/
├── order-service.sln             # src projects + both test projects
├── Dockerfile
├── .github/workflows/ci-cd.yml
├── src/
│   ├── Api/
│   │   ├── Controllers/          # Orders (v2), LegacyOrders (v1 GET), OrderSettings, OrderStatuses
│   │   ├── Authorization/        # permission policies, 403 { message } handler, sub → user id
│   │   ├── Middleware/           # exceptions → { message } + status
│   │   └── Program.cs            # JWT, CORS, UseUrls, DI, migrations on startup
│   ├── Application/
│   │   ├── DTOs/                 # request + response DTOs and mappers
│   │   └── UseCases/             # I*UseCase + implementations
│   ├── Domain/
│   │   ├── Entities/             # Order, OrderLine, OrderStatus, PaymentStatus, Setting, value objects
│   │   ├── Exceptions/           # validation (400), conflict (409), concurrency (409)
│   │   └── Ports/Driven/         # persistence, order numbers, settings
│   └── Infrastructure/
│       ├── Adapters/Driven/      # persistence, order number generator, settings
│       ├── Persistence/          # OrderDbContext (seeds, counters, xmin, soft-delete filter)
│       └── Migrations/           # EF Core migrations (Npgsql-specific)
└── test/
    ├── Application/              # domain + use case unit tests (xUnit + Moq)
    └── Api/                      # API integration tests (Testcontainers PostgreSQL)
```

---

## 🔑 API

All endpoints require `Authorization: Bearer <jwt>` issued by auth-service, plus the permission in the table. JSON is camelCase on the wire. Errors are `{ "message": "..." }` with 400 (validation), 401 (no/invalid token), 403 (missing permission), 404, 409 (invalid transition or stale `version`) or 500.

| Method | Path | Permission | Success |
|---|---|---|---|
| `GET` | `/api/v2/orders?page&pageSize&search&statusId&paymentStatus&from&to&sort&order` | `orders:view` | 200 `{ data, meta: { page, pageSize, total, totalPages } }` |
| `GET` | `/api/v2/orders/{id}` | `orders:view` | 200 order with lines / 404 |
| `POST` | `/api/v2/orders` (optional `Idempotency-Key` header) | `orders:create` | 201 + `Location` |
| `PUT` | `/api/v2/orders/{id}` (Pending only, body includes `version`) | `orders:edit` | 200 |
| `POST` | `/api/v2/orders/{id}/confirm`, `/ship`, `/deliver` (`{ version }`), `/cancel` (`{ version, reason }`) | `orders:edit` | 200 |
| `DELETE` | `/api/v2/orders/{id}` (soft delete; Pending or Cancelled without payments) | `orders:delete` | 204 |
| `GET` | `/api/v1/order-settings` · `PUT` `{ orderNumberPrefix }` | `settings:view` · `settings:edit` | 200 `{ orderNumberPrefix, nextNumber, updatedAt, updatedBy }` |
| `GET` | `/api/v1/order-statuses` | `orders:view` | 200 |
| `GET` | `/api/v1/orders`, `/api/v1/orders/{id}` (**deprecated**, legacy shape, no writes) | `orders:view` | 200 |

Create request (the server calculates every amount; a client `total` is ignored):

```json
{
  "customer": { "id": "auth-service customer uuid", "name": "Hacienda La Esperanza", "taxId": "0991234567001", "email": "compras@esperanza.ec", "phone": null },
  "currency": "USD",
  "notes": "Entregar en bodega 2",
  "lines": [
    { "productId": "uuid", "sku": "FERT-UREA-50KG", "productName": "Urea 46% N", "unit": "bag",
      "quantity": 3, "unitPrice": 25.50, "discountAmount": 0, "taxRate": 0.15 }
  ]
}
```

The response adds `id`, `orderNumber`, per-line `lineGross/lineSubtotal/lineTax/lineTotal`, `subtotal`, `discountTotal`, `taxTotal`, `total`, `amountPaid`, `balanceDue`, `paymentStatus`, `statusId` + `status`, the timeline (`confirmedAt`, `shippedAt`, `deliveredAt`, `cancelledAt`, `cancelReason`), the audit fields (`createdBy`, `updatedBy`, from the token's `sub`) and `version`.

**Statuses** are seeded reference data: `1 Pending`, `2 Paid` (legacy, no longer reachable), `3 Shipped`, `4 Delivered`, `5 Cancelled`, `6 Confirmed`. Only append new ids. **Payment state** (`Unpaid`, `PartiallyPaid`, `Paid`, `Refunded`) is a separate field, reserved for the future payment-service.

**Order numbers** are `{prefix}-{yyyy}-{000000}`. The prefix is a database setting (`ORD` by default, editable through `/api/v1/order-settings`). The counter lives in `OrderNumberCounters`, keyed by prefix and year: it restarts at 000001 every year and for every new prefix. It's taken inside the same transaction as the insert, so numbers are unique and gapless, even with concurrent requests.

---

## 🗄 Database & Migrations

- **PostgreSQL** through `UseNpgsql(ConnectionStrings:DefaultConnection)`. The `Microsoft.EntityFrameworkCore.Sqlite` package reference and any `orders.db` file are leftovers. Don't use them.
- `db.Database.Migrate()` runs on **every startup**, so every deploy applies pending migrations before serving traffic. An unreachable database or a failing migration stops the service from starting.
- Create migrations only with the tooling:

  ```bash
  dotnet ef migrations add <PascalCaseName> --project src/Infrastructure --startup-project src/Api
  ```

  Commit the migration, its `.Designer.cs` and the updated `OrderDbContextModelSnapshot.cs` together with the entity or `OrderDbContext` change.
- **Never edit or delete a migration that has been pushed to `main`.** Fix mistakes with a new migration.
- Migrations must be backward compatible with the running code (expand → migrate → contract). Add nullable or defaulted columns first, and drop old ones in a later release. A rename is add + copy + drop across releases. That's why the legacy column `Orders.total` keeps its name and type.
- `OrderMaintainerV2` adds lines, settings, idempotency keys, soft delete, audit and payment-copy columns, and backfills existing rows (order numbers oldest first; `Paid` → `Confirmed` + `PaymentStatus = Paid`). `test/Api/MigrationTests.cs` runs it against a pre-v2 database.

---

## ⚙️ Getting Started

### 1. Clone and restore

```bash
git clone https://github.com/AgricultureOperations/order-service
cd order-service
dotnet restore
```

### 2. Configure

`appsettings.*` files are gitignored, and there is no `.env.example`. Provide these keys through `src/Api/appsettings.Development.json` or environment variables (`__` maps to `:`):

| Config key | Env var | Description |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Npgsql connection string, e.g. `Host=localhost;Port=5432;Database=orders;Username=...;Password=...` |
| `ConnectionStrings:ServerPort` | `ConnectionStrings__ServerPort` | Listen port (8080 locally). Applied via `UseUrls("http://0.0.0.0:<port>")`, which overrides `launchSettings.json` and `ASPNETCORE_HTTP_PORTS` |
| `ConnectionStrings:FrontendHost` | `ConnectionStrings__FrontendHost` | The only allowed CORS origin: `http://localhost:5173` locally, `https://agricultureops.netlify.app` on Render |
| `Jwt:Secret` | `Jwt__Secret` | Same value as auth-service's `JWT_SECRET` (at least 32 bytes) |
| `Jwt:Issuer` | `Jwt__Issuer` | `auth-service` |
| `Jwt:Audience` | `Jwt__Audience` | `orders-api` |

Startup throws if any `Jwt:*` key is missing. A missing `ServerPort` produces an invalid listen URL.

### 3. Run

```bash
dotnet build
dotnet run --project src/Api      # needs a reachable PostgreSQL; applies migrations first
```

The API is served on `http://localhost:8080`.

---

## 🧪 Testing

```bash
dotnet test                                          # both projects (CI runs the same)
dotnet test test/Application/Application.Tests.csproj
dotnet test test/Api/Api.Tests.csproj                # needs Docker (Testcontainers starts postgres:15-alpine)
```

- `test/Application`: domain rules (totals, rounding, transitions, soft delete, payment balance) and use cases with a mocked port. No database needed.
- `test/Api`: the real API through `WebApplicationFactory` against a throwaway PostgreSQL. It covers 401/403 per permission, the full lifecycle, 409s, idempotency and concurrent numbering, soft delete, list filters, the legacy v1 shape, and the migration backfill.

---

## 🐳 Docker

```bash
docker build -t order-service .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=orders;Username=...;Password=..." \
  -e ConnectionStrings__ServerPort=8080 \
  -e ConnectionStrings__FrontendHost=http://localhost:5173 \
  -e Jwt__Secret=... -e Jwt__Issuer=auth-service -e Jwt__Audience=orders-api \
  order-service
```

- Multi-stage build: `dotnet/sdk:9.0` publishes `src/Api` (tests are excluded), and `dotnet/aspnet:9.0` runs it as the non-root `appuser`.
- The container listens on `ConnectionStrings__ServerPort` (`EXPOSE 8080` documents the usual value). Map the host port to `ServerPort`.
- From inside a container, reach a Postgres on your machine with `host.docker.internal`, not `localhost`.
- `.dockerignore` excludes `appsettings*.json`, `.env*` and `test/`, and the build restores only `src/Api`.

### Full stack with docker compose

The workspace root's `docker-compose.yml` runs postgres (with a health check that order-service waits for), auth-service, order-service and the frontend. It builds the connection string and the `Jwt__*` values from the root `.env`.

```bash
# from the workspace root
docker compose up -d --build order-service
docker compose logs -f order-service
```

To check the JWT link end to end, log in through auth-service, then call `GET /api/v2/orders` with the token. A 401 means the JWT settings differ between the two services, and a 403 means the token has no `orders:view` permission.

---

## 🚀 CI/CD

`.github/workflows/ci-cd.yml` runs on pushes to `main` that touch `src/`, `test/`, the `.sln`, `Dockerfile`, `.dockerignore` or workflows:

1. **build-test:** `dotnet restore`, `build` and `test` on .NET 9 (unit + integration; the runner's Docker hosts the Testcontainers database).
2. **docker:** build and push `<DOCKER_USERNAME>/order-service:{sha,latest}` to Docker Hub.
3. **deploy:** trigger the Render deploy hook. Migrations run when the new container starts.

---

## ⚠️ Known Issues

- Line prices are a snapshot sent by the browser from product-service. order-service validates them but can't check them against the catalog (services don't call each other).
- The customer id is not verified against auth-service either. A customer deleted later remains on its orders as a snapshot.
- The deprecated `GET /api/v1/orders` returns every order unpaginated. Remove it once no deployed frontend reads it.

---

## 📦 Deploying v2

1. **auth-service first.** It must issue the `orders:*`, `customers:*` and `settings:*` permissions and serve `/api/v1/customers`. Without them every order request is 403, and the order form can't pick a customer.
2. **order-service.** On startup the `OrderMaintainerV2` migration runs once. It numbers existing orders per year, oldest first (`ORD-2025-000001`, `ORD-2026-000001`, …), seeds the counters and turns `Paid` orders into `Confirmed` + `PaymentStatus = Paid`. The old `GET /api/v1/orders` keeps working for the frontend that is still deployed.
3. **frontend**, which switches to `/api/v2/orders`.

---

## 🔮 Future Improvements

- Payments in a separate payment-service, applied through `Order.ApplyPaymentBalance` from a broker event (see the workspace roadmap)
- A customers maintainer screen in the frontend (auth-service already has `/api/v1/customers`)
- OpenAPI / Swagger documentation

---

## 🤝 Contributing

- Keep `Domain` free of framework types and register every new port, adapter and use case in `Program.cs`.
- A breaking change needs a new route version served alongside the old one (as `/api/v2/orders` did), because the frontend (Netlify) and this service (Render) deploy independently.
- Changing a response or request DTO requires updating the frontend mirror in `features/orders/interfaces/` in the agriops-web repo, with coordinated deploys.

---

## 📌 Author

**Edward Cruz**
Full Stack Developer | ASP.NET Core | .NET | Microservices
