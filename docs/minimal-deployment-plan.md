# Minimalní roadmapa pro lokální nasazení (IIS + SQL Server)

Cílem je dostat řešení do stavu, kdy lze na IIS nasadit API + React administraci,
pracovat pouze s lokálními účty a obsluhovat evidence **Servers / Network devices / Workstations**.

| Krok | Stav | Popis |
| --- | --- | --- |
| 1 | ✅ Hotovo | Zpřístupnit čistě lokální přihlášení – přidat REST endpointy pro přihlášení/odhlášení, vytvořit výchozího Super Admina při seedování databáze a vše zdokumentovat. |
| 2 | ✅ Hotovo | Doplnit React přihlašovací obrazovku (formulář + volání `/api/auth/login`), session guard pro ochranu administračních stránek a možnost odhlášení. |
| 3 | ✅ Hotovo | Umožnit průvodci prvního spuštění pokračovat i bez externích konektorů (LDAP/OIDC/SMS) – přidat přepínač „Lokální režim“ a aktualizovat readiness kontrolu. |
| 4 | ⬜ | Zjednodušit konfiguraci – připravit `appsettings.Production.json` s ukázkovým connection stringem, vypnout nepotřebné moduly a doplnit README o krátký návod pro čistě lokální nasazení. |
| 5 | ⬜ | Doplnit inicializační skript (PowerShell) pro vytvoření SQL databáze a naplnění seed dat (číselníky, výchozí admin heslo). |
| 6 | ✅ Hotovo | Provést end-to-end ověření CRUD operací pro Servery/Síťová zařízení/Pracovní stanice – opravit případné chyby v API nebo formulářích. Pokryto integračními testy v `HWInventory.Api.IntegrationTests`. |
| 7 | ⬜ | Vyčistit UI od nedokončených modulů – skrýt pokročilé sekce (Reports, Updates, Observability…) za feature flag, aby minimální nasazení působilo konzistentně. |
| 8 | ⬜ | Připravit základní smoke test (PowerShell/Postman kolekce) ověřující přihlášení, CRUD operace a audit log. |
| 9 | ⬜ | Vylepšit `scripts/installer.ps1` tak, aby ve výchozím režimu publikoval pouze API + React build a doplnil inicializační heslo do `appsettings.json`. |
| 10 | ⬜ | Dokončit dokumentaci pro provoz – krátký PDF/Markdown návod „Jak nasadit minimální verzi“, checklist před spuštěním a seznam kroků po prvním přihlášení (změna hesla, základní číselníky). |

> Poznámka: Do budoucna lze tyto kroky rozšířit směrem k plné specifikaci (LDAP, SSO, štítky, reporty atd.),
ale pro rychlé nasazení stačí výše uvedených 10 úkolů postupně dokončit.
