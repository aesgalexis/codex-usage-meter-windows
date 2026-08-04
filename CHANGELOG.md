# Changelog

All notable changes to this project will be documented here.

The project follows [Semantic Versioning](https://semver.org/).

## [0.4.3] - 2026-08-04

### Fixed

- Declares per-monitor V2 DPI awareness for the mixed WPF and Windows Forms process.
- Prevents Windows coordinate virtualization from moving the tray menu and hover widget to a different monitor.

## [0.4.2] - 2026-08-04

### Fixed

- Positions the tray-hover widget using the DPI and physical coordinates of the monitor that owns the notification area.
- Prevents vertically stacked mixed-DPI displays from pushing the hover widget onto the laptop screen.

## [0.4.1] - 2026-08-04

### Added

- Usage-bar display selection with Automatic, All displays and one entry for each monitor detected by Windows.
- Persistent monitor preference with automatic fallback when the selected display is temporarily disconnected.
- Configurable usage-bar thickness from one to five pixels.

### Documentation

- Added a screenshot showing the usage bar above the Windows taskbar.

## [0.4.0] - 2026-08-04

### Changed

- The tray-visibility command now opens Windows taskbar settings directly without an intermediate message.
- Reads primary and secondary rate-limit windows, identifies them by duration and uses the most restrictive window for the main indicator.
- Selects snapshots by their event timestamp across recent sessions instead of trusting file modification order.
- Keeps the last valid snapshot during temporary failures and marks its age and stale state in the interface.
- Labels the reported balance as credits instead of assuming that it represents available resets.
- Compact mode no longer converts credits into reset dots; dots stay hidden until Codex exposes a reliable reset count.
- Compact-widget shine reflects active Codex tasks, stays clipped inside the capsule and uses a three-second cadence.
- Monochrome Sol, Luna and Terra model symbols in normal and compact widgets, sourced from the latest session turn context.
- Shine triggering moved to immediate session writes so delayed snapshot refreshes cannot create late passes.
- Removed the developer-oriented session-folder shortcut from the main tray menu and made compact activity animation automatic.
- Percentage-change and 50/75/90% threshold notifications are disabled by default for new installations.

### Added

- English and Spanish application catalogs covering the tray menu, widgets, tooltips and notifications.
- Installer language selection, Windows-language detection, persistent preference and an in-app language menu.
- Optional three-pixel usage bar above the Windows taskbar, independent from the normal and compact widgets.
- Full-width progress that recedes from right to left as availability falls, with a high-contrast activity shine.

### Fixed

- Prevents activity shine from remaining active after Codex finishes when several session files change together.
- Reconciles tracked task state before each shine cycle so missed filesystem events self-correct.

### Tests

- Added coverage for dual windows, secondary-only events, replayed old snapshots, cross-session ordering, incomplete JSONL tail fragments and notification defaults.

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
