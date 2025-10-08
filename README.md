# HW Inventory Platform (Monorepo Skeleton)

This repository provides a .NET 8 and React monorepo scaffold for the HW Inventory platform described in the consolidated specification. The previous Python prototype has been replaced with the target architecture so that future iterations can focus on completing feature depth rather than replatforming.

> **Status:** the solution is **not** production-ready. The current codebase now boots with SQL Server, ASP.NET Core Identity (including TOTP 2FA scaffolding), baseline policy-based RBAC guard rails with per-user location/department scoping, persistent audit logging, Quartz.NET job scheduling, a label rendering engine, and a React admin shell with dark/light theming. Security integrations cover LDAP/AD test/dry-run endpoints, hardened HTTPS+PKCE OIDC configuration, configurable password policies with history/lockout enforcement, CAPTCHA and SMS connector management, SMTP connector management with test e-mail delivery, AD group-to-role mappings, middleware-backed session tracking with remote logout APIs, a security readiness summary API/UI (blocking the onboarding wizard until all critical connectors are configured), and dedicated React pages „Security Settings“, „Email (SMTP)“, „Konektory“ and „Observabilita“. Aktuální iterace doplnila modul „Backup & Restore“ (REST API `/api/maintenance/*`, Quartz plánování, šifrované ZIP archivy, SMTP notifikace), modul „Import/Export“ (`/api/exports/*`, `/api/imports/*`, Quartz procesory a administrace v UI) a základní regresní testy (`HWInventory.Infrastructure.Tests`) pokrývající politiky hesel. However, the majority of domain workflows (např. plná importní pipeline, plánované reporty, aktualizace, další konektory a kompletní frontend) are still outstanding—see [`docs/implementation-plan.md`](docs/implementation-plan.md) for the full backlog.

## Structure

```
/attachments      Supporting templates and artefacts (placeholders)
/db               Database migrations and seed data (to be completed)
/docs             Product documentation (manual, release notes, etc.)
/scripts          Deployment helpers (IIS installer placeholder)
/src
  /api            ASP.NET Core solution (Domain, Application, Infrastructure, Api)
  /web            React admin shell (Vite + Tailwind, dark/light theme toggle)
```

### API Solution

The API solution (`src/api/HWInventory.sln`) includes four projects:

* **HWInventory.Domain** – entity definitions for servers, network devices, workstations, audit, RBAC, connectors, labels, and settings.
* **HWInventory.Application** – shared abstractions (e.g., `IAppDbContext`), paging helpers, and DTOs for application services.
* **HWInventory.Infrastructure** – EF Core `DbContext`, SQL Server migrations, Identity integration, Quartz.NET job wiring (label print and backup processors), label rendering services, and initial seed data.
* **HWInventory.Api** – ASP.NET Core Web API exposing the required endpoints (`/api/servers`, `/api/network-devices`, `/api/workstations`, `/api/dictionaries`, `/api/audit`, `/api/modules`, `/api/dashboard/summary`, `/api/labels/*`, `/api/auth/*`).

The API defaults to SQL Server (LocalDB for development, configurable via `appsettings.json`), runs migrations on startup, and seeds baseline dictionaries, roles, and feature modules. Controllers implement CRUD, soft-retire/restore flows, module toggling, dashboard summaries, label rendering (QuestPDF + ZPL), `/api/auth` endpoints for profile introspection plus TOTP enrollment, observability settings, connector management, and backup/restore maintenance APIs. Background processing is orchestrated through Quartz.NET (`LabelPrintJobProcessor`, `BackupJobProcessor`).

### Remaining Work

The skeleton is intentionally incomplete compared to the full specification. Major follow-up tasks include (see the implementation plan for detail):

* Rozšířit bezpečnostní governance o pokročilé dashboardy (hlavní homepage widgety, observability metriky), širší regresní pokrytí (LDAP sync, SSPR edge cases, CAPTCHA throttling) a další bezpečnostní alerting.
* Building the React admin (`src/web`) with onboarding wizard, dark/light theming, feature pages, and contextual help.
* Delivering domain workflows: workstation handover PDFs/e-mails, label management and printing, rozšířené import pipelines (konfliktní strategie se zápisem do DB, XLSX, mapování sloupců, UI validace), export enhancements (filtry, expirace odkazů, auditované stažení), reports & schedules, extended connectors (SFTP/FTPS, storage, printing, webhooks), incremental/advanced backup features (např. dedikované storage konektory, incremental snapshoty, archivace), updates, observability dashboards/alerting, Help Center.
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
