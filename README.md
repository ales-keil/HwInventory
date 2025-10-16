# HW Inventory Platform

This repository provides a .NET 8 and React monorepo scaffold for the HW Inventory platform described in the consolidated specification. The previous Python prototype has been replaced with the target architecture so that future iterations can focus on completing feature depth rather than replatforming.

> **Status:** the solution is **not** production-ready. The current codebase now boots with SQL Server, ASP.NET Core Identity (including TOTP 2FA scaffolding), baseline policy-based RBAC guard rails with per-user location/department scoping, persistent audit logging, Quartz.NET job scheduling, a label rendering engine, and a React administration experience with dark/light theming. Security integrations cover LDAP/AD test/dry-run endpoints, hardened HTTPS+PKCE OIDC configuration, configurable password policies with history/lockout enforcement, CAPTCHA and SMS connector management, SMTP connector management with test e-mail delivery, storage connector configuration (local disk/SMB/S3) with inline health checks, SFTP/FTPS connector configuration with secret rotation and health metadata, **printing connector management (RAW 9100/fronta) with auditované testy a sdílenými tajemstvími**, webhook connector configuration with signed test dispatch, AD group-to-role mappings, middleware-backed session tracking with remote logout APIs, a security readiness summary API/UI (blocking the onboarding wizard until all critical connectors are configured), and dedicated React pages „Security Settings“, „Email (SMTP)“, „Webhooks“, „Konektory“, „Úložiště“, „SFTP / FTPS“, „**Tisk**“, „Observabilita“, „Backup & Restore“, „Import/Export“, „Reporty & Schedules“, „Aktualizace“ a „Help Center“. Inventární moduly nyní zahrnují plnohodnotné React stránky pro Servery, Síťová zařízení, Pracovní stanice, Audit, Číselníky, Štítky a Moduly s CRUD formuláři, stránkovanými tabulkami, fulltextem a filtry (status, prostředí/typ, lokalita), hromadnými akcemi a integrací s číselníky a renderováním štítků. Workflow předání pracovních stanic nyní generuje QuestPDF protokol, odesílá e-mail s tlačítky „Přijmout“/„Odmítnout“ a po potvrzení příjemcem automaticky přepíše vlastníka/umístění (Super Admin může převod vynutit bez schválení). Úvodní dashboard načítá inventární statistiky, bezpečnostní ukazatele (2FA adopce, uzamčené účty, aktivní relace) a nejnovější auditní záznamy, přičemž stejné metriky jsou k dispozici i ve formátu Prometheus `/metrics`. Importní pipeline podporuje CSV/XLSX, konfliktní strategie, mapování sloupců a notifikace a nově je doplněna o REST API a React stránku pro správu reportů a jejich plánování (definice, historie běhů, ruční spuštění, stahování artefaktů). Nově je k dispozici builder (`scripts/builder.ps1`) a GitHub Actions workflow (`.github/workflows/build-and-package.yml`) generující All-in-One balík se SHA256 checksumem. Zbývají pokročilé workflow (rozšířené observability dashboardy, smoke/regresní testy, fyzické tiskové fronty napojené na zařízení, pokročilé reportingové exporty) – kompletní backlog viz [`docs/implementation-plan.md`](docs/implementation-plan.md). Pokud potřebujete rychle nasadit pouze lokální variantu s inventářem serverů/sítě/stanic, sledujte roadmapu v [`docs/minimal-deployment-plan.md`](docs/minimal-deployment-plan.md) a detailní postup v [`docs/minimal-deployment-guide.md`](docs/minimal-deployment-guide.md).

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
* Dovršit React administraci o onboarding wizard s validacemi, doplnit UI pro plánované reporty a tisk štítků z výběrů a dokončit workflow kolem help center.
* Delivering domain workflows: workstation handover PDFs/e-mails, export enhancements (pokročilé filtry, notifikace a archivace odkazů), rozšířený reporting (pokročilé filtry, e-mailové notifikace, archivace běhů), extended connectors (napojení fyzických tiskových cílů), incremental/advanced backup features (např. dedikované storage konektory, incremental snapshoty, archivace) a observability dashboards/alerting.
* Completing packaging: finalising documentation set (manual, release notes, deployment guide) a smoke/regression testy pro publish pipeline.

### SQL Server & IIS deployment checklist

