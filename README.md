# ETS 2 Companion Launcher

A lightweight Windows system tray utility that automatically launches your companion apps (**Lossless Scaling**, **LookPilot**, **WestPlay**, or any custom app) whenever **Euro Truck Simulator 2** (`eurotrucks2.exe`) starts.

## Features

- **Automatic Game Detection:** Quietly monitors `eurotrucks2.exe` in the background with minimal CPU and memory usage. Supports watching additional games (e.g., `eurotrucks2, amtrucks` for American Truck Simulator).
- **Smart Auto-Detection:** Automatically locates installed apps across:
  - Steam libraries (`libraryfolders.vdf`, `appmanifest_*.acf`, and `steamapps\common`)
  - Windows Uninstall Registry entries
  - Start Menu and Desktop shortcuts (`.lnk` & `.url`)
  - Standard installation folders (`Program Files`, `LocalAppData`)
  - Currently running processes
- **No Duplicate Launches:** Checks whether a companion app is already running before starting it.
- **Per-App Launch Delay & Auto-Close:** Configure an individual startup delay (in seconds) and optionally close companion apps automatically when ETS 2 exits.
- **System Tray & Windows Startup:** Minimizes cleanly to the system tray with a live status-colored icon and supports launching minimized on Windows startup.
- **Fully Customizable:** Add any additional `.exe`, `.lnk`, `.bat`, or `steam://rungameid/...` target (e.g., SimHub, TruckersMP, CrewChief, OpenTrack) with optional launch arguments and administrator elevation.

## Requirements

- Windows 10 / 11 (64-bit)
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (if running the framework-dependent build)

## Quick Start

1. Download the latest release (or build from source below) and run `ETS2CompanionLauncher.exe`.
2. On first launch, the app automatically scans for **Lossless Scaling**, **LookPilot**, and **WestPlay**.
   - If an app is installed in a custom directory and isn't found automatically, double-click its row and click **Browse...** to select its `.exe` file — or open that app and click **Auto-Detect** to capture its exact path from the running process.
3. Click **Launch Now (Test)** to verify your companion apps start properly.
4. Check **Start minimized with Windows** so the launcher quietly waits in the system tray whenever your PC boots.
5. Closing the window (`X`) hides it to the system tray. To exit completely, right-click the tray icon and select **Exit**.

## Configuration

All settings are stored automatically at:

```text
%APPDATA%\ETS2CompanionLauncher\config.json
```

## Building from Source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0):

```powershell
# Framework-dependent build (small footprint, requires .NET 9 Desktop Runtime)
dotnet publish -c Release -o publish --no-self-contained

# Self-contained build (works on any Windows PC without installing .NET)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone
```
