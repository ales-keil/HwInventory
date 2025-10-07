# HW Inventory Platform – Current Status

_Last updated: 2025-10-07T18:44:56Z_

## Implemented Capabilities
- Monorepo scaffold targeting .NET 8 (API) and React/Vite (admin UI shell) with Tailwind-based dark/light theming.
- ASP.NET Core backend structured into Domain, Application, Infrastructure, and Api layers with SQL Server persistence, Entity Framework Core migrations, and seeded baseline dictionaries, feature modules, and RBAC role definitions.
- Identity foundation using ASP.NET Core Identity with TOTP 2FA helpers, policy-based authorization tied to the mandated RBAC roles, audit log entities that capture change diffs, and Quartz.NET background job wiring (initial label print processor).
- Label rendering services that produce QuestPDF previews and ZPL output, plus REST endpoints for inventory CRUD (servers, network devices, workstations), dictionaries, modules, dashboard summary, audit exploration, and TOTP enrollment.
- PowerShell installer scaffold that provisions IIS resources and patches connection strings, alongside documentation describing the remaining backlog and execution plan.

## Outstanding Work (High-Level)
- Harden authentication & RBAC: scoped authorization policies, LDAP/AD integration, SSO/OIDC, SSPR with CAPTCHA/SMS, session management, and audit coverage for all security changes.
- Complete domain workflows: workstation handover PDF/e-mail process, import/export with dry-run and async jobs, reporting & schedules, connectors catalogue, backup/restore, updates module, observability endpoints, and full audit diff surfacing.
- Build the full React admin experience: onboarding wizard, inventory tables with filters/batch actions, settings subsections, label template editor, reports dashboards, help center/manual integration, and modules toggles.
- Deliver packaging & ops tooling: All-in-One builder, CI/CD pipelines, release notes/manual PDFs, smoke/regression tests, and artefact bundling with checksums.
- Gather stakeholder inputs for SMTP, LDAP, SMS, storage, printing, branding, security policies, reporting thresholds, observability, and support contacts to configure environment-specific connectors.

See [`docs/implementation-plan.md`](implementation-plan.md) for the detailed backlog and sequencing guidance.
