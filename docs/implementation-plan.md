# HW Inventory Platform – Implementation Plan

This document enumerates the outstanding work required to transform the current monorepo skeleton into a production-ready release that satisfies the full specification from the "HW Inventory – Zadání pro Codex" document. Items are grouped by thematic area and capture dependencies where relevant.

## 1. Platform Foundations
- **Adopt SQL Server persistence**
  - Replace the InMemory provider with SQL Server across all environments.
  - Create comprehensive EF Core migrations for the domain model (servers, network devices, workstations, dictionaries, users/roles, audit, connectors, labels, jobs, reports, backups, updates, settings, modules, etc.).
  - Seed baseline dictionaries, RBAC roles, feature modules, connectors, and system settings with idempotent scripts.
- **Configuration model**
  - Introduce strongly-typed options for App Configuration, Security & Auth, LDAP, SMTP, Connectors, Reports, Import/Export, Maintenance, Observability, Advanced, About/Support, Handover, Updates, Backup & Restore.
  - Ensure secrets are stored encrypted (DPAPI on Windows / AES-256 elsewhere) and surfaced in the UI as "set/unset" flags with reset flows.
- **Background job infrastructure**
  - Integrate Quartz.NET (or Hangfire if selected) for scheduled jobs (LDAP sync, reports, backups, exports, print jobs, maintenance, observability health checks).
  - Design persistence tables for job definitions, executions, locks, and audit.

## 2. Security, Identity, and RBAC
> _Progress update:_ Identity foundation, PKCE-enforced OIDC configuration, password history enforcement, security readiness summary, and baseline regression tests are in place. Items below track the remaining enhancements required for full compliance.
- **Identity provider**
  - Implement ASP.NET Core Identity with support for local accounts and Active Directory/LDAP linked identities.
  - Add password policies (complexity, expiry, history) and lockout rules in line with the specification.
- **Authentication mechanisms**
  - Enable cookie-based sessions for the admin UI plus JWT/OIDC for API access as needed.
  - Support external SSO/OIDC providers with configurable discovery and claim mapping.
- **Two-factor authentication (2FA)**
  - Provide TOTP (OtpNet) and e-mail OTP methods with enforced enrollment for Super Admin.
  - Implement per-role/group policy enforcement, overrides, reset flows, backup codes, and audit trails.
- **Self-service password reset (SSPR)**
  - Build secure token issuance with optional SMS OTP via the SMS connector, CAPTCHA enforcement, rate limiting, and audit logging.
- **Session management**
  - Expose active sessions, remote logout, automatic invalidation after password changes, and inactivity timeouts.
- **RBAC policies**
  - Materialize policies (Servers.*, Network.*, Workstations.*, Dicts.*, Audit.Read, Settings.*) and map LDAP groups to roles with an auditable ruleset.
  - Implement data scoping per department/location across queries, exports, dashboards, and print jobs.

## 3. Domain Features
- **Inventory management**
  - Complete CRUD flows, retire/restore, hard delete (with password confirmation), comments, audit history, Excel import/export, PDF/CSV exports.
  - Enforce VLAN/IP pairing logic, status management, and scoped administration for Aplikační admin role.
- **Workstation handover**
  - Implement mandatory old/new location capture, PDF generation via QuestPDF, templated e-mails with dynamic recipients, CC/BCC, attachments, and audit entries.
- **Labels & printing**
  - Finalize label template editor (JSON schema), Code128/EAN-13/QR rendering, ZPL and PDF outputs, batch printing, print job queue with destinations (download, named printer, raw socket 9100), audit, and artefact retention policies.
- **Dictionaries**
  - Build full CRUD with import/export (CSV/XLSX), duplicate detection, referential integrity, VLAN metadata requirements, and audit.
- **Audit logging**
  - Persist JSON diffs for all entity operations, support filtering (entity, user, action, timeframe), highlight deletes/restores, and include export functionality that logs access.
- **Reports & schedules**
  - Provide report definitions with filters (e.g., retired devices, aging assets, expiring warranties), scheduling (daily/weekly/monthly), async execution with e-mail delivery, and history tracking.
- **Import/Export jobs** ✅ _(baseline completed 2025-10-09)_
  - Implementováno: CSV/XLSX ingest, mapování sloupců, konfliktní strategie (skip/update/create), dry-run, auditované souhrny, e-mail notifikace, perzistence výsledků, React UI pro správu fronty a Quartz joby pro import/export.
  - Zbývá: rozšířit exporty o pokročilé filtry a expiraci odkazů, přidat plánované exporty/notifikace přes více konektorů, dovést auditované stažení artefaktů a rozšířit importní validace o business pravidla.
