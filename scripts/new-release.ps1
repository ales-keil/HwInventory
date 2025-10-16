param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter()]
    [ValidateSet('Major', 'Minor', 'Patch')]
    [string]$ReleaseType = 'Minor',

    [Parameter()]
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),

    [Parameter()]
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs')
)

$ErrorActionPreference = 'Stop'

$templatePath = Join-Path $OutputDirectory 'release-notes-template.md'
if (-not (Test-Path $templatePath)) {
    throw "Release notes template not found at $templatePath."
}

$targetDirectory = Join-Path $OutputDirectory 'release-notes'
if (-not (Test-Path $targetDirectory)) {
    New-Item -ItemType Directory -Path $targetDirectory | Out-Null
}

$targetFile = Join-Path $targetDirectory ("release-notes-$Version.md")
if (Test-Path $targetFile) {
    throw "Release notes for version $Version already exist at $targetFile."
}

$content = Get-Content $templatePath -Raw
$content = $content.Replace('{{VERSION}}', $Version)
$content = $content.Replace('{{DATE}}', $Date)
$content = $content.Replace('{{RELEASE_TYPE}}', $ReleaseType)
$content = $content.Replace('{{REQUIRES_DB_MIGRATION}}', 'Ano/Ne')
$content = $content.Replace('{{API_COMPATIBILITY}}', 'Bez změny')

Set-Content -Path $targetFile -Value $content -Encoding UTF8

Write-Host "Release notes scaffold created at $targetFile"
