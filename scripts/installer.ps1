param(
    [string]$PublishPath = "publish",
    [string]$SiteName = "HWInventory",
    [string]$AppPoolName = "HWInventoryPool",
    [string]$AppPoolIdentity = "ApplicationPoolIdentity",
    [string]$SqlConnectionString = "Server=localhost;Database=HWInventory;Trusted_Connection=True;TrustServerCertificate=True",
    [string]$SeedAdminEmail = "admin@localhost",
    [string]$SeedAdminPassword,
    [string]$ApiSourcePath,
    [string]$WebSourcePath,
    [switch]$SkipCopy,
    [switch]$SkipApiCopy,
    [switch]$SkipWebCopy,
    [switch]$DisablePasswordEncryption,
    [switch]$SkipIisProvisioning,
    [switch]$SkipMigrations
)

$script:scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $ApiSourcePath) {
    $ApiSourcePath = Join-Path $script:scriptRoot "..\publish\api"
}

if (-not $WebSourcePath) {
    $WebSourcePath = Join-Path $script:scriptRoot "..\publish\web"
}

function Resolve-OptionalPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    try {
        return (Resolve-Path $Path -ErrorAction Stop).Path
    }
    catch {
        return $Path
    }
}

$ApiSourcePath = Resolve-OptionalPath -Path $ApiSourcePath
$WebSourcePath = Resolve-OptionalPath -Path $WebSourcePath

function New-RandomPassword {
    param([int]$Length = 20)

    $bytes = New-Object byte[] ($Length)
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $base64 = [Convert]::ToBase64String($bytes)
    return $base64.Substring(0, $Length)
}

if (-not $PSBoundParameters.ContainsKey('SeedAdminPassword')) {
    $SeedAdminPassword = New-RandomPassword -Length 24
    Write-Host "[HWInventory] Generated random seed admin password: $SeedAdminPassword" -ForegroundColor Yellow
    Write-Host "[HWInventory] Store this password securely and change it after the first login." -ForegroundColor Yellow
}

Import-Module WebAdministration -ErrorAction Stop

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -Path $Path)) {
        Write-Host "[HWInventory] Creating directory $Path" -ForegroundColor Green
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Clear-DirectoryContents {
    param(
        [string]$Path,
        [string[]]$Exclude = @()
    )

    if (-not (Test-Path $Path)) {
        return
    }

    Get-ChildItem -Path $Path -Force | ForEach-Object {
        if ($Exclude -and $Exclude -contains $_.Name) {
            return
        }

        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Copy-DirectoryContents {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$Description
    )

    if (-not (Test-Path $Source)) {
        Write-Warning "[HWInventory] Skipping copy for $Description – source path $Source was not found."
        return $false
    }

    Ensure-Directory -Path $Destination
    $exclusions = @()
    if ($Description -eq "API") {
        $exclusions = @('logs', 'updates')
    }

    Clear-DirectoryContents -Path $Destination -Exclude $exclusions

    Get-ChildItem -Path $Source -Force | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $Destination -Recurse -Force
    }

    Write-Host "[HWInventory] Copied $Description artefacts from $Source to $Destination" -ForegroundColor Green
    return $true
}

function Protect-SeedAdminPassword {
    param([string]$Password)

    $isWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
    if (-not $isWindows) {
        Write-Warning "[HWInventory] Password encryption is only available on Windows. Storing password in plain text."
        return $null
    }

    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Password)
        $protected = [System.Security.Cryptography.ProtectedData]::Protect($bytes, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
        return [Convert]::ToBase64String($protected)
    }
    catch {
        Write-Warning "[HWInventory] Failed to protect seed admin password: $($_.Exception.Message). Falling back to plain text."
        return $null
    }
}

function Update-AppSettings {
    param(
        [string]$ConfigPath,
        [string]$ConnectionString,
        [string]$AdminEmail,
        [string]$AdminPassword,
        [switch]$EncryptPassword
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

    $json.SeedAdmin.PSObject.Properties.Remove('PasswordProtected') | Out-Null
    $json.SeedAdmin.PSObject.Properties.Remove('Password') | Out-Null

    $protected = $null
    if ($EncryptPassword) {
        $protected = Protect-SeedAdminPassword -Password $AdminPassword
    }

    if ($protected) {
        $json.SeedAdmin | Add-Member -NotePropertyName PasswordProtected -NotePropertyValue $protected
    }
    else {
        $json.SeedAdmin | Add-Member -NotePropertyName Password -NotePropertyValue $AdminPassword
    }

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

if (-not $SkipCopy) {
    if (-not $SkipApiCopy) {
        Copy-DirectoryContents -Source $ApiSourcePath -Destination $PublishPath -Description "API"
    }

    if (-not $SkipWebCopy) {
        $webTarget = Join-Path $PublishPath "web"
        Copy-DirectoryContents -Source $WebSourcePath -Destination $webTarget -Description "React build"
    }
}

Ensure-Directory -Path (Join-Path $PublishPath "logs")
Ensure-Directory -Path (Join-Path $PublishPath "updates")

$webConfigPath = Join-Path $PublishPath "appsettings.Production.json"
if (-not (Test-Path $webConfigPath)) {
    $webConfigPath = Join-Path $PublishPath "appsettings.json"
}

if (Test-Path $webConfigPath) {
    $encryptPassword = -not $DisablePasswordEncryption
    Update-AppSettings -ConfigPath $webConfigPath -ConnectionString $SqlConnectionString -AdminEmail $SeedAdminEmail -AdminPassword $SeedAdminPassword -EncryptPassword:$encryptPassword
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
