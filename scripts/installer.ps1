param(
    [string]$PublishPath = "publish",
    [string]$SiteName = "HWInventory",
    [string]$AppPoolName = "HWInventoryPool",
    [string]$AppPoolIdentity = "ApplicationPoolIdentity",
    [string]$SqlConnectionString = "Server=localhost;Database=HWInventory;Trusted_Connection=True;TrustServerCertificate=True",
    [string]$SeedAdminEmail = "admin@localhost",
    [string]$SeedAdminPassword = "ChangeMe!123!",
    [switch]$SkipIisProvisioning,
    [switch]$SkipMigrations
)

Import-Module WebAdministration -ErrorAction Stop

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -Path $Path)) {
        Write-Host "[HWInventory] Creating directory $Path" -ForegroundColor Green
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Update-AppSettings {
    param(
        [string]$ConfigPath,
        [string]$ConnectionString,
        [string]$AdminEmail,
        [string]$AdminPassword
    )

    $json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    if (-not $json.ConnectionStrings) {
        $json | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue @{ }
    }

    $json.ConnectionStrings.DefaultConnection = $ConnectionString

    if (-not $json.SeedAdmin) {
        $json | Add-Member -NotePropertyName SeedAdmin -NotePropertyValue @{ }
    }

    $json.SeedAdmin.Email = $AdminEmail
    $json.SeedAdmin.Password = $AdminPassword

    if (-not $json.FeatureFlags) {
        $json | Add-Member -NotePropertyName FeatureFlags -NotePropertyValue @{ }
    }

    if (-not $json.FeatureFlags.ContainsKey('MinimalMode')) {
        $json.FeatureFlags.MinimalMode = $true
    }

    $json | ConvertTo-Json -Depth 6 | Out-File $ConfigPath -Encoding UTF8
    Write-Host "[HWInventory] Updated $ConfigPath with SQL Server connection string" -ForegroundColor Green
}

function Grant-AppPermissions {
    param([string]$Path, [string]$Identity)

    Write-Host "[HWInventory] Granting Modify permissions to $Identity on $Path" -ForegroundColor Green
    icacls $Path /grant "$Identity":(OI)(CI)M | Out-Null
}

function Ensure-SqlDatabase {
    param([string]$ConnectionString)

    Add-Type -AssemblyName System.Data
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
    if ([string]::IsNullOrWhiteSpace($builder.InitialCatalog)) {
        Write-Warning "[HWInventory] Connection string must specify a database name (Initial Catalog). Skipping automatic database creation."
        return
    }

    $databaseName = $builder.InitialCatalog
    $builder.InitialCatalog = "master"
    $masterConnection = $builder.ConnectionString

    $query = "IF DB_ID(N'$databaseName') IS NULL CREATE DATABASE [$databaseName];"
    $connection = New-Object System.Data.SqlClient.SqlConnection $masterConnection
    $command = $connection.CreateCommand()
    $command.CommandText = $query

    try {
        $connection.Open()
        $command.ExecuteNonQuery() | Out-Null
        Write-Host "[HWInventory] Database '$databaseName' ensured." -ForegroundColor Green
    }
    catch {
        Write-Warning "[HWInventory] Failed to ensure database '$databaseName': $($_.Exception.Message)"
    }
    finally {
        $connection.Dispose()
    }
}

function Invoke-Migrations {
    param([string]$PublishPath)

    $dllPath = Join-Path $PublishPath "HWInventory.Api.dll"
    if (-not (Test-Path $dllPath)) {
        Write-Warning "[HWInventory] Unable to locate HWInventory.Api.dll in $PublishPath. Skipping EF Core migrations."
        return
    }

    Write-Host "[HWInventory] Applying EF Core migrations via HWInventory.Api.dll" -ForegroundColor Cyan
    $process = Start-Process -FilePath "dotnet" -ArgumentList "`"$dllPath`" --apply-migrations" -NoNewWindow -PassThru -Wait -ErrorAction SilentlyContinue

    if ($process.ExitCode -ne 0) {
        Write-Warning "[HWInventory] Migration process exited with code $($process.ExitCode). Review application logs for details."
    }
    else {
        Write-Host "[HWInventory] Database migrations completed successfully." -ForegroundColor Green
    }
}

Ensure-Directory -Path $PublishPath
Ensure-Directory -Path (Join-Path $PublishPath "logs")
Ensure-Directory -Path (Join-Path $PublishPath "updates")

$webConfigPath = Join-Path $PublishPath "appsettings.Production.json"
if (-not (Test-Path $webConfigPath)) {
    $webConfigPath = Join-Path $PublishPath "appsettings.json"
}

if (Test-Path $webConfigPath) {
    Update-AppSettings -ConfigPath $webConfigPath -ConnectionString $SqlConnectionString -AdminEmail $SeedAdminEmail -AdminPassword $SeedAdminPassword
    Ensure-SqlDatabase -ConnectionString $SqlConnectionString
    if (-not $SkipMigrations) {
        Invoke-Migrations -PublishPath $PublishPath
    }
}
else {
    Write-Warning "[HWInventory] Unable to locate appsettings file in $PublishPath. Please update the connection string manually."
}

if (-not $SkipIisProvisioning) {
    Write-Host "[HWInventory] Configuring IIS application pool $AppPoolName" -ForegroundColor Cyan

    if (-not (Get-Item "IIS:\AppPools\$AppPoolName" -ErrorAction SilentlyContinue)) {
        New-Item "IIS:\AppPools\$AppPoolName" -Force | Out-Null
    }

    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value $AppPoolIdentity

    Write-Host "[HWInventory] Ensuring IIS site $SiteName" -ForegroundColor Cyan
    if (-not (Get-Item "IIS:\Sites\$SiteName" -ErrorAction SilentlyContinue)) {
        New-Item "IIS:\Sites\$SiteName" -Bindings @{ protocol = "http"; bindingInformation = "*:8080:" } -PhysicalPath $PublishPath | Out-Null
    }

    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName

    Grant-AppPermissions -Path $PublishPath -Identity "IIS AppPool\$AppPoolName"
    Grant-AppPermissions -Path (Join-Path $PublishPath "logs") -Identity "IIS AppPool\$AppPoolName"
    Grant-AppPermissions -Path (Join-Path $PublishPath "updates") -Identity "IIS AppPool\$AppPoolName"
}

Write-Host "[HWInventory] Deployment artifacts staged successfully. Start the IIS site to serve the application." -ForegroundColor Cyan
