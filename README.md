# FlowForge

> An AI-augmented project and workflow management platform built with .NET 8, Angular 18, and a production-grade event-driven architecture.

FlowForge is a multi-tenant SaaS application that helps teams plan projects, organize tasks across customizable boards, automate repetitive workflows, and use AI to summarize progress and detect risks before they become blockers.

Think of it as Jira's structure, Linear's speed, and Monday's flexibility, augmented with an AI assistant that actually understands your team.

## What This Project Is

FlowForge is a real, working SaaS application built from the ground up to demonstrate senior level software engineering practices, not a tutorial project or a CRUD demo.

Every architectural decision in this codebase is intentional and reflects how production systems are built at scale. The system supports multiple isolated organizations (multi-tenancy), separates read and write concerns (CQRS), communicates between services through events (RabbitMQ), and uses AI as one component inside a larger workflow, not as the entire product.

## Why This Project Exists

The motivation behind FlowForge comes from a single observation: most junior developer portfolios contain todo apps, weather widgets, and basic e-commerce clones. These projects show that someone can write code, but they don't show the engineering judgment that separates a junior dev from a mid level or senior one.

FlowForge is intentionally built to demonstrate the things that matter in real engineering teams:

| Concern | How It's Addressed |
|---------|---------------------|
| Scalability | Multi-tenant architecture with row level data isolation |
| Maintainability | Clean Architecture with Domain Driven Design |
| Reliability | Event-driven communication, distributed tracing, health checks |
| Testability | Unit tests, integration tests with real PostgreSQL containers |
| Security | JWT + refresh token rotation, BCrypt hashing, rate limiting |
| Performance | Redis caching, distributed locks, optimistic concurrency |
| Operations | Docker Compose for local, GitHub Actions for CI, structured logging |

The goal is to provide an engineering team a reference point that says "this person understands how production systems are built."

## Core Features

| Category | Features |
|----------|----------|
| Identity | Multi-tenant workspaces, JWT auth with refresh tokens, role based permissions, invitation flow |
| Projects | Custom project keys (like Jira), boards (Kanban / Scrum), columns with WIP limits, drag and drop tasks |
| Tasks | Subtasks, comments with @mentions, file attachments, time tracking, story points, labels, watchers |
| Sprints | Sprint planning, burndown charts, retrospective notes, backlog management |
| Workflows | Custom task states per project, automation rules, state machine transitions |
| AI Engine | Auto assign tasks based on workload, sprint summaries, blocker detection, task breakdown suggestions |
| Notifications | Real-time updates via SignalR, in-app notifications, email digests, Slack integration |
| Admin Panel | Separate application for system admins to manage tenants, users, audit logs, and feature flags |
| Audit | Immutable audit log of every meaningful action for compliance |

## Tech Stack

### Backend

| Technology | Purpose |
|------------|---------|
| .NET 8 (LTS) | Runtime and framework |
| ASP.NET Core | Web API and SignalR hubs |
| C# 12 | Programming language |
| Entity Framework Core 8 | ORM for PostgreSQL |
| PostgreSQL 16 | Primary relational database |
| Redis 7 | Distributed cache, sessions, locks |
| RabbitMQ | Event bus for async messaging |
| MassTransit | Message bus abstraction over RabbitMQ |
| MediatR | CQRS and pipeline behaviors |
| FluentValidation | Request validation |
| AutoMapper | Object to object mapping |
| Hangfire | Background job orchestration |
| MinIO | S3 compatible file storage |
| OpenAI SDK | AI integration (GPT 4o) |
| Serilog | Structured logging |
| OpenTelemetry | Distributed tracing |
| Polly | Resilience and retry logic |
| BCrypt | Password hashing |
| xUnit | Unit testing framework |
| TestContainers | Integration testing with real Postgres |

### Frontend

