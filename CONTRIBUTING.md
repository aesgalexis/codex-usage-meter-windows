# Contributing

Thanks for helping improve Codex Usage Meter for Windows.

## Before opening an issue

- Search existing issues first.
- Include your Windows version and Codex client when reporting a bug.
- Describe what the tray menu shows, but do not attach raw Codex session files because they may contain private conversation data.

## Development workflow

1. Fork the repository and create a focused branch.
2. Keep parsing and data-source logic outside the WPF application project.
3. Add or update a check in `tests/CodexUsageMeter.Tests` for parser changes.
4. Run the Release build and checks:

   ```powershell
   dotnet build .\CodexUsageMeter.sln --configuration Release
   dotnet run --project .\tests\CodexUsageMeter.Tests\CodexUsageMeter.Tests.csproj --configuration Release
   ```

5. Open a pull request explaining the user-visible result and any compatibility implications.

Never commit real session files, credentials, access tokens, or personal usage data. Use minimal synthetic JSON fixtures.
