# HW Inventory Platform (Monorepo Skeleton)

This repository provides a .NET 8 and React monorepo scaffold for the HW Inventory platform described in the consolidated specification. The previous Python prototype has been replaced with the target architecture so that future iterations can focus on completing feature depth rather than replatforming.

> **Status:** the solution is **not** production-ready. The current codebase lacks authentication, authorization, SQL Server persistence, front-end implementation, background jobs, packaging, and most domain workflows (handover, imports/exports, backups, updates, connectors, etc.). Refer to [`docs/implementation-plan.md`](docs/implementation-plan.md) for the exhaustive backlog needed to reach feature parity with the specification.

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

The skeleton is intentionally incomplete compared to the full specification. Major follow-up tasks include (see the implementation plan for detail):

* Implementing authentication/authorization (Identity, policies, 2FA, LDAP/OIDC integration) with auditable scope controls.
* Migrating to SQL Server with EF Core migrations, seeders, background jobs, and persistent audit logging.
* Building the React admin (`src/web`) with onboarding wizard, dark/light theming, feature pages, and contextual help.
* Delivering domain workflows: workstation handover PDFs/e-mails, label management and printing, imports/exports with jobs, reports & schedules, connectors catalogue, backups/restores, updates, observability, Help Center.
* Completing packaging: installer scripts, All-in-One builder, GitHub Actions, documentation set (manual, release notes, deployment guide), and smoke tests.

### Suggested execution order

If you are planning the next development iteration, tackle the milestones in this order (summarised from the implementation plan):

1. **Foundations & persistence** – move to SQL Server, produce migrations, seed data, configuration options, and job scheduling infrastructure.
2. **Identity & security** – wire up Identity, 2FA, SSPR, CAPTCHA, scoped RBAC, and the onboarding wizard steps that exercise SMTP/LDAP connectivity.
3. **Inventory workflows** – finish CRUD, audit diffing, VLAN/IP enforcement, handover PDFs/e-mails, labels, imports/exports, and async jobs.
4. **Operations & integrations** – connectors catalogue, Updates, Backup & Restore, Reports & Schedules, Observability dashboards.
5. **Frontend completion** – full React admin, theming, contextual help/manual integration, UI automation tests.
6. **Packaging & release** – installers, builder scripts, GitHub Actions, All-in-One ZIP with SHA256, documentation, and smoke/regression test suites.

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
