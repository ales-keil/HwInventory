# HW Inventory Platform

This repository provides a .NET 8 and React monorepo scaffold for the HW Inventory platform described in the consolidated specification. The previous Python prototype has been replaced with the target architecture so that future iterations can focus on completing feature depth rather than replatforming.

> **Status:** the solution is **not** production-ready. The current codebase now boots with SQL Server, ASP.NET Core Identity (including TOTP 2FA scaffolding), baseline policy-based RBAC guard rails with per-user location/department scoping, persistent audit logging, Quartz.NET job scheduling, a label rendering engine, and a React administration experience with dark/light theming. Security integrations cover LDAP/AD test/dry-run endpoints, hardened HTTPS+PKCE OIDC configuration, configurable password policies with history/lockout enforcement, CAPTCHA and SMS connector management, SMTP connector management with test e-mail delivery, AD group-to-role mappings, middleware-backed session tracking with remote logout APIs, a security readiness summary API/UI (blocking the onboarding wizard until all critical connectors are configured), and dedicated React pages „Security Settings“, „Email (SMTP)“, „Konektory“, „Observabilita“, „Backup & Restore“, „Import/Export“, „Aktualizace“ a „Help Center“. Inventární moduly nyní zahrnují plnohodnotné React stránky pro Servery, Síťová zařízení, Pracovní stanice, Audit, Číselníky, Štítky a Moduly s CRUD formuláři, stránkovanými tabulkami, hromadnými akcemi a integrací s číselníky a renderováním štítků. Importní pipeline podporuje CSV/XLSX, konfliktní strategie, mapování sloupců a notifikace. Nově je k dispozici builder (`scripts/builder.ps1`) a GitHub Actions workflow (`.github/workflows/build-and-package.yml`) generující All-in-One balík se SHA256 checksumem. Zbývají pokročilé workflow (plánované reporty, fyzické tiskové konektory, handover PDF/e-mail proces, rozšířené observability dashboardy, smoke/regresní testy) – kompletní backlog viz [`docs/implementation-plan.md`](docs/implementation-plan.md).

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
* Dovršit React administraci o onboarding wizard s validacemi, pokročilé filtry/hromadné akce v inventárních tabulkách, doplnit UI pro plánované reporty a tisk štítků z výběrů.
* Delivering domain workflows: workstation handover PDFs/e-mails, export enhancements (filtry, expirace odkazů, auditované stažení), reports & schedules, extended connectors (SFTP/FTPS, storage, printing, webhooks), incremental/advanced backup features (např. dedikované storage konektory, incremental snapshoty, archivace) a observability dashboards/alerting.
* Completing packaging: finalising documentation set (manual, release notes, deployment guide) a smoke/regression testy pro publish pipeline.

### SQL Server & IIS deployment checklist

1. Publish the API project (`dotnet publish src/api/src/HWInventory.Api/HWInventory.Api.csproj -c Release -o publish`).
2. Copy the contents of the `publish` folder to the IIS server.
3. Run `scripts/installer.ps1` with administrator privileges:
   ```powershell
   cd scripts
   .\installer.ps1 -PublishPath "C:\inetpub\HWInventory" -SiteName "HWInventory" -AppPoolName "HWInventoryPool" -SqlConnectionString "Server=sql01;Database=HWInventory;User Id=hwinv;Password=Secret;TrustServerCertificate=True"
   ```
   The script updates `appsettings*.json` with the provided SQL Server connection string, provisions/updates the IIS site + app pool (unless `-SkipIisProvisioning` is supplied), and grants modify permissions to the pool identity.
4. Verify SQL connectivity by running the API locally (`dotnet run`) or starting the IIS site, then execute EF Core migrations (the API performs `DbContext.Database.Migrate()` on startup) and confirm `/health` returns `OK`.
5. Complete the first-run wizard in the browser to configure SMTP, LDAP/SSPR connectors, and seed the Super Admin account.

### Update workflow (Settings → Aktualizace)

* Upload ZIP/PKG packages through the new REST endpoint `POST /api/updates` or via the React administration page **Settings → Aktualizace**.
* Each upload computes an SHA256 hash, persists the package under `%ProgramFiles%/HWInventory/updates`, and optionally triggers a full backup before processing.
* Quartz job `UpdatePackageProcessor` validates integrity, extracts the archive to a staging directory, parses `update-manifest.json` (if present), and stores verbose log output available under `GET /api/updates/{id}/log`.
* The updates UI lists package history, surface errors, and provides one-click download of logs and manifest preview, ensuring administrators have an audit trail before swapping binaries in production.

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
