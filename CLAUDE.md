# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Order Service is an ASP.NET Core Web API (.NET 9) microservice of the AgricultureOperations platform. It provides JWT-protected CRUD for orders at `api/v1/orders`. Tokens are issued by a separate auth service (issuer `auth-service`, audience `orders-api`).

## Architectural Pattern

**Hexagonal Architecture (Ports & Adapters)** keeps domain logic, application use cases, infrastructure concerns, and API delivery mechanisms separate. Dependencies point inward to `Domain`, and `Domain` references nothing. Keep EF Core, ASP.NET, and other framework types out of `Domain` and `Application`.

Project references: `Application → Domain`; `Infrastructure → Domain, Application`; `Api → Infrastructure, Domain` (it reaches `Application` transitively).

### Active ports

| Kind | Port | Adapter |
|---|---|---|
| Driven (outbound) | `Domain/Ports/Driven/IOrderPersistencePort` | `Infrastructure/Adapters/Driven/OrderPersistenceAdapter` (EF Core → PostgreSQL) |
| Driving (inbound) | `Application/UseCases/I{Create,GetOrderById,GetOrders,Update,Delete}OrderUseCase` | `Api/Controllers/OrderController` (HTTP) |

There is no `Ports/Driving` folder yet. The use case interfaces serve as the inbound ports. `src/Api/Program.cs` is the only composition root, where every port→adapter binding is registered with `AddScoped`. Any new port, adapter, or use case must be registered there.

## Key Folder Responsibilities

- `src/Domain/Entities/`: `Order` and `OrderStatus`. Entities use private setters and change state only through methods such as `UpdateTotal` and `UpdateStatus`, which enforce invariants (for example, the total can't be negative).
- `src/Domain/Ports/Driven/`: outbound interfaces the domain needs (currently only persistence).
- `src/Application/UseCases/`: one interface and one class per operation, each with a single `Execute(...)` method. They orchestrate entities and ports.
- `src/Application/DTOs/`: request contracts (`CreateOrderRequest`, `UpdateOrderRequest`) at the application boundary.
- `src/Infrastructure/Adapters/Driven/`: implementations of the driven ports.
- `src/Infrastructure/Persistence/`: `OrderDbContext`, which seeds the `OrderStatus` lookup (1 = Pending … 5 = Cancelled). New orders default to `StatusId = 1`.
- `src/Infrastructure/Migrations/`: EF Core migrations. They are specific to PostgreSQL (Npgsql).
- `src/Api/`: controllers, JWT auth, CORS, and DI wiring (`Program.cs`).
- `test/Application/`: xUnit + Moq unit tests for use cases, which mock `IOrderPersistencePort`.

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run --project src/Api        # applies migrations on startup; needs a reachable PostgreSQL DB
docker build -t order-service .     # multi-stage build; copies only src/ (tests excluded)
```

EF Core migrations (the DbContext is in Infrastructure; the startup config is in Api):
```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
```

## Test

The test project is **not** in `order-service.sln`, so point commands at it directly:
```bash
dotnet test test/Application/Application.Tests.csproj
dotnet test test/Application/Application.Tests.csproj --filter "FullyQualifiedName~CreateOrderUseCasesTest"
```
Note: the test project targets `net10.0`, while the rest of the code and CI use .NET 9.

## Configuration

- `appsettings.*` is gitignored, so local config files are not in the repo. Required keys: `ConnectionStrings:DefaultConnection`, `ConnectionStrings:ServerPort`, `ConnectionStrings:FrontendHost` (CORS origin), and `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`. Startup throws if any `Jwt:*` key is missing. In CI and deployed environments these come from env vars (`ConnectionStrings__ServerPort`, `Jwt__Secret`, …).
- The listen port comes from `ConnectionStrings:ServerPort` through `UseUrls` (8080 in dev). This overrides `launchSettings.json`, and the Dockerfile's `EXPOSE 80` doesn't match it.
- The database is PostgreSQL. The README's mentions of SQLite are outdated, and the leftover SQLite package reference and `*.db` files can be ignored.

## CI/CD

`.github/workflows/ci-cd.yml` runs on pushes to `main`: build and test, then push `order-service:{sha,latest}` to Docker Hub, then deploy through a Render deploy hook. Because the test project is outside the sln, the CI `dotnet test` step currently runs no tests.