| Technology | Purpose |
|------------|---------|
| Angular 18 | Frontend framework |
| TypeScript | Programming language |
| Angular Signals | Reactive state primitives |
| NgRx | Global state management |
| Angular Material | UI component library |
| Tailwind CSS | Utility first styling |
| RxJS | Reactive programming and async streams |
| SignalR client | Real-time WebSocket connection |
| Chart.js | Charts and burndown graphs |
| Angular CDK | Drag and drop, overlays |

### Infrastructure

| Technology | Purpose |
|------------|---------|
| Docker | Containerization |
| Docker Compose | Local multi-service orchestration |
| GitHub Actions | CI/CD pipelines |
| Nginx | Reverse proxy for production |
| MailHog | Local development email server |
| Seq | Log aggregation and search |

## Architecture Overview

The system follows Clean Architecture with strict layering. Each layer only depends on layers below it.

```
Frontend (Angular Main App + Angular Admin Panel)
       responds to user interactions
                  |
                  v
API Layer (ASP.NET Core)
       routes requests, validates auth, version control
                  |
                  v
Application Layer (CQRS handlers)
       executes business logic across bounded contexts
                  |
                  v
Domain Layer (Pure C# business rules)
       aggregates, entities, value objects, domain events
                  |
                  v
Infrastructure Layer (EF Core, Redis, RabbitMQ, OpenAI)
       talks to databases, caches, queues, and external services
                  |
                  v
Data Stores (PostgreSQL, Redis, RabbitMQ, MinIO)
```

The Domain Layer is the innermost ring and depends on nothing. The Infrastructure Layer adapts external systems to the interfaces defined by Application. This makes the system testable and replaceable. Swap PostgreSQL for MongoDB tomorrow and only Infrastructure changes.

## Project Structure

```
FlowForge
  backend
    src
      FlowForge.Domain          Business entities, value objects, domain events
      FlowForge.Application     CQRS commands and queries, validation, DTOs
      FlowForge.Infrastructure  EF Core, repositories, JWT, OpenAI, messaging
      FlowForge.Shared          Shared kernel and base types
      FlowForge.API             REST controllers and SignalR hubs
      FlowForge.Workers         Background job host
    tests
      FlowForge.Domain.Tests
      FlowForge.Application.Tests
      FlowForge.API.IntegrationTests
  frontend
    flowforge-web               Main Angular application
    flowforge-admin             Admin panel Angular application
  docker-compose.yml            Local development services
  docs                          Architecture documentation
  README.md
```

## Running FlowForge Locally

These instructions get the entire stack running on your machine for development or evaluation.

### Prerequisites

Install these tools before starting.

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8.0 or higher | Backend runtime and build |
| Node.js | 20 or higher | Frontend build |
| Angular CLI | 18 or higher | Frontend development server |
| Docker Desktop | Latest | Runs Postgres, Redis, RabbitMQ |
| Git | Any recent version | Cloning the repository |

You can verify each tool is installed by running these commands.

```bash
dotnet --version
node --version
ng version
docker --version
git --version
```

### Step 1 Clone the Repository

```bash
git clone https://github.com/YOUR_USERNAME/FlowForge.git
cd FlowForge
```

### Step 2 Start Background Services

Docker Compose reads its credentials from a `.env` file (gitignored, never committed) rather than from the compose file itself. Create yours from the template first.

```bash
cp .env.example .env
# then open .env and set real values - at minimum POSTGRES_PASSWORD, MINIO_ROOT_PASSWORD, and JWT_KEY
```

This single command starts PostgreSQL, Redis, RabbitMQ, MinIO, MailHog, and Seq inside Docker containers.

```bash
docker compose up -d
```

The first run downloads all images and takes a few minutes. Subsequent starts are nearly instant.

Verify all services are running.

```bash
docker compose ps
```

