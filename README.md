# HW Inventory Platform (Monorepo Skeleton)

This repository provides a .NET 8 and React monorepo scaffold for the HW Inventory platform described in the consolidated specification. The previous Python prototype has been replaced with the target architecture so that future iterations can focus on completing feature depth rather than replatforming.

## Structure

```
/attachments      Supporting templates and artefacts (placeholders)
/db               Database migrations and seed data (to be completed)
/docs             Product documentation (manual, release notes, etc.)
/scripts          Deployment helpers (IIS installer placeholder)
/src
  /api            ASP.NET Core solution (Domain, Application, Infrastructure, Api)
  /web            React admin application (to be implemented)
```

### API Solution

The API solution (`src/api/HWInventory.sln`) includes four projects:

* **HWInventory.Domain** – entity definitions for servers, network devices, workstations, audit, RBAC, connectors, labels, and settings.
* **HWInventory.Application** – shared abstractions (e.g., `IAppDbContext`), paging helpers, and DTOs for application services.
* **HWInventory.Infrastructure** – EF Core `DbContext`, entity configurations, DI helpers, and initial seed data.
* **HWInventory.Api** – ASP.NET Core Web API exposing the required endpoints (`/api/servers`, `/api/network-devices`, `/api/workstations`, `/api/dictionaries`, `/api/audit`, `/api/modules`, `/api/dashboard/summary`, `/api/labels/*`).

The API boots with EF Core InMemory by default (until a SQL Server connection string is supplied) and seeds baseline dictionaries, roles, and feature modules. Controllers implement CRUD, soft-retire/restore flows, module toggling, dashboard summaries, and basic label rendering with QuestPDF.

### Remaining Work

The skeleton is intentionally incomplete compared to the full specification. Major follow-up tasks include:

* Implementing authentication/authorization (JWT, policies, 2FA, LDAP/OIDC integration).
* Finishing audit logging, handover workflows (PDF/email), background jobs, imports/exports, and report scheduling.
* Completing the React admin (`src/web`) with Vite + Tailwind, dark/light theming, and wizard UX.
* Creating production-ready installer/build scripts, All-in-One packaging, migrations, and CI pipelines.
* Finalising documentation, attachments, and smoke tests.

## Getting Started

1. Ensure the .NET 8 SDK is installed.
2. Restore dependencies and build the solution:
   ```bash
   cd src/api
   dotnet restore
   dotnet build
   dotnet run --project src/HWInventory.Api/HWInventory.Api.csproj
   ```
3. Browse Swagger UI at `https://localhost:5001/swagger` (or configured URL).

> **Note:** In this environment the .NET SDK and third-party packages (e.g., QuestPDF) are not pre-installed. The project files are provided so that the solution restores and builds once the SDK is available.

## Licensing

QuestPDF is licensed under the Polyform Noncommercial license. Evaluate licensing implications before production use.
