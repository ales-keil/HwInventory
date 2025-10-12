param(
    [ValidateSet('MERGE','ALLINONE')]
    [string]$Mode = 'ALLINONE',
    [string]$Output = "artifacts",
    [switch]$SkipWebBuild
)

$ErrorActionPreference = 'Stop'
$script:root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $script:root '..')
Set-Location $repoRoot

function Invoke-Step {
    param(
        [string]$Message
    )
    Write-Host "[builder] $Message"
}

Invoke-Step "Restoring dotnet dependencies"
dotnet restore src/api/HWInventory.sln

Invoke-Step "Building backend"
dotnet publish src/api/src/HWInventory.Api/HWInventory.Api.csproj -c Release -o publish/api

if (-not $SkipWebBuild) {
    Invoke-Step "Installing npm dependencies"
    pushd src/web
    npm install
    npm run build --if-present
    popd
}

Invoke-Step "Collecting artefacts"
New-Item -ItemType Directory -Force -Path publish/web | Out-Null
Copy-Item src/web/dist/* publish/web/ -Recurse -Force -ErrorAction SilentlyContinue

if ($Mode -eq 'MERGE') {
    Invoke-Step "MERGE mode completed"
    return
}

Invoke-Step "Creating All-in-One archive"
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$timestamp = Get-Date -Format 'yyyyMMddHHmmss'
$zipPath = Join-Path $Output "HWInventory_AllInOne_$timestamp.zip"
Compress-Archive -Path publish/* -DestinationPath $zipPath -Force

Invoke-Step "Generating checksum"
(Get-FileHash $zipPath -Algorithm SHA256).Hash | Out-File ("$zipPath.sha256") -Encoding ascii

Invoke-Step "Builder completed"