**Prefer not to install the .NET SDK or Node locally?** `docker compose up -d` (with no
service name) also builds and starts the API and background worker alongside the infra
above - the API applies its own EF Core migrations on startup, so there's no separate
migration step. You'd still run the frontends with `ng serve` as in Step 6, since they
aren't containerized. Steps 3-5 below are for running the API directly with `dotnet run`
instead, which is faster to iterate on while developing the backend.

### Step 3 Configure Secrets (required)

`appsettings.Development.json` intentionally ships with these values blank - the backend
reads them from .NET User Secrets instead, so nothing sensitive lives in the repository.
Running `dotnet run` without doing this step first will fail fast with "Jwt:Key missing".

Use the same values you put in your `.env` file (see [Step 2](#step-2-start-background-services)) so the API behaves identically whether you run it via `dotnet run` or via `docker compose up`.

```bash
cd backend/src/FlowForge.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=flowforge;Username=postgres;Password=<your POSTGRES_PASSWORD from .env>"
dotnet user-secrets set "Jwt:Key" "<your JWT_KEY from .env, at least 32 characters>"
dotnet user-secrets set "RabbitMQ:Username" "<your RABBITMQ_USER from .env>"
dotnet user-secrets set "RabbitMQ:Password" "<your RABBITMQ_PASSWORD from .env>"
dotnet user-secrets set "MinIO:AccessKey" "<your MINIO_ROOT_USER from .env>"
dotnet user-secrets set "MinIO:SecretKey" "<your MINIO_ROOT_PASSWORD from .env>"
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key-here"
```

### Step 4 Apply Database Migrations

This creates all tables in PostgreSQL.

```bash
cd /path/to/FlowForge/backend

dotnet ef database update \
  --project src/FlowForge.Infrastructure/FlowForge.Infrastructure.csproj \
  --startup-project src/FlowForge.API/FlowForge.API.csproj
```

### Step 5 Start the Backend API

```bash
dotnet run --project src/FlowForge.API/FlowForge.API.csproj
```

The API will print the port it's listening on. Typically something like http://localhost:5000 or http://localhost:5076.

### Step 6 Start the Frontend

In a separate terminal.

```bash
cd /path/to/FlowForge/frontend/flowforge-web
npm install
ng serve
```

The Angular app starts on http://localhost:4200.

### Step 7 Open the Application

Open http://localhost:4200 in your browser. Register a new account and you're in.

## Available Services and Dashboards

When everything is running locally, these endpoints are available.

| Service | URL | Credentials |
|---------|-----|-------------|
| Frontend (main app) | http://localhost:4200 | Register your own |
| Admin Panel | http://localhost:4201 | Register your own (system admin) |
| Backend API | http://localhost:5076 | JWT required |
| Swagger UI | http://localhost:5076/swagger | None |
| Health Check | http://localhost:5076/health | None |
| PostgreSQL | localhost:5432 | postgres / FlowForge@123 |
| Redis | localhost:6379 | None |
| RabbitMQ Dashboard | http://localhost:15672 | guest / guest |
| MinIO Console | http://localhost:9001 | minioadmin / minioadmin |
| MailHog Inbox | http://localhost:8025 | None |
| Seq Logs | http://localhost:5341 | None |

## How To Verify Everything Works

A complete end-to-end test of the system.

### Test 1 Authentication

Open Swagger at http://localhost:5076/swagger and call POST /api/v1/auth/register with a sample payload. You should receive an access token and refresh token in the response.

### Test 2 Database

Open DBeaver or pgAdmin and connect to PostgreSQL using the credentials above. You should see tables like users, tenants, memberships, projects, boards, tasks, and so on.

### Test 3 Frontend Login

Go to http://localhost:4200, register an account, and you should land on the dashboard.

### Test 4 Create a Project

From the dashboard, create a new project. Open DBeaver and confirm a row was added to the projects table.

### Test 5 Real Time

Open the dashboard in two browser tabs. Create a task in one tab and watch it appear in the other tab in real time. This confirms SignalR is working.

## Running Tests

```bash
cd backend

# Domain layer - pure business-rule tests, no external dependencies
dotnet test tests/FlowForge.Domain.Tests

# Application layer - command/query handlers, mocked via Moq
dotnet test tests/FlowForge.Application.Tests

# API integration tests - boots the real API against a throwaway PostgreSQL
# container via Testcontainers, so Docker Desktop must be running for this one
dotnet test tests/FlowForge.API.IntegrationTests
```

The same three steps run in CI on every push via `.github/workflows/ci.yml`, alongside a production build of both Angular apps.

## Build Status

| Component | Status |
|-----------|--------|
| Domain layer | Complete |
| Application layer | Complete |
| Infrastructure layer | Complete |
| API layer | Complete |
| Authentication flow | Complete (JWT + refresh tokens, account lockout, suspension) |
| Multi-tenancy | Complete |
| Projects, Boards, Tasks | Complete (Kanban board, drag-and-drop, real-time sync via SignalR) |
| Workflows & automations | Complete ("when task moves to list X, notify/assign user Y") |
| AI integration | Complete (task breakdown, assignee suggestion, project summaries via OpenAI) |
| Notifications | Complete (real-time via SignalR, email via SMTP, Slack via incoming webhook) |
| Frontend main app | Complete |
| Admin panel | Complete (separate Angular app: tenants, users, audit logs, system stats) |
| Audit logging | Complete (every command logged automatically via a MediatR pipeline behavior) |
| Tests | Complete (xUnit unit tests for Domain/Application, TestContainers-backed API integration tests) |
| CI/CD pipeline | Complete (GitHub Actions: backend tests, integration tests, both frontend builds) |
| Docker / deployment | `docker compose up` runs the entire stack (infra + API + Workers) with migrations applied automatically on startup |

### Known gaps

A few packages referenced in the tech stack are not yet wired into the running app -
listed here rather than left to be discovered:

- **Redis** runs in Docker but nothing reads/writes to it yet (no caching or session
  storage is implemented against it).
- **RabbitMQ / MassTransit** runs in Docker but there is no publisher or consumer wired
  up - all current cross-feature communication (automations, notifications) goes through
  in-process MediatR domain events, not the message bus.
- **MinIO** runs in Docker but there is no file upload/attachment feature calling it yet.
- **FlowForge.Workers** is a running background service host but has no actual jobs
  scheduled in it yet (no Hangfire recurring jobs).
- Deployment has been built and tested locally via Docker Compose; it has not been
  deployed to Railway (or any other host) yet.

## Why Each Technology Was Chosen

A few notes for engineers reviewing the stack.

**.NET 8 over Node.js** was chosen because static typing prevents an entire class of bugs that show up in JavaScript at runtime. The performance is also significantly better for the CPU bound work involved in CQRS pipelines, validation, and EF Core queries.

**PostgreSQL over MongoDB** was chosen because project management data has many interconnected relationships (projects to boards to lists to tasks to subtasks to comments) and these are easier to model and query relationally than as embedded documents.

**Angular over React** was chosen because Angular is opinionated. It enforces structure through dependency injection, services, and modules. This produces a more consistent codebase, which matters in larger projects. React is more flexible but flexibility becomes a problem at scale.

**RabbitMQ over Kafka** was chosen because RabbitMQ is simpler to operate and runs well in Docker. Kafka is excellent for very high throughput streams but adds operational complexity FlowForge doesn't need.

**Multi-tenancy with shared database** was chosen because it's the simplest approach that still provides full isolation through TenantId on every row. Database per tenant scales worse for many small tenants.

**JWT with refresh tokens** was chosen because pure JWT cannot be revoked. Refresh tokens enable proper logout, password reset, and session invalidation.

## License

MIT

## Contact

Built as a portfolio project. If you're an engineering manager or senior engineer reviewing this repo and you'd like to talk, reach out at [your email or LinkedIn].
