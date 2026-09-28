# 🌱 AgriOps Order Service

Order Service is the orders microservice of the **AgricultureOperations (AgriOps)** platform. It exposes JWT-protected CRUD for orders at `/api/v1/orders`.

It is built with **ASP.NET Core Web API on .NET 9** and **PostgreSQL** (EF Core + Npgsql), and follows a **Hexagonal Architecture (Ports & Adapters)**. That keeps the domain logic independent of frameworks, the database and the HTTP layer.

---

## 🌐 Place in the AgriOps Platform

AgriOps is made of three independent services, each in its own repository with its own CI/CD:

| Service | Stack | Role |
|---|---|---|
| [auth-service](https://github.com/AgricultureOperations/auth-service) | Express 5 + TypeScript + SQLite | Users, login, JWT issuance |
| **order-service** (this repo) | ASP.NET Core (.NET 9) + PostgreSQL | JWT-protected order CRUD |
| [agriops-web](https://github.com/AgricultureOperations/frontend) | React 18 + Vite | Back-office SPA for both backends |

```
frontend ──(login/register, users)──▶ auth-service   ──issues JWT──┐
    │                                                              │ shared secret/issuer/audience
    └──(Bearer JWT, orders)──────────▶ order-service ◀─validates───┘
```

- **Tokens are issued by auth-service, not here.** This service validates HS256 tokens against `Jwt:Secret`, `Jwt:Issuer` (`auth-service`) and `Jwt:Audience` (`orders-api`). These values must match auth-service's `JWT_SECRET`, `JWT_ISSUER` and `JWT_AUDIENCE`. If any of them differ, every request returns 401.
- The backends never call each other. The browser is the only client.
- This service owns the `Orders` and `OrderStatus` tables exclusively. `Order.customerId` is an auth-service `User.id` (UUID), referenced by ID only: no foreign key and no existence check.

---

## 🛠 Tech Stack

- C#, .NET 9, ASP.NET Core Web API
- Entity Framework Core 9 + Npgsql (PostgreSQL)
- JWT Bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- xUnit + Moq for tests
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
| Driving (inbound) | `Application/UseCases/I{Create,GetOrderById,GetOrders,Update,Delete}OrderUseCase` | `Api/Controllers/OrderController` (HTTP) |

- There is no `Ports/Driving` folder yet. The use case interfaces serve as the inbound ports.
- Each use case is one interface plus one class with a single `Execute(...)` method.
- `src/Api/Program.cs` is the **only composition root**. Every port → adapter and use case binding is registered there with `AddScoped`.
- Entities have private setters and change state only through methods such as `UpdateTotal` and `UpdateStatus`, which enforce invariants (for example, the total can't be negative).

### Request flow (create order)

```
Client ─▶ OrderController (Api) ─▶ CreateOrderUseCase (Application) ─▶ Order (Domain)
       ─▶ IOrderPersistencePort (port) ─▶ OrderPersistenceAdapter (Infrastructure) ─▶ PostgreSQL
```

---

## 📁 Project Structure

```bash
order-service/
├── order-service.sln             # Api, Application, Domain, Infrastructure (not the test project)
├── Dockerfile
├── .github/workflows/ci-cd.yml
├── src/
│   ├── Api/
│   │   ├── Controllers/OrderController.cs
│   │   └── Program.cs            # JWT, CORS, UseUrls, DI, migrations on startup
│   ├── Application/
│   │   ├── DTOs/                 # CreateOrderRequest, UpdateOrderRequest
│   │   └── UseCases/             # I*UseCase + implementations
│   ├── Domain/
│   │   ├── Entities/             # Order, OrderStatus
│   │   └── Ports/Driven/         # IOrderPersistencePort
│   └── Infrastructure/
│       ├── Adapters/Driven/      # OrderPersistenceAdapter
│       ├── Persistence/          # OrderDbContext (seeds OrderStatus)
│       └── Migrations/           # EF Core migrations (Npgsql-specific)
└── test/
    └── Application/              # xUnit + Moq use case tests
```

---

## 🔑 API

All endpoints require `Authorization: Bearer <jwt>` issued by auth-service. JSON is camelCase on the wire.

| Method | Path | Body | Success | Response |
|---|---|---|---|---|
| `POST` | `/api/v1/orders` | `{ customerId, total }` | 200 | `Order` |
| `GET` | `/api/v1/orders` | — | 200 | `Order[]` |
| `GET` | `/api/v1/orders/{id}` | — | 200 / 404 (empty body) | `Order` |
| `PUT` | `/api/v1/orders/{id}` | `{ customerId, total }` | 200 | empty |
| `DELETE` | `/api/v1/orders/{id}` | — | 200 | empty |

`Order` response shape (the domain entity, serialized directly):

```json
{
  "id": "uuid",
  "customerId": "uuid",
  "total": 100.0,
  "createdAt": "2026-04-07T20:21:46Z",
  "statusId": 1,
  "status": { "id": 1, "name": "Pending" }
}
```

Order statuses are seeded reference data: `1 Pending`, `2 Paid`, `3 Shipped`, `4 Delivered`, `5 Cancelled`. New orders start at `1`. The frontend mirrors these IDs, so only append new statuses with new IDs. Never renumber or reuse one.

An invalid, expired or missing token returns 401 with an empty body and a `WWW-Authenticate` header. The frontend reacts by clearing the token and redirecting to `/login`.

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
- Migrations must be backward compatible with the running code (expand → migrate → contract). Add nullable or defaulted columns first, and drop old ones in a later release. A rename is add + copy + drop across releases.

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

The test project is **not** in `order-service.sln`, so point commands at it directly:

```bash
dotnet test test/Application/Application.Tests.csproj
dotnet test test/Application/Application.Tests.csproj --filter "FullyQualifiedName~CreateOrderUseCasesTest"
```

- Use case tests mock `IOrderPersistencePort` with Moq. No database is needed.
- The test project targets `net10.0`, while the service and CI use .NET 9, so running the tests needs the .NET 10 SDK.

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
- The container listens on `ConnectionStrings__ServerPort`. **The Dockerfile's `EXPOSE 80` is wrong**, so map the host port to `ServerPort`, not to 80.
- From inside a container, reach a Postgres on your machine with `host.docker.internal`, not `localhost`.
- **Never push a locally built image.** `.dockerignore` doesn't exclude `appsettings*.json` yet, so local builds contain your local config. CI images don't, because those files are absent from the checkout.

### Full stack with docker compose

The workspace root's `docker-compose.yml` runs postgres (with a health check that order-service waits for), auth-service, order-service and the frontend. It builds the connection string and the `Jwt__*` values from the root `.env`.

```bash
# from the workspace root
docker compose up -d --build order-service
docker compose logs -f order-service
```

To check the JWT link end to end, log in through auth-service, then call `GET /api/v1/orders` with the token. A 401 means the JWT settings differ between the two services.

---

## 🚀 CI/CD

`.github/workflows/ci-cd.yml` runs on pushes to `main` that touch `src/`, `test/`, the `.sln`, `Dockerfile`, `.dockerignore` or workflows:

1. **build-test:** `dotnet restore`, `build` and `test` on .NET 9, with config from GitHub secrets. Because the test project is outside the `.sln`, this step currently runs **no tests**.
2. **docker:** build and push `<DOCKER_USERNAME>/order-service:{sha,latest}` to Docker Hub.
3. **deploy:** trigger the Render deploy hook. Migrations run when the new container starts.

---

## ⚠️ Known Issues

- **Missing orders on update or delete return a 500**, not a 404. The use cases throw `KeyNotFoundException`, and there is no exception handling middleware. A negative total on update (`ArgumentException`) is also a 500. Move these to the correct 4xx status with a `{ message }` body.
- **`POST` returns 200**, not 201 with the created resource.
- **The controller returns the domain entity directly.** Add response DTOs in `Application/DTOs/` before exposing new fields, so domain refactors don't break the API.
- **`customerId` is taken from the request body**, not from the token's `id` claim, and there is no ownership check. `PUT` accepts `customerId` but only updates `total`.
- `CreateOrderUseCase` doesn't reject a negative total (only `UpdateTotal` does).
- `Dockerfile` `EXPOSE 80` and the missing `appsettings*` entry in `.dockerignore` (see Docker).
- The CI test step runs no tests (see CI/CD).

---

## 🔮 Future Improvements

- Consistent error handling (`{ message }` bodies, 400/404 instead of 500)
- Response DTOs and validation (FluentValidation)
- Ownership: read the user ID from the JWT `id` claim
- Pagination and filtering for `GET /api/v1/orders`
- OpenAPI / Swagger documentation
- Add the test project to the solution so CI runs it

---

## 🤝 Contributing

- Keep `Domain` free of framework types and register every new port, adapter and use case in `Program.cs`.
- Keep every route under `/api/v1/`. A breaking change needs a new version served alongside v1, because the frontend (Netlify) and this service (Render) deploy independently.
- Changing an entity's serialized shape or a DTO requires updating the frontend mirror in `features/orders/interfaces/` in the agriops-web repo, with coordinated deploys.

---

## 📌 Author

**Edward Cruz**
Full Stack Developer | ASP.NET Core | .NET | Microservices
