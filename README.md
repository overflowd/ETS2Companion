<p align="center">
  <img src="Assets/logo.png" width="112" height="112" alt="ETS 2 Companion Launcher Logo" />
</p>

<h1 align="center">ETS 2 Companion Launcher</h1>

<p align="center">
  Lightweight Windows system tray utility that automatically launches your chosen companion apps whenever <strong>Euro Truck Simulator 2</strong> (<code>eurotrucks2.exe</code>) or <strong>American Truck Simulator</strong> (<code>amtrucks.exe</code>) starts.
</p>

<p align="center">
  <a href="https://github.com/overflowd/ETS2Companion/releases/latest">
    <img src="https://img.shields.io/github/v/release/overflowd/ETS2Companion?style=for-the-badge&color=22C55E&labelColor=181818" alt="Latest Release" />
  </a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-262626?style=for-the-badge&labelColor=181818" alt="Platform Windows 10/11" />
  <img src="https://img.shields.io/badge/.NET-9.0-262626?style=for-the-badge&labelColor=181818" alt=".NET 9" />
</p>

---

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
- **Pure Dark Mode & System Tray:** Neutral dark UI with native Windows dark title bar and dark context menus. Minimizes quietly to the system tray with a live status-colored icon and supports starting minimized with Windows.

## Requirements

- Windows 10 / 11 (64-bit)
- **No prerequisites** for the `standalone` release build *(recommended)*
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) only if using the smaller `net9` build

## Quick Start

1. Download the latest build from the **[Releases](https://github.com/overflowd/ETS2Companion/releases/latest)** page (`standalone.zip` recommended), extract it, and run `ETS2CompanionLauncher.exe`.
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
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-standalone
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full release history.
