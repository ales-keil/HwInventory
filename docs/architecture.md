# HW Inventory – Architecture Skeleton (v1)

This document captures the initial .NET-based architecture that replaces the earlier Python FastAPI prototype. The structure aligns with the Codex implementation brief and provides a foundation for subsequent feature work.

## Projects Overview

* **HWInventory.Domain** – Entities and enums representing inventory assets, audit logs, RBAC objects, connectors, label templates, maintenance artefacts (backup jobs/schedules), export/import jobs, and settings. Entities derive from `AuditableEntity` to support audit metadata and soft-state transitions.
* **HWInventory.Application** – Shared contracts (e.g., `IAppDbContext`), paging helpers, and filter descriptors. Application services can depend on these abstractions without referencing ASP.NET Core directly.
* **HWInventory.Infrastructure** – EF Core persistence layer with entity configurations, SQL Server/InMemory providers, DI extensions, and seeded baseline data (roles, dictionaries, feature modules).
* **HWInventory.Api** – ASP.NET Core Web API hosting controllers, swagger, health endpoints, label rendering helpers (QuestPDF), observability configuration, connector management, maintenance endpoints for backup/restore flows, a rozhraní `/api/exports/*` a `/api/imports/*` pro asynchronní exportní a importní joby.

## Persistence

* EF Core `AppDbContext` manages inventory tables and owned collections for VLAN/IP assignments.
* Seed data creates baseline dictionaries (environment, WSUS priority, OS, server roles, workstation types, VLAN, locations) and feature modules (labels, reports, Zabbix).
* Database provider defaults to InMemory when no connection string is configured; relational providers trigger migrations when available.

## API Highlights

* Controllers implement pagination with `X-Total-Count` and RFC5988 `Link` headers (limited to simple next/prev semantics for now).
* Servers/NetworkDevices/Workstations expose CRUD + retire/restore flows to mirror the specification.
* Workstation handover endpoint vytváří schvalovací požadavek – novému vlastníkovi odešle e-mail s PDF předávacím protokolem a tlačítky „Přijmout“/„Odmítnout“ a po potvrzení automaticky přepíše vlastníka; Super Admin může převod vynutit bez schválení.
* Modules API toggles feature flags to support optional components (labels, reports, Zabbix, etc.).
* Dashboard summary aggregates counts and latest audit entries.
* Labels controller manages templates, renders sample PDF previews via QuestPDF, and captures print job requests (persisted for auditing/queue processing).
* Maintenance controller (`/api/maintenance/*`) queues backups (full/partial), stores AES-256 encrypted artefacts, exposes history and integrity checks, and manages scheduled backups via Quartz.
* Exports controller (`/api/exports/*`) přijímá exportní požadavky, ukládá je do fronty, generuje CSV soubory (Servers/NetworkDevices/Workstations/Dictionaries/AuditLogs), posílá volitelné notifikace a vystavuje artefakt ke stažení.
* Updates controller (`/api/updates`) přijímá ZIP/PKG balíčky, provádí kontrolu SHA256, volitelně spouští zálohu, extrahuje obsah do staging adresáře, uchovává manifest/logy a deleguje zpracování na Quartz job `UpdatePackageProcessor`.

## Next Steps

* Introduce authentication/authorization middleware, policy-based enforcement, and LDAP/OIDC connectors.
* Replace placeholder installer with IIS-aware deployment automation and All-in-One packaging scripts.
* Expand audit logging to capture diffs and export audit events.
* Implement background job processing for print jobs, backup/report schedules, a rozšířené import/export pipelines (Quartz wiring now obsluhuje štítky, zálohy, exporty i importy — zbývá dopracovat validace, konfliktní logiku a mapování).
* Build the React admin UI with module toggles, settings wizard, and dark/light theming.
