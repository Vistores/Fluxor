# Fluxor 0.1.0 Smoke Checklist

Use this checklist before building the final `0.1.0` installer and publishing release notes.

## Startup and recovery

- App starts with the default `minimal-suite` profile.
- App starts correctly when the previously selected profile is a built-in profile.
- App starts correctly when the previously selected imported plugin profile is valid.
- App falls back to the safe profile when the selected imported profile DLL is missing.
- App falls back to the safe profile when the selected imported profile has broken runtime metadata.
- Startup recovery banner appears with a clear explanation.
- `Keep Safe Profile` dismisses the banner.
- `Open Plugins Folder` opens the plugin workspace from the recovery banner.

## Main UI

- Main window opens without clipped text at the default window size.
- Current profile card shows name, description, runtime kind, and status correctly.
- `More Actions` menu opens with correct theme styling.
- Built-in profiles and imported profiles remain separated and scroll correctly.
- Parameter editor still renders compact two-column layout where expected.

## Profile workflow

- `Save as Profile` creates a new separate profile.
- `Duplicate Profile` creates a copy with a unique name.
- `Rename Profile` updates the profile name without changing identity.
- `Edit Description` updates the saved description.
- Built-in profiles cannot be renamed directly.
- Built-in profiles can still be duplicated.
- Exported profile archive uses `.fluxorprofile`.
- Imported `.fluxorprofile` restores a profile correctly.
- Legacy `.fluxor-profile.zip` still imports correctly.

## Plugin workflow

- `Import Plugin` works for a new DLL plugin.
- `Import Plugin` can replace an existing imported plugin.
- Replaced plugin keeps compatible parameter values where keys still match.
- `Import Archive` opens the archive preview window.
- Archive preview shows name, id, runtime type, parameter count, icon presence, and DLL presence.
- Archive import conflict mode correctly offers `Replace Existing` and `Import Copy`.

## Diagnostics

- Diagnostics panel shows loaded state for a healthy imported plugin.
- Diagnostics panel shows broken state when the plugin DLL is missing.
- Diagnostics panel shows runtime error state after a plugin failure.
- `Reload` refreshes the runtime.
- `Reveal Files` opens the plugin file location.
- `Use Safe Profile` switches back to a built-in profile.
- `Details` opens the diagnostics details window.
- Diagnostics details window shows summary and technical details text.
- `Next steps` text changes appropriately for broken/error states.

## Localization

- English UI strings look correct in the main window.
- Ukrainian UI strings look correct in the main window.
- Russian UI strings look correct in the main window.
- Settings window localizes correctly.
- Import Plugin window localizes correctly.
- Archive preview window localizes correctly.
- Diagnostics panel and diagnostics details window localize correctly.
- Startup recovery banner localizes correctly.
- Tray menu and tray balloons localize correctly.

## Runtime behavior

- Overlay starts correctly after app launch.
- Cursor effects render after switching between multiple built-in profiles.
- Safe profile switch works after a broken imported plugin.
- App can still minimize to tray and restore correctly.
- App can close to tray when background mode is enabled.
- App exits completely when background mode is disabled.

## Packaging

- `dotnet build .\CursorFX.App\CursorFX.App.csproj -m:1 -v minimal` succeeds.
- `powershell -ExecutionPolicy Bypass -File .\build-installer.ps1` succeeds.
- Installer output file is versioned as `Fluxor-Setup-v0.1.0.exe`.
- Installer still includes plugin workspace samples and plugin SDK.