- **Backup & restore**
  - Rozšířit stávající modul o pokročilé funkce: incremental/differential snapshoty, cílové storage konektory (SMB/S3), archivaci a čištění starých záloh, export/import pouze nastavení aplikace, CLI nástroje a smoke testy obnovy.
- **Updates module** ✅ _(baseline delivered 2025-10-08)_
  - Implementováno: REST API `/api/updates`, perzistence balíčků, výpočet SHA256, volitelná záloha přes `UpdateService` + `BackupService`, extrakce do staging adresáře, parsování `update-manifest.json`, audit, download logů a React stránka „Aktualizace“.
  - Zbývá: propojit se skutečnou maintenance mode signalizací, file-swap orchestrace (stop web, replace publish output, warm-up), rollback workflow v UI, integrace s All-in-One builderem a smoke testy aktualizačního scénáře.
- **Connectors catalogue**
  - Rozšířit REST API a React stránku „Konektory“ o CRUD/editaci profilů pro Database, External SQL, SFTP/FTPS, Storage, Printing, Observability a Zabbix včetně health-checků, plánovaných ověřování, retry/backoff politik a správy tajemství (aktuální iterace pokrývá SMTP/SMS testy, LDAP/OIDC přehledy a samostatné Webhooks UI/testy).
- **Observability**
  - Expose structured logging, /health, /metrics, OpenTelemetry exporter configuration, correlation IDs in responses, diagnostics export for support.

## 4. Frontend (React + Vite + Tailwind)
- **App shell**
  - Implement authentication views, onboarding wizard (9 steps), dark/light theme toggle persisted in localStorage, responsive layout, accessibility considerations.
- **Feature pages**
  - Evidence pages for servers, network devices, workstations with tables, filters, scoped data, multi-select actions (batch export, print, retire).
  - Audit explorer with filtering, highlighting, diff viewer, export.
  - Labels & printing section with template editor, preview, batch printing workflow, job status list.
  - Settings subsections (App Configuration, Security & Auth, LDAP/AD Sync, Email, Dictionaries, Roles & Users, Labels & Printing, Reports & Schedules, Import/Export, Maintenance, Observability, Advanced, About & Support, Handover, Updates, Backup & Restore).
  - Modules page with toggles (GET/POST /api/modules).
  - Dashboard with counts, recent changes, quick actions.
  - Help Center integration (embedded manual viewer, contextual "?" tooltips linking to manual chapters). ✅ _(viewer delivered 2025-10-09 – zbývají kontextové tooltipy)_
- **API integration**
  - Provide strongly-typed clients (OpenAPI generated or hand-written) with pagination, filters, and consistent error handling.
  - Implement optimistic UI updates where appropriate and handle async job polling.

## 5. Packaging & Deployment
- **Installer & builder scripts**
  - Complete PowerShell installer (installer.ps1) to set up IIS App Pool, file permissions, environment configuration, and health checks. ✅ _(aktualizováno 2025-10-09: přidáno nastavování SeedAdmin, vytvoření DB, volitelné migrace)_
  - Provide merge/all-in-one builder supporting MERGE/ALLINONE modes with logging, phase detection, regex fixes, wizard verification, and optional installer automation. ✅ _(builder.ps1 delivered 2025-10-09 – zbývá rozšíření installeru a smoke testy)_
- **CI/CD** ✅ _(baseline workflow delivered 2025-10-09)_
  - Autor GitHub Actions workflows for build, test (API + web), publish artefacts, generate All-in-One ZIP with SHA256 manifest, and smoke tests. _(Remaining: FE lint/test, smoke/regression coverage, release publikace)_
- **All-in-One artefact**
  - Package publish output, API contracts, migrations, installer, documentation, attachments, observability examples, release notes, checksums.
- **Documentation**
  - Produce manual.pdf, release notes template updates, user guide, deployment guide, troubleshooting, rollback instructions.

## 6. Testing & Quality
- **Automated tests**
  - Unit, integration, and end-to-end tests covering domain logic, API endpoints, RBAC enforcement, background jobs, printing workflows, import/export, backups, updates.
  - UI tests (Playwright) for key flows including onboarding wizard, settings updates, inventory CRUD, label printing, report scheduling.
- **Smoke tests**
  - Scripts verifying /health, login + 2FA, CRUD basics, label rendering, SMTP/LDAP tests post-deployment. ✅ _(částčně – `scripts/smoke-test.ps1` pokrývá login + CRUD inventáře + audit; zbývá rozšířit o 2FA/SMTP/LDAP)_
- **Performance & security**
  - Add rate limiting, CAPTCHA for login/SSPR, vulnerability scanning, dependency checks, and penetration testing considerations.

