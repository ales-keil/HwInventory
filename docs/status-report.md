# HW Inventory Platform – Current Status

_Last updated: 2025-10-08T13:00:00Z_

## Implemented Capabilities
- Monorepo scaffold targeting .NET 8 (API) and React/Vite (admin UI shell) with Tailwind-based dark/light theming.
- ASP.NET Core backend structured into Domain, Application, Infrastructure, and Api layers with SQL Server persistence, Entity Framework Core migrations, and seeded baseline dictionaries, feature modules, and RBAC role definitions.
- Identity foundation using ASP.NET Core Identity with TOTP 2FA helpers, policy-based authorization tied to the mandated RBAC roles (including per-user location/department data scoping), audit log entities that capture change diffs, and Quartz.NET background job wiring (initial label print processor).
- Security integrations covering LDAP/AD connection tests and dry-runs, configurable OpenID Connect sign-in, password policy management, CAPTCHA validation, SMS-backed SSPR flows that revoke active sessions, auditované změny OIDC konfigurace i datových scopů a middleware, který sleduje aktivní relace uživatelů včetně vzdálených odhlášení, společně s React stránkou „Security Settings“ pro správu OIDC/LDAP/SSPR/CAPTCHA/SMS/politik hesel, mapování AD skupin na role, náhled auditních diffů a přehled aktivních relací s možností odhlášení.
- SMTP konektor s uložením tajemství, auditovanými změnami konfigurace, testovacím odesláním e-mailu a dedikovanou React stránkou „Email (SMTP)“, která zajišťuje úpravu host/port/TLS/přihlašovacích údajů, zobrazuje zdravotní stav a podporuje testování přímo z UI i průvodce prvním spuštěním.
- Katalog konektorů s REST API a React stránkou „Konektory“ zobrazující stav SMTP/SMS/LDAP/OIDC, podporující přepínání, testy (SMTP/SMS) a rotaci tajemství s auditními záznamy.
- First-run wizard skeleton with a dedicated „Bezpečnostní nastavení“ krok, který agreguje klíčové bezpečnostní formuláře (OIDC/LDAP/SSPR/CAPTCHA/SMS) a nově také SMTP konfiguraci přímo v průvodci onboardingem.
- Label rendering services that produce QuestPDF previews and ZPL output, plus REST endpoints for inventory CRUD (servers, network devices, workstations), dictionaries, modules, dashboard summary, audit exploration, and TOTP enrollment.
- PowerShell installer scaffold that provisions IIS resources and patches connection strings, alongside documentation describing the remaining backlog and execution plan.

## Outstanding Work (High-Level)
- Rozšířit security UX: napojit bezpečnostní krok na zbytek onboarding wizardu, dokončit SSO/OIDC hardening, doručit bezpečnostní reporting/dashboardy, vynucení politik hesel na Identity options a doplnit automatizované regresní testy.
- Complete remaining domain workflows: import/export with dry-run and async jobs, reporting & schedules, rozšířené konektory (SFTP/FTPS, webhooks, storage, printing), backup/restore, updates module, observability endpoints, and full audit diff surfacing for additional entities.
- Build the full React admin experience: onboarding wizard, inventory tables with filters/batch actions, settings subsections, label template editor, reports dashboards, help center/manual integration, and modules toggles.
- Deliver packaging & ops tooling: All-in-One builder, CI/CD pipelines, release notes/manual PDFs, smoke/regression tests, and artefact bundling with checksums.
- Gather stakeholder inputs for SMTP, LDAP, SMS, storage, printing, branding, security policies, reporting thresholds, observability, and support contacts to configure environment-specific connectors.

See [`docs/implementation-plan.md`](implementation-plan.md) for the detailed backlog and sequencing guidance.
