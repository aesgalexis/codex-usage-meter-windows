# Release checklist

Use one consolidated release for confirmed fixes. Finish the relevant review and checks before pushing a release tag. Record failures and unavailable checks; passing automation does not replace native Windows checks or actual use.

The commands below use version **0.8.0** and release date **2026-11-05** as an example. Keep the selected version consistent throughout the release.

## Review and version consistency

- [ ] Review the complete diff and confirm that each behavior change addresses a reproducible problem or an explicitly requested improvement.
- [ ] Confirm that the app project `<Version>`, Git tag `v0.8.0`, installer `MyAppVersion`, changelog entry, and WinGet package version all agree.
- [ ] Ensure that the changelog describes the final changes and their limits. Keep unresolved ideas in the existing roadmap rather than presenting them as implemented behavior.
- [ ] Confirm that screenshots use synthetic data and contain no session contents, credentials, tokens, or personal account information.

## Build and automated checks

Use the .NET 10 SDK on Windows:

```powershell
dotnet build .\CodexUsageMeter.sln --configuration Release
dotnet run --project .\tests\CodexUsageMeter.Tests\CodexUsageMeter.Tests.csproj `
  --configuration Release --no-build
dotnet publish .\src\CodexUsageMeter.App\CodexUsageMeter.App.csproj `
  -p:PublishProfile=win-x64 --output .\artifacts\win-x64
```

- [ ] Build and automated checks pass for the exact commit being released.
- [ ] Regression checks cover absent weekly data, long inactive session tails, and explicit/environment/default Codex home resolution using synthetic fixtures.
- [ ] Confirm that the published executable and version metadata match the candidate version. Published builds are self-contained; test the resulting executable, not only `dotnet run`.
- [ ] Build the installer from that published output with the same version. The release workflow passes the project version explicitly to Inno Setup and rejects a mismatched tag.

## Native checks and actual use

On an interactive Windows desktop:

```powershell
dotnet run --project .\tests\CodexUsageMeter.Tests\CodexUsageMeter.Tests.csproj `
  --configuration Release --no-build -- --window-checks
```

- [ ] Native window checks pass, including focus preservation, input pass-through, and non-activation of the taskbar line.
- [ ] Check English and Spanish menus, tooltips, widgets, About, and installer language behavior. Verify immediate language switching and preservation across upgrades.
- [ ] Check mouse and keyboard tray access: Windows+B, arrow keys, Enter/Space, and Menu or Shift+F10. Check primary-action behavior and menu placement.
- [ ] Check normal and compact widgets, pin/unpin, outside-click/Escape dismissal, restored position, and missing weekly data shown as unavailable.
- [ ] Check the usage bar with ordinary, maximized, and full-screen windows, taskbar auto-hide, and more than one display where available.
- [ ] Check relevant DPI scales, including mixed-scale displays and moving the widget between displays. Disconnect or rearrange a display and confirm that the pinned widget remains reachable.
- [ ] Exercise a recent Codex task with the meter running, without committing or publishing the user's session data. Confirm that refresh and activity behavior agree with the locally reported state.
- [ ] Record the Windows builds, Codex clients, languages, and display configurations actually tested. The minimum supported OS does not imply a physical test on every supported Windows version.

## Installer and update checks

Run integration checks only in CI or a **clean, disposable Windows profile**. The profile must have no existing app installation, app settings, startup registration, or running copy. These checks install, run, update, and uninstall the app and change HKCU startup state.

In that disposable profile:

```powershell
.\tools\Test-Installer.ps1 `
  -InstallerPath .\artifacts\CodexUsageMeter-Setup-win-x64.exe `
  -PortableExecutablePath .\artifacts\win-x64\CodexUsageMeter.exe `
  -AllowCurrentUserRegistryChanges
```

CI does not require the opt-in switch. Do not use the switch to bypass the clean-profile requirement.

- [ ] Check silent installation, installed update, close/relaunch, and uninstall.
- [ ] Check that disabled startup remains disabled, enabled startup survives updates, and an enabled portable registration moves to the installed copy.
- [ ] Confirm that update and uninstall preserve another copy's startup registration.
- [ ] Check download verification/failure handling and cleanup of app-owned update directories older than 24 hours. Recent or unrelated temporary directories must remain intact.

## Release and WinGet

- [ ] Retain the tested installer and portable ZIP, and verify their hashes. Do not rebuild or replace the installer after generating its manifest checksum without repeating the affected checks.
- [ ] Confirm the candidate diff and commit before tagging. Push the matching version tag only after the preceding checks pass.
- [ ] Confirm that the release workflow succeeds and that both GitHub release assets are present with the expected names and version.

Generate the WinGet manifests from the exact released installer:

```powershell
.\tools\New-WinGetManifest.ps1 -Version 0.8.0 `
  -InstallerPath .\artifacts\CodexUsageMeter-Setup-win-x64.exe `
  -ReleaseDate '2026-11-05'
winget validate --manifest .\packaging\winget\0.8.0
```

The generator writes a version subdirectory under `packaging/winget` by default; `-OutputDirectory` selects a different parent directory. The release workflow also prepares manifests as build artifacts.

- [ ] Review the generated installer URL, SHA-256 hash, architecture, version, release date, and supported OS against the released asset.
- [ ] Run WinGet validation and address any findings before submission.
- [ ] Submit the version manifests to the Windows Package Manager community repository and follow its validation checks.
- [ ] Confirm that the catalog pull request is merged before stating that the new version is available through WinGet. GitHub release availability and WinGet catalog acceptance are separate milestones.
- [ ] Record the final commit, tag, release links, catalog pull request, and validation results together. Publish another version only for a new confirmed issue or requested change.
