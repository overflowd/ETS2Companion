# ETS 2 Companion Launcher

A lightweight Windows system tray utility that automatically launches your chosen companion apps whenever **Euro Truck Simulator 2** (`eurotrucks2.exe`) or **American Truck Simulator** (`amtrucks.exe`) starts.

Whether you use head-tracking software, upscalers, dashboard overlays, telemetry clients, or recording tools, add any programs you want and let them start automatically every time you hit the road.

## Features

- **ETS 2 & ATS Support:** Quick checkboxes for **ETS 2** (`eurotrucks2`) and **ATS** (`amtrucks`), plus an optional field for any extra game processes.
- **Easy App Selection:** Add any program on your PC via:
  - **Running Apps Picker** — select any app you currently have open
  - **Installed Apps Picker** — searchable list of Start Menu shortcuts, Steam apps, and installed programs
  - **Browse / Custom Entry** — choose any `.exe`, `.lnk`, `.bat`, or `steam://rungameid/...` URL
- **Quick-Add Presets:** Optional one-click presets with automatic path detection for popular trucking tools (Lossless Scaling, Beam Eye Tracker, LookPilot, WestPlay, OpenTrack, TrackIR, SimHub, TruckersMP, TrucksBook, CrewChief, OBS Studio, Discord).
- **No Duplicate Launches:** Checks if a companion app is already running before starting it.
- **Per-App Launch Delay & Auto-Close:** Set an individual delay (in seconds) for each app and optionally close apps automatically when the game exits.
- **System Tray & Windows Startup:** Minimizes quietly to the system tray with a live status-colored icon and supports starting minimized with Windows.

## Requirements

- Windows 10 / 11 (64-bit)
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (if using the framework-dependent build)

## Quick Start

1. Download the latest release (or build from source below) and run `ETS2CompanionLauncher.exe`.
2. Click **+ Add App ▾** to pick any application from your running apps, installed apps, or file browser — or open **Presets ▾** to quick-add popular ETS 2 / ATS tools.
3. Choose which game(s) to monitor (**ETS 2**, **ATS**, or custom processes).
4. Click **Launch Now (Test)** to verify your companion apps open as expected.
5. Check **Start minimized with Windows** so the launcher quietly waits in your system tray every time you boot your PC.
6. Closing the window (`X`) minimizes it to the system tray. To exit completely, right-click the tray icon and select **Exit**.

## Configuration

All settings are saved automatically at:

```text
%APPDATA%\ETS2CompanionLauncher\config.json
```

## Building from Source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0):

```powershell
# Framework-dependent build (small footprint, requires .NET 9 Desktop Runtime)
dotnet publish -c Release -o publish --no-self-contained

# Self-contained single-file build (runs on any Windows PC without installing .NET)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone
```
