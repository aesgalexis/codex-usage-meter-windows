# Contributing

Thanks for helping improve Codex Usage Meter for Windows.

## Before opening an issue

- Search existing issues first.
- Use the bug-report template and include the Windows build, app version, Codex client, installation method, and steps to reproduce.
- Describe what the tray menu shows, but do not attach raw Codex session files because they may contain private conversation data.

## Development workflow

1. Install the .NET 10 SDK, fork the repository, and create a focused branch.
2. Keep parsing and data-source logic outside the WPF application project.
3. Add or update a meaningful check in `tests/CodexUsageMeter.Tests` for a reproducible functional change. Use synthetic fixtures and an explicit temporary Codex home; do not read the contributor's real sessions.
4. Run the Release build and checks:

   ```powershell
   dotnet build .\CodexUsageMeter.sln --configuration Release
   dotnet run --project .\tests\CodexUsageMeter.Tests\CodexUsageMeter.Tests.csproj --configuration Release
   ```

5. For window behavior, also run the test command with `-- --window-checks` on an interactive Windows desktop. Check affected controls manually in English and Spanish and at the relevant display scales.
6. Review the diff, then open a pull request explaining the user-visible result, compatibility implications, and what was checked. Distinguish automated checks, native window checks, and actual use; record any checks that could not run.

Never commit real session files, credentials, access tokens, or personal usage data. Use minimal synthetic JSON fixtures.

For a release, follow the [release checklist](docs/release-checklist.md). Group confirmed fixes into one verified release rather than publishing a separate version for each finding. If the relevant checks pass and no further failure is reproducible, retain the current version until there is new evidence.

`tools/Test-Installer.ps1` changes the current user's installation and startup registration. Run it only in CI or a clean, disposable Windows profile with no existing app installation, settings, startup entry, or running copy. Its opt-in switch does not make an active personal profile suitable for those checks.
