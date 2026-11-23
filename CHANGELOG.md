# Changelog

All notable changes to **ETS 2 Companion Launcher** are documented in this file.

## [v1.0.3]

### Fixed
- Removed extra vertical whitespace below the action toolbar (`+ Add App`, `Presets`, `Edit`, `Remove`) and inside the settings panel caused by `FlowLayoutPanel` wrap measurement.
- Tightened single-row panel spacing across the main window, Add/Edit dialog, and App Picker dialog for a cleaner layout.

## [v1.0.2]

### Fixed
- Fixed overlapping labels, checkboxes, and clipped buttons on High-DPI displays (125%, 150%, and higher Windows scaling) in the Settings panel (`Start minimized with Windows`, `Watch games`, `Extra process(es)`, `Apply`).
- Refactored the header, toolbar, settings panel, `AppEditDialog`, and `AppPickerDialog` to auto-sizing `TableLayoutPanel` and `FlowLayoutPanel` layouts so controls scale cleanly at any DPI.

## [v1.0.1]

### Added
- Added a custom multi-resolution (`16px`–`256px`) dark steering wheel icon (`Assets/app.ico`) embedded directly into `ETS2CompanionLauncher.exe` for Windows Explorer, Desktop shortcuts, and Task Manager.
- Updated runtime window and system tray status icons to match the new high-DPI steering wheel design.

## [v1.0.0]

### Added
- Initial public release of **ETS 2 Companion Launcher**.
- Automatic background game watcher with quick toggles for **Euro Truck Simulator 2** (`eurotrucks2.exe`), **American Truck Simulator** (`amtrucks.exe`), and custom comma-separated processes.
- Pure neutral dark mode UI with native Windows dark title bar and dark context menus.
- **+ Add App** picker supporting Running Apps, Installed / Start Menu / Steam Apps (with live search), and custom `.exe` / `.lnk` / `.bat` / `steam://` targets.
- Optional **Presets** menu with automatic installation path detection for popular tools (Lossless Scaling, Beam Eye Tracker, LookPilot, WestPlay, OpenTrack, TrackIR, SimHub, TruckersMP, TrucksBook, CrewChief, OBS Studio, Discord).
- Per-app launch delay, duplicate launch prevention, optional auto-close on game exit, system tray integration, and start minimized with Windows.
- Automated Windows release builds via GitHub Actions (`standalone` single-file and `net9` framework-dependent).