1. Publish the API project (`dotnet publish src/api/src/HWInventory.Api/HWInventory.Api.csproj -c Release -o publish`).
2. Build the React frontend (`cd src/web && npm install && npm run build`) – the artefacts will be placed under `publish/web` when the builder is used.
3. Run `scripts/installer.ps1` with administrator privileges (the script copies both the API publish output and the React build by default):
   ```powershell
   cd scripts
   .\installer.ps1 -PublishPath "C:\inetpub\HWInventory" -SiteName "HWInventory" -AppPoolName "HWInventoryPool" -SqlConnectionString "Server=sql01;Database=HWInventory;User Id=hwinv;Password=Secret;TrustServerCertificate=True"
   ```
   The script copies artefacts from `publish/api` and `publish/web`, updates `appsettings*.json` with the provided SQL Server connection string, generates (or uses the supplied) seed admin credentials, protects the password using DPAPI by default, ensures the database exists, optionally runs migrations via `dotnet HWInventory.Api.dll --apply-migrations`, provisions/updates the IIS site + app pool (unless `-SkipIisProvisioning` is supplied), and grants modify permissions to the pool identity. Use `-DisablePasswordEncryption` if you need the password stored in plain text (e.g., for non-Windows hosting) and `-SkipCopy`, `-SkipApiCopy`, or `-SkipWebCopy` to control copying behaviour. Pokud heslo nezadáte, skript vygeneruje náhodné 24znakové heslo, zobrazí jej v konzoli a uloží (šifrovaně) do konfigurace.
4. Start the IIS site (or run `dotnet HWInventory.Api.dll`) to finish applying EF Core migrations, then confirm `/health` returns `OK`.
5. Complete the first-run wizard in the browser. In lokálním režimu je možné přeskočit externí konektory a pokračovat pouze s lokálními účty.

### Minimální lokální nasazení (bez konektorů)

Pokud chcete rychle zprovoznit jen inventární moduly **Servers / Network devices / Workstations** na jednom serveru s SQL Serverem, držte se těchto kroků:

1. Publikujte API (`dotnet publish src/api/src/HWInventory.Api/HWInventory.Api.csproj -c Release -o publish`) a vybuilděte React administraci (`cd src/web && npm install && npm run build`). Výchozí konfigurace `src/web/.env.production` nastavuje `VITE_MINIMAL_MODE=true`, takže se v UI zobrazí pouze nezbytné sekce.
2. Zkopírujte obsah `publish` na cílový server (např. `C:\inetpub\HWInventory`). Ujistěte se, že v adresáři leží `appsettings.Production.json` s ukázkovým connection stringem, které můžete dále upravit.
3. Spusťte `scripts/installer.ps1` (viz výše) – skript nastaví connection string, inicializačního admina a spustí migrace proti SQL Serveru.
4. Otevřete aplikaci v prohlížeči, v průvodci zaškrtněte „Pokračovat v lokálním režimu“ a dokončete onboarding bez SMTP/LDAP.
5. Ověřte funkčnost pomocí smoke testu `scripts/smoke-test.ps1` (parametry `-BaseUrl`, `-UserName`, `-Password`). Skript provede přihlášení, vytvoří ukázkové servery/síťová zařízení/stanice a zkontroluje auditní log.
6. Po prvním přihlášení změňte heslo výchozího účtu **admin@localhost / ChangeMe!123!**.

### Update workflow (Settings → Aktualizace)

* Upload ZIP/PKG packages through the new REST endpoint `POST /api/updates` or via the React administration page **Settings → Aktualizace**.
* Each upload computes an SHA256 hash, persists the package under `%ProgramFiles%/HWInventory/updates`, and optionally triggers a full backup before processing.
* Quartz job `UpdatePackageProcessor` validates integrity, extracts the archive to a staging directory, parses `update-manifest.json` (if present), and stores verbose log output available under `GET /api/updates/{id}/log`.
* The updates UI lists package history, surface errors, and provides one-click download of logs and manifest preview, ensuring administrators have an audit trail before swapping binaries in production.

### Suggested execution order

If you are planning the next development iteration, tackle the milestones in this order (summarised from the implementation plan):

1. **Foundations & persistence** – move to SQL Server, produce migrations, seed data, configuration options, and job scheduling infrastructure.
2. **Identity & security** – wire up Identity, 2FA, SSPR, CAPTCHA, scoped RBAC, and the onboarding wizard steps that exercise SMTP/LDAP connectivity.
3. **Inventory workflows** – finish CRUD, audit diffing, VLAN/IP enforcement, labels, imports/exports, and async jobs.
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

### Výchozí lokální účet

Při prvním spuštění (během seedování databáze) se vytvoří účet **admin@localhost** s heslem **ChangeMe!123!**. Hodnoty lze přepsat v konfiguraci (`SeedAdmin:Email`, `SeedAdmin:Password`). Heslo po přihlášení neprodleně změňte a zvažte zapnutí 2FA.

## Licensing

QuestPDF is licensed under the Polyform Noncommercial license. Evaluate licensing implications before production use.
