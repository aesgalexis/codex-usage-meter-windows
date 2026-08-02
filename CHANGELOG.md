# Changelog

All notable changes to this project will be documented here.

The project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- The tray-visibility command now opens Windows taskbar settings directly without an intermediate message.
- Reads primary and secondary rate-limit windows, identifies them by duration and uses the most restrictive window for the main indicator.
- Selects snapshots by their event timestamp across recent sessions instead of trusting file modification order.
- Keeps the last valid snapshot during temporary failures and marks its age and stale state in the interface.
- Labels the reported balance as credits instead of assuming that it represents available resets.

### Added

- English and Spanish application catalogs covering the tray menu, widgets, tooltips and notifications.
- Installer language selection, Windows-language detection, persistent preference and an in-app language menu.

### Tests

- Added coverage for dual windows, secondary-only events, replayed old snapshots, cross-session ordering and incomplete JSONL tail fragments.

## [0.3.0] - 2026-08-02

### Added

- Compact usage card that opens when hovering over or left-clicking the tray icon.
- Pinable, always-visible desktop widget with drag-and-drop positioning.
- Persistent widget visibility and screen position across restarts.
- Compact percentage typography and an icon-only pin control with a visible active state.
- Larger borderless pin that rotates to indicate whether the widget is fixed or released.
- Reset countdown expressed in remaining days instead of an absolute date in the widget.
- First-time pinning preserves the flyout position beside the notification area.
- Tray icon and widget bar now share colors and rounded percentage thresholds.
- Complete vector pin shown downward when released and rotated 45 degrees when fixed.
- Clear usage-reset countdown label and locally reported credit balance.
- Borderless pin hover state, a more compact widget header, and clearer available-resets wording.
- Persistent compact widget mode with an integrated usage bar, reset dots and pin control.
- Clear Disabled, Normal and Compact widget modes, with widgets disabled by default and the selected design used consistently on hover and when pinned.

### Fixed

- Graceful idle behavior when Codex is not installed or its local sessions are removed, with automatic recovery when they appear again.

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
