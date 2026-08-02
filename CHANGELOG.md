# Changelog

All notable changes to this project will be documented here.

The project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Compact usage card that opens when hovering over or left-clicking the tray icon.
- Pinable, always-visible desktop widget with drag-and-drop positioning.
- Persistent widget visibility and screen position across restarts.
- Compact percentage typography and an icon-only pin control with a visible active state.
- Larger borderless pin that rotates to indicate whether the widget is fixed or released.

## [0.2.2] - 2026-08-02

### Fixed

- Registers the notification area icon with a stable native GUID so Windows can preserve its visibility choice across restarts and upgrades.
- Detects stale start-with-Windows entries that still point to an older portable executable.

## [0.2.1] - 2026-08-02

### Added

- Per-user Inno Setup installer with silent install and uninstall support for WinGet.
- Automated installer verification during GitHub releases.

## [0.2.0] - 2026-08-02

### Added

- Immediate usage refresh through recursive Codex session file monitoring with debounce.
- Persistent notification choices for percentage changes, 50/75/90% usage thresholds, and limit resets.
- Tested notification policy that avoids startup and duplicate alerts.

## [0.1.1] - 2026-08-02

### Added

- Tray menu shortcut and guidance for keeping the icon permanently visible through the supported Windows taskbar setting.

## [0.1.0] - 2026-08-02

### Added

- Initial Windows tray application.
- Local Codex session usage provider.
- Available usage, consumed usage, and reset-time display.
- Color-coded tray icon and start-with-Windows option.
- Automated build, checks, and portable release workflow.
