# 🌱 AgricultureOperations - Order Service

<p> Order Service is a backend microservice of the <strong>AgricultureOperations platform</strong>, responsible for managing agricultural operation orders.

The service is built using <strong>ASP.NET Core Web API</strong> with <strong>.NET 9</strong> and follows a <strong>Hexagonal Architecture (Ports & Adapters)</strong> approach to ensure clear separation between domain logic, application use cases, infrastructure concerns, and API delivery mechanisms.

This architecture allows the business logic to remain independent from frameworks, databases, and external technologies.
</p>
---

## 🚀 Features

- Create orders for agricultural operations

- Clean separation between domain, application, infrastructure, and API layers

- Hexagonal architecture (Ports & Adapters)

- Dependency inversion between layers

- REST API endpoints for order management

- Extensible architecture for future microservices

- Clear separation of business logic and infrastructure
---

## 🛠 Tech Stack

- C#

- .NET 9

- ASP.NET Core Web API

- Hexagonal Architecture (Ports & Adapters)

Built using the .NET and ASP.NET Core.

---

## 🧱 Architecture Overview

The service follows Hexagonal Architecture to isolate the domain logic from external dependencies.

```bash
API → Application → Domain
          ↑
    Infrastructure
```

### Layers

#### Domain
Contains:

- Core business entities

- Domain models

- Ports (interfaces)

This layer has no dependencies on frameworks.

#### Application
Contains:


- Use cases

- Application services

- Business workflows

It coordinates domain operations and interacts with domain ports.

#### Infrastructure
Contains implementations of domain ports such as:


- Persistence adapters

- External service integrations

- Messaging adapters

#### API
Responsible for:


- HTTP controllers

- Request handling

- Routing

- Dependency injection configuration

---

## 📁 Project Structure

```bash
order-service/
│
├── order-service.sln
├── README.md
│
└── src/
    │
    ├── Api/
    │   ├── Controllers/
    │   │   └── OrderController.cs
    │   ├── Program.cs
    │   └── appsettings.json
    │
    ├── Application/
    │   └── UseCases/
    │        └── CreateOrder.cs
    │
    ├── Domain/
    │   ├── Models/
    │   │    └── Order.cs
    │   │
    │   └── Ports/
    │        ├── Driven/
    │        │    └── IOrderPersistencePort.cs
    │        └── Driving/
    │
    └── Infrastructure/
        └── Adapters/
             └── Driven/
                  └── OrderPersistenceAdapter.cs
```

---

## 🔄 Request Flow

Example flow when creating an order:

```bash
Client
  │
  ▼
OrderController (API Layer)
  │
  ▼
CreateOrder (Application Layer)
  │
  ▼
Order (Domain Model)
  │
  ▼
IOrderPersistencePort (Domain Port)
  │
  ▼
OrderPersistenceAdapter (Infrastructure Adapter)
```
This ensures the domain layer remains independent of infrastructure details.

---

## ⚙️ Installation

### 1. Clone the repository

```bash
git clone https://github.com/AgricultureOperations/order-service.git
```

### 2. Navigate to the project

```bash
cd order-service
```

### 3. Restore dependencies

```bash
dotnet restore
```

### 4. Run the API:

```bash
dotnet run --project src/Api
```

---

## 🔗 AgriOps Platform

This service is part of the AgriOps ecosystem, which includes:

- AgriOps Web – React frontend application

- Backend microservices

- API gateway

- Agricultural operations management tools

Frontend repository:

https://github.com/AgricultureOperations/frontend

---

## 🧠 Design Decisions

- ✅ Hexagonal architecture for maintainability

- ✅ Domain logic isolated from frameworks

- ✅ Ports and adapters pattern

- ✅ Layered dependency flow

- ✅ Microservice-ready architecture

- ✅ Clear separation of concerns 

---

## 🔮 Future Improvements

- Add order retrieval endpoints

- Implement order update and cancellation

- Add DTOs and validation layer

- Add unit and integration tests

- Introduce containerization with Docker

- Add CI/CD pipeline with GitHub Actions

---

## 📌 Author

**Edward Cruz**  

Full Stack Developer | ASP .Net Core | .Net | Microservice