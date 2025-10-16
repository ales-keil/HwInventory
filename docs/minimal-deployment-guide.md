# Minimální nasazení HW Inventory na IIS se SQL Serverem

Tento dokument popisuje doporučený postup pro rychlé nasazení "minimal mode" verze
HW Inventory, která vyžaduje pouze lokální účty a poskytuje evidenci Servers,
Network Devices a Workstations. Postup vychází z PowerShell skriptů a konfigurace
v repozitáři k datu vydání.

## 1. Předpoklady

- Windows Server s IIS (role Web Server + WebSockets, Static Content, HTTP Redirection).
- IIS Application Pool s .NET CLR "No Managed Code" a povoleným 32/64bit dle
  potřeby.
- SQL Server (Express/Standard) s účtem, který může vytvářet databáze.
- PowerShell 5.1+ s povoleným skriptem `Set-ExecutionPolicy RemoteSigned`.
- Zip balík vytvořený pomocí `scripts/builder.ps1` nebo `dotnet publish` + `npm run build`.

## 2. Publikace artefaktů

1. Na build stroji spusťte:
   - `dotnet publish src/api/src/HWInventory.Api/HWInventory.Api.csproj -c Release -o publish/api`
   - `cd src/web && npm install && npm run build` (výstup ve `dist/`).
2. Zabalte obsah složek `publish/api` a `src/web/dist` (nebo použijte výstup builderu).
3. Přeneste balík na cílový server.

## 3. Konfigurace appsettings

1. Otevřete `src/api/src/HWInventory.Api/appsettings.Production.json` a nastavte:
   - `ConnectionStrings:Default` na instanci SQL Serveru (SQL auth nebo Integrated).
   - `Authentication:Cookie:Domain` a `BaseUrl` na cílovou URL.
2. Zkontrolujte, že `FeatureFlags:MinimalMode` je `true` (pokud chcete pouze základní evidenci).

## 4. Spuštění instalačního skriptu

1. Na cílovém serveru rozbalte artefakty do dočasné složky, např. `C:\Deploy\HWInventory`.
2. Spusťte jako administrátor:
   ```powershell
   cd C:\Deploy\HWInventory\scripts
   .\installer.ps1 -SiteName "HWInventory" -AppPoolName "HWInventoryPool" -InstallPath "C:\inetpub\HWInventory" \
     -SqlServer "SERVER\INSTANCE" -DatabaseName "HWInventory" -SqlAuthType "Sql" -SqlUser "hwinv" -SqlPassword "<heslo>"
   ```
3. Skript provede:
   - vytvoření cílové složky a zkopírování API + React buildu,
   - vytvoření AppPoolu a webu,
   - aplikaci migrací a seed dat včetně generace šifrovaného hesla pro Super Admin.
4. Po dokončení skript vypíše cestu k souboru s informací o seed heslu. Heslo je
defaultně chráněno DPAPI a lze jej získat příkazem:
   ```powershell
   .\installer.ps1 -RetrieveSeedPassword -InstallPath "C:\inetpub\HWInventory"
   ```

## 5. Po nasazení

1. Ověřte, že web běží na zadané URL.
2. Přihlaste se jako seedovaný Super Admin (uživatelské jméno `admin@local`).
3. Okamžitě změňte heslo a zaznamenejte změnu do provozní dokumentace.
4. Zkontrolujte základní číselníky (Environment, OS, WorkstationType, DeviceType).
5. V menu Evidence vytvořte testovací záznamy pro Server, Network Device a Workstation.
6. Zkontrolujte auditní logy, že operace byly zaznamenány.

## 6. Smoke test

1. Spusťte `scripts/smoke-test.ps1 -BaseUrl "https://..." -AdminUser "admin@local" -AdminPassword "<nové heslo>"`.
2. Skript provede přihlášení, vytvoření a odstranění záznamů a ověří HTTP 200 odpovědi.
3. Pokud test uspěje, je prostředí připraveno pro základní provoz.

## 7. Další kroky

- Po ověření základního provozu doporučujeme pokračovat v implementačním plánu
  (`docs/implementation-plan.md`) a rozšiřovat funkcionalitu o konektory,
  štítky, reporting, aktualizace atd.
- Sledujte `docs/status-report.md` pro přehled o tom, které části specifikace jsou
  již implementovány.

---
Poslední aktualizace: 2024-06-01
