param(
    [string]$PublishPath = "publish",
    [string]$SiteName = "HWInventory",
    [string]$AppPoolName = "HWInventoryPool",
    [string]$AppPoolIdentity = "ApplicationPoolIdentity",
    [string]$SqlConnectionString = "Server=localhost;Database=HWInventory;Trusted_Connection=True;TrustServerCertificate=True",
    [switch]$SkipIisProvisioning
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
    param([string]$ConfigPath, [string]$ConnectionString)

    $json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    if (-not $json.ConnectionStrings) {
        $json | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue @{ }
    }

    $json.ConnectionStrings.DefaultConnection = $ConnectionString
    $json | ConvertTo-Json -Depth 6 | Out-File $ConfigPath -Encoding UTF8
    Write-Host "[HWInventory] Updated $ConfigPath with SQL Server connection string" -ForegroundColor Green
}

function Grant-AppPermissions {
    param([string]$Path, [string]$Identity)

    Write-Host "[HWInventory] Granting Modify permissions to $Identity on $Path" -ForegroundColor Green
    icacls $Path /grant "$Identity":(OI)(CI)M | Out-Null
}

Ensure-Directory -Path $PublishPath
Ensure-Directory -Path (Join-Path $PublishPath "logs")
Ensure-Directory -Path (Join-Path $PublishPath "updates")

$webConfigPath = Join-Path $PublishPath "appsettings.Production.json"
if (-not (Test-Path $webConfigPath)) {
    $webConfigPath = Join-Path $PublishPath "appsettings.json"
}

if (Test-Path $webConfigPath) {
    Update-AppSettings -ConfigPath $webConfigPath -ConnectionString $SqlConnectionString
} else {
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

Write-Host "[HWInventory] Deployment artifacts staged successfully. Run dotnet ef database update or start the web application to apply migrations." -ForegroundColor Cyan