## 7. Inputs Required From Stakeholders
The following information is necessary to implement environment-specific integrations safely:
1. **SMTP relay details** – host, port, TLS requirements, credentials, from/reply-to policy.
2. **LDAP/AD topology** – hostnames, base DNs, group naming conventions for role mapping, attribute mapping preferences.
3. **SMS provider selection** – preferred vendor (Twilio/Vonage/SMPP/HTTP gateway), sender ID policy, compliance requirements.
4. **Storage locations** – UNC paths or S3 buckets for artefacts, backups, and exports.
5. **Printing infrastructure** – target printer names or IPs for raw 9100 jobs, supported label sizes/DPI.
6. **Branding assets** – organization name, logos, color palette, default e-mail templates (CZ/EN), handover PDF branding.
7. **Security policies** – password complexity rules, session timeout standards, audit retention periods, captcha provider (if any).
8. **Reporting requirements** – exact report recipients, schedules, filtering thresholds (e.g., warranty expiry days).
9. **Observability endpoints** – OTEL collector URLs, authentication tokens, log retention policies.
10. **Support contacts** – helpdesk URLs, diagnostic package recipients, escalation procedures.

Documenting these inputs early will unblock the configuration-driven portions of the build and reduce rework during implementation.

## 8. Recommended execution order
To turn the blueprint into a production-ready release, iterate through the following milestone-oriented phases. Each phase is
designed to produce a demonstrable increment that can be validated with automated smoke tests and short stakeholder reviews.

1. **Foundations & Persistence**
   - Switch to SQL Server, ship the initial migration bundle, and stand up seed data for dictionaries, roles, modules, and feature
     flags.
   - Harden the configuration system, secrets storage, and background job infrastructure so subsequent features can rely on
     durable state and scheduling.
2. **Identity, RBAC, and Security**
   - Light up ASP.NET Core Identity, session management, 2FA (TOTP/e-mail), SSPR, CAPTCHA, and policy-based authorization with
     scoped data filters.
   - Deliver the first pass of the onboarding wizard to exercise authentication, SMTP test, and LDAP dry-run flows end to end.
3. **Core Inventory Workflows**
   - Complete server/network/workstation CRUD, retire/restore, comments, audit diffing, VLAN/IP enforcement, and scoped admin
     behaviour.
   - Implement handover PDFs/e-mail notifications, label management with ZPL/PDF rendering, and export/import jobs with dry-run.
4. **Operations & Integrations**
   - Finish connectors (SMTP, SMS, LDAP/OIDC, Webhooks hotové; doplnit SFTP/FTPS, Storage, Printing, rozšířenou Observability a Zabbix) s health reportingem a
     správou tajemství.
   - Build the Updates, Backup & Restore, Reports & Schedules, and Observability dashboards/alerting modules plus widgets.
5. **Frontend completion & UX polish**
   - Deliver the full React/Vite admin app, dark/light theming, contextual help, manual viewer, and feature-specific pages.
   - Add Playwright tests and align UI copy with terminology in the specification.
6. **Packaging, CI/CD, and Release Enablement**
   - Finalize PowerShell installers, builder/merger scripts, All-in-One ZIP with SHA256, GitHub Actions pipelines, release notes,
     manual PDFs, and smoke tests ready for acceptance.

## 9. Immediate actions possible without stakeholder input
While waiting for environment-specific details, the following engineering work can start immediately inside the repository:

- Replace EF Core InMemory with SQL Server LocalDB/SQL Express configuration and generate baseline migrations.
- Implement ASP.NET Core Identity with built-in local authentication, password policies, and TOTP 2FA using OtpNet.
- Rozšířit React frontend o onboarding wizard, help centrum a pokročilé inventární filtry/batch akce nad již existujícími stránkami.
- Build audit logging infrastructure (entity change diffing, history endpoints) and wire it into existing CRUD handlers.
- Implement label template storage, QuestPDF previews, and ZPL generation utilities with unit tests.
- Set up Quartz.NET with tables for scheduled jobs and stub processors for LDAP sync, report scheduling, export, and backup jobs.
- Draft PowerShell installer scaffolding to create IIS App Pool/sites and prepare configuration placeholders.

These deliverables unblock downstream work and prove the end-to-end tooling without requiring external credentials.

## 10. Items waiting on stakeholder input
Once the engineering baseline above is in place, integrate the environment-specific values listed in Section 7. A consolidated
tracking table should be maintained in the project wiki (or issue tracker) so that each dependency is assigned an owner and due
date. During implementation, guard production-only settings behind feature flags and provide sensible development defaults to
keep local environments functional while inputs are pending.
