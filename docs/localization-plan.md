# English and Spanish localization plan

> Implemented for version 0.4.0. This document also records the intended precedence and verification contract for future languages.

## Goal

Offer English and Spanish throughout the application and let the user choose the initial language in the installer. Upgrades must preserve the existing choice, while silent WinGet installations should select a sensible language automatically.

## Language resolution

Use this precedence at application startup:

1. Language already saved in the application settings.
2. Initial language selected by the installer.
3. Windows display language for first-run portable builds.
4. English as the fallback for unsupported languages.

The stored values will be stable culture codes: `en-US` and `es-ES`.

## Implementation phases

### 1. Extract application text

- Move every visible string from the tray menu, widget, notifications and dialogs into matching application catalogs.
- Keep English and Spanish catalogs with identical stable keys.
- Introduce a small text-catalog service so programmatic WPF and WinForms UI use the same source.
- Localize formatted values through the active culture, including percentages, dates, numbers and singular or plural reset countdowns.

### 2. Persist and change the language

- Add `Language` to `AppSettings`.
- Add an `Idioma / Language` submenu with `English` and `Español`.
- Rebuild the tray menu and refresh the widget immediately when the language changes.
- Persist the selection without requiring reinstallation.

### 3. Connect Inno Setup

- Enable the official English and Spanish Inno Setup language files.
- Let interactive installations select a language on the normal installer language page.
- Pass the chosen culture to the app on first launch or store it in a dedicated per-user bootstrap registry value.
- Never overwrite an existing app-language preference during upgrades.
- For silent installations, map the installer language when supplied and otherwise let the app fall back to the Windows display language.

### 4. Verification

- Test that every resource key exists in both languages.
- Test language precedence for clean installs, upgrades, portable runs and silent installs.
- Run parser and notification tests under both cultures.
- Verify the tray menu, widget dimensions, tooltips, notifications and installer pages visually in English and Spanish.
- Add installer CI checks for `/LANG=english` and `/LANG=spanish` before publishing a release.

## Release approach

Implement localization after the current widget layout is approved. Ship it as a separate minor version so the widget changes and installer-language behavior can be validated independently.
