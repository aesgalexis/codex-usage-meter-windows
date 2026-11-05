[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [Parameter(Mandatory)]
    [string]$InstallerPath,
    [string]$OutputDirectory = 'packaging/winget',
    [datetime]$ReleaseDate = [datetime]::UtcNow.Date
)

$ErrorActionPreference = 'Stop'
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.PSIsContainer -or $installer.Name -ne 'CodexUsageMeter-Setup-win-x64.exe') {
    throw 'Provide the published CodexUsageMeter-Setup-win-x64.exe installer.'
}
$installedVersion = $installer.VersionInfo.ProductVersion.Trim()
if ($installedVersion -ne $Version) {
    throw "Installer version $installedVersion does not match requested version $Version."
}
$checksum = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash
$directory = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $Version
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$date = $ReleaseDate.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
$repo = 'https://github.com/aesgalexis/codex-usage-meter-windows'

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json
PackageIdentifier: aesgalexis.CodexUsageMeter
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.12.0
"@ | Set-Content -LiteralPath (Join-Path $directory 'aesgalexis.CodexUsageMeter.yaml') -Encoding utf8

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json
PackageIdentifier: aesgalexis.CodexUsageMeter
PackageVersion: $Version
InstallerType: inno
Scope: user
UpgradeBehavior: install
ReleaseDate: $date
Installers:
  - Architecture: x64
    MinimumOSVersion: 10.0.17763.0
    InstallerUrl: $repo/releases/download/v$Version/CodexUsageMeter-Setup-win-x64.exe
    InstallerSha256: $checksum
    AppsAndFeaturesEntries:
      - DisplayName: Codex Usage Meter for Windows
        Publisher: aesgalexis
        ProductCode: '{9B7B432F-36A6-4E80-AB97-6D020F2F0CAB}_is1'
ManifestType: installer
ManifestVersion: 1.12.0
"@ | Set-Content -LiteralPath (Join-Path $directory 'aesgalexis.CodexUsageMeter.installer.yaml') -Encoding utf8

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json
PackageIdentifier: aesgalexis.CodexUsageMeter
PackageVersion: $Version
PackageLocale: en-US
Publisher: aesgalexis
PublisherUrl: https://github.com/aesgalexis
PublisherSupportUrl: $repo/issues
Author: aesgalexis and contributors
PackageName: Codex Usage Meter for Windows
PackageUrl: $repo
License: MIT
LicenseUrl: $repo/blob/main/LICENSE
Copyright: Copyright (c) 2026 Codex Usage Meter contributors
ShortDescription: Windows tray app for viewing local Codex usage limits.
Description: >-
  A small, privacy-friendly Windows tray application that reads local Codex
  usage and task-state metadata. It displays weekly and rolling five-hour
  limits through widgets, a tray icon and a multi-display taskbar indicator
  without account credentials, analytics, or telemetry.
Moniker: codex-usage-meter
Tags:
  - codex
  - openai
  - system-tray
  - usage
  - windows
ReleaseNotesUrl: $repo/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: 1.12.0
"@ | Set-Content -LiteralPath (Join-Path $directory 'aesgalexis.CodexUsageMeter.locale.en-US.yaml') -Encoding utf8

Write-Output $directory
