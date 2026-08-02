# Codex Usage Meter for Windows

[![CI](https://github.com/aesgalexis/codex-usage-meter-windows/actions/workflows/ci.yml/badge.svg)](https://github.com/aesgalexis/codex-usage-meter-windows/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)](https://github.com/aesgalexis/codex-usage-meter-windows)

A small, privacy-friendly Windows tray application that shows your latest Codex usage limit at a glance.

![Codex Usage Meter tray menu](docs/assets/tray-menu.png)

> [!IMPORTANT]
> This is an early development version. The app reads rate-limit snapshots written by Codex to local session files. That file format is not a documented public API and may change in future Codex versions.

## Features

- Shows the available and used Codex percentages in the Windows notification area.
- Displays the limit reset date and time in your local timezone.
- Uses a green, amber, or red indicator according to the remaining usage.
- Reacts to Codex session changes almost immediately, with a 30-second fallback refresh.
- Offers persistent notifications for integer percentage changes, 50/75/90% usage thresholds, and limit resets.
- Opens the Windows setting used to keep its icon permanently visible.
- Uses a stable native notification-area identity so Windows can retain that visibility choice across upgrades.
- Optionally starts with Windows.
- Runs locally without account credentials, analytics, or telemetry from this app.

## Installation

### Portable release

Download `CodexUsageMeter-Setup-win-x64.exe` from [Releases](https://github.com/aesgalexis/codex-usage-meter-windows/releases) and run it. The installer works per user, does not require administrator privileges, and keeps a stable location for future upgrades.

The portable `CodexUsageMeter-win-x64.zip` remains available for users who prefer not to install the app.

Published builds are self-contained: the destination PC does not need the .NET runtime installed.

### WinGet

The package is being prepared for the Windows Package Manager community catalog. Once Microsoft accepts the submission, installation and upgrades will use:

```powershell
winget install --id aesgalexis.CodexUsageMeter --exact
winget upgrade --id aesgalexis.CodexUsageMeter --exact
```

### Using the app

The app has no main window. After starting it, look for the circular indicator next to the Windows clock; it may initially appear under the hidden-icons arrow.

- **Left-click:** show the current usage summary.
- **Right-click:** refresh, open the Codex sessions folder, enable startup with Windows, or exit.
- **Show always in the system tray:** opens the Windows taskbar setting where you can promote Codex Usage Meter out of the hidden-icons menu.
- **Notifications:** enables or disables percentage changes, individual warning thresholds, and reset notices.

If the icon reports that no data is available, run at least one Codex task and select **Update now**.

## How it works

Codex writes session events below `%USERPROFILE%\.codex\sessions`. The app searches recent `.jsonl` session files, reads only their tail, and extracts the latest `rate_limits` snapshot. It does not read the files to reconstruct conversations.

```text
Codex local sessions
        ↓
CodexSessionUsageProvider
        ↓
UsageSnapshot
        ↓
Windows tray indicator
```

The reader is isolated behind an `IUsageProvider` interface so it can be replaced if OpenAI publishes a suitable supported API.

## Privacy and security

- No OpenAI password, API key, or ChatGPT token is requested.
- Session contents and usage values never leave the computer.
- This project does not include its own analytics or telemetry.
- The optional start-with-Windows setting is stored in the current user's standard Windows `Run` registry key.
- Notification preferences are stored in `%LOCALAPPDATA%\CodexUsageMeter\settings.json`.

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

## Requirements

For a published build:

- Windows 10 or Windows 11, x64.
- Codex installed and at least one recent Codex task.

For development:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- Git.

## Development

Clone and run:

```powershell
git clone https://github.com/aesgalexis/codex-usage-meter-windows.git
cd codex-usage-meter-windows
dotnet run --project .\src\CodexUsageMeter.App\CodexUsageMeter.App.csproj
```

Build and verify:

```powershell
dotnet build .\CodexUsageMeter.sln --configuration Release
dotnet run --project .\tests\CodexUsageMeter.Tests\CodexUsageMeter.Tests.csproj --configuration Release
```

Create a self-contained portable build:

```powershell
dotnet publish .\src\CodexUsageMeter.App\CodexUsageMeter.App.csproj `
  -p:PublishProfile=win-x64 `
  --output .\artifacts\win-x64
```

## Project structure

```text
src/
  CodexUsageMeter.App/             WPF application and tray integration
  CodexUsageMeter.Core/            Stable models and provider contract
  CodexUsageMeter.Infrastructure/  Local Codex session reader
tests/
  CodexUsageMeter.Tests/           Dependency-free parser/provider checks
```

## Roadmap

- Desktop widget anchored to any screen corner.
- Native Windows toast notifications and richer scheduling controls.
- Installer and automatic updates.
- ARM64 builds.
- Additional usage providers if a supported API becomes available.

Ideas and contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

## Disclaimer

This is an independent open-source project and is not an official OpenAI product. Codex and OpenAI are trademarks of OpenAI.

## License

Distributed under the [MIT License](LICENSE).
