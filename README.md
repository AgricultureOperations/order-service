# 🌱 AgricultureOperations - Order Service

<p> Order Service is a backend microservice of the <strong>AgricultureOperations platform</strong>, responsible for managing agricultural operation orders.

The service is built using <strong>ASP.NET Core Web API</strong> with <strong>.NET 9</strong> and follows a <strong>Hexagonal Architecture (Ports & Adapters)</strong> approach to ensure clear separation between domain logic, application use cases, infrastructure concerns, and API delivery mechanisms.

This architecture allows the business logic to remain independent from frameworks, databases, and external technologies.
</p>

---

## 🚀 Features

- Full CRUD Operations for agricultural orders

- Hexagonal architecture (Ports & Adapters)

- Clean separation between domain, application, infrastructure, and API layers

- Dependency inversion between layers

- SQLite persistence layer

- Entity Framework Core as ORM

- RESTFul API endpoints for order management

- Docker containerization support

- Automatic database migrations on application startup

- Microservice-ready architecture

- Easily extensible for future services
---

## 🛠 Tech Stack

- C#

- .NET 9

- ASP.NET Core Web API

- SQLite

- Entity Framework Core

- Docker

- Hexagonal Architecture (Ports & Adapters)

---

## 🧱 Architecture Overview

The service follows Hexagonal Architecture to isolate the domain logic from external dependencies.

```bash
        ┌───────────────┐
        │      API      │
        └──────┬────────┘
               │
               ▼
        ┌───────────────┐
        │ Application   │
        └──────┬────────┘
               │
               ▼
        ┌───────────────┐
        │    Domain     │
        └──────┬────────┘
               ▲
               │
        ┌───────────────┐
        │ Infrastructure│
        └───────────────┘
```

---

## 🎯 Purpose

This project demonstrates the implementation of a scalable backend service using Hexagonal Architecture, focusing on:

- Separation of concerns across layers
- Domain-driven design principles
- Decoupled infrastructure using ports and adapters
- Clean and maintainable code structure

---

### Layers

- Domain
  Contains the core business entities and interfaces(Ports)

- Application
  Contains DTOs and the use cases that coordinates business operations.

- Infrastructure
  Contains implementations of domain ports such as:
  - Persistence adapters

  - External service integrations

  - Messaging adapters

- API
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
│── Dockerfile
│── .dockerignore
└── src/
    │
    ├── Api/
    │   ├── Controllers/        
    │   ├── Program.cs
    │   └── appsettings.json
    │
    ├── Application/
    │   ├── DTOs/
    │   └── UseCases/
    │
    ├── Domain/
    │   ├── Entities/
    │   └── Ports/
    │        └── Driven/
    │
    └── Infrastructure/
        │── Adapters/
        │    └── Driven/
        │── Migrations/
        │
        └── Persistence/
             └── OrderDbContext.cs
```

---

## 🔄 Request Flow

Example flow when creating an order:

```bash
Client
  │
  ▼
OrderController (API)
  │
  ▼
CreateOrderUseCase (Application)
  │
  ▼
Order (Domain)
  │
  ▼
IOrderPersistencePort (Boundary / Port)
  │
  ▼
OrderPersistenceAdapter (Infrastructure)
  │
  ▼
SQLite (Database)
```
This ensures the domain layer remains independent of infrastructure details.

---

## 🗄 Database

The service uses SQLite as a lightweight embedded database for local development.

Example connection string:

```bash
Data Source=orders.db
```

The database is managed via Entity Framework Core.

---

## 🐳 Running with Docker

The service can be built and executed as a Docker container.

### Build the image

```bash
docker build -t order-service .
```

### Run the container

```bash
docker run -p 8080:8080 order-service
```

The API will be available at:
```bash
http://localhost:8080
```

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

- ✅ Hexagonal architecture to decouple domain logic from infrastructure

- ✅ Use of driven ports to abstract persistence layer

- ✅ Application layer orchestrates use cases without leaking infrastructure concerns

- ✅ Domain layer remains framework-agnostic

- ✅ DTOs defined at application boundary to isolate transport layer

- ✅ Microservice-ready architecture

- ✅ Docker-ready deployment

---

## 🔮 Future Improvements

- Add validation layer using FluentValidation

- Implement pagination and filtering for orders

- Support multiple databases (PostgreSQL)

- Add unit and integration tests

- Introduce API documentation with OpenAPI/Swagger

- Add CI/CD pipeline with GitHub Actions

- Add container orchestration support

- Support multiple databases (PostgreSQL)

---

## 📌 Author

**Edward Cruz**  

Full Stack Developer | ASP .Net Core | .Net | Microservice