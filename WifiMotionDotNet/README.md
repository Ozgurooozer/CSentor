# WiFi Motion — C# / .NET 9 WinForms

Desktop application that detects motion by analyzing Wi-Fi signal variation on Windows 11. It reads **RSSI (dBm)** via `netsh wlan show interfaces` and multi–access point (AP) data via `wlanapi.dll` (WLAN API).

This project is a **C# .NET 9 Visual Studio WinForms** port of the original **Python CLI** tool (`wifi_motion`).

---

## How It Works

Wi-Fi signals are absorbed and reflected by objects in the environment (human body, furniture). When someone moves, signal strength changes. The app detects motion using three metrics derived from RSSI:

- **VAR (Variance)** — Signal spread within the sampling window (linear power / mW domain). Slow or sustained position changes.
- **DEL (Delta)** — Instant difference between consecutive readings. Fast motion (e.g. hand waving).
- **PTP (Peak-to-Peak)** — Range between the lowest and highest values in the window. Overall activity level.

Additionally: **RXv** (receive-rate variance), **FFT** dominant frequency (breath / walk classification), **EWMA** baseline tracking, **multi-AP** slots, and a coarse **spatial hint** when 2+ APs are enabled.

### Motion Scale (VAR)

| Scale | VAR | Example |
|-------|-----|---------|
| Very Small | < 3 | Finger, keyboard key |
| Small | 3–10 | Hand, arm |
| Medium | 10–30 | Walking |
| Large | 30–100 | Moving around the room |
| Very Large | 100+ | Door, furniture |

---

## Requirements

- **Windows 11** (24H2+ recommended for reliable RSSI in dBm)
- **.NET 9 SDK** or **Visual Studio 2022 (17.12+)** / **Visual Studio 2026** — for building
- To run: **.NET 9 Desktop Runtime** (or a self-contained publish — see below)
- Wi-Fi adapter connected to a network
- **Administrator privileges** (`netsh` + `wlanapi`) — the app manifest requests elevation automatically
- **Location services enabled** (Settings → Privacy & security → Location) — required for `netsh` RSSI reads

---

## Build and Run

### Visual Studio (recommended)

1. Open `WifiMotion.sln` in Visual Studio 2022 (17.12+) or 2026.
2. Select configuration **Debug | x64** (or Release | x64).
3. Press **F5** to build and run. The app will prompt for administrator approval (UAC).

### Command line (.NET CLI)

```powershell
dotnet build WifiMotion.sln -c Release
dotnet run --project WifiMotion\WifiMotion.csproj -c Release
```

> Note: `dotnet run` may trigger UAC because of the administrator manifest. Full functionality (RSSI reads) requires running as administrator.

### Portable (self-contained) publish — no runtime install on target machine

If .NET 9 is not installed on the target PC, you can publish a single-file build:

```powershell
dotnet publish WifiMotion\WifiMotion.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `WifiMotion\bin\Release\net9.0-windows\win-x64\publish\WifiMotion.exe`

---

## Usage

1. Launch the application as administrator.
2. **Start (S)** — runs ~30 s calibration first (stay still), then starts detection.
3. When motion is detected, **Motion: YES!** turns red and the audio alarm plays (if enabled).
4. Adjust **Sensitivity** with the slider or **+/-**; press **Calibration (C)** to recalibrate anytime.

### Keyboard Shortcuts (same as Python CLI)

| Key | Action | Key | Action |
|-----|--------|-----|--------|
| **S** | Start | **Z** | Toggle audio alarm |
| **T** | Stop | **I** | Info window |
| **C** | Calibration | **H** | Help |
| **V/D/P** | Select VAR/DEL/PTP threshold | **↑/↓** | Selected threshold ±1.0 |
| **1–6** | Toggle AP slot | **←/→** | Selected threshold ±0.1 |
| **+/-** | Sensitivity (0.1–15.0) | **Space** | Add note during test |
| **E** | Hand-wave test | **Y** | Direction test |
| **K** | Custom test (30 s) | **M** | All tests menu |

> You can also change thresholds via NumericUpDown controls and enable APs via checkboxes in the list. Keyboard shortcuts work when no input control has focus.

### Tests

While detection is running, you can start **Hand Wave / Direction / Custom / All Tests**. Each test:

- Shows step-by-step instructions and fills a progress bar.
- On completion, writes **CSV** under `test_output/` (all samples + notes + AP history).
- Generates and opens a **heatmap PNG** (4 panels: RSSI, metrics, AP heat map, RX/TX).

**All Tests (M)** runs 12 expert tests: FFT Profile, Noise, Through-Wall Detection, Masking, Speed Scale, Repeatability, Room Occupancy, Breathing Pattern, Fall Simulation, Gesture Library, Hand Wave, Direction Test.

---

## Project Structure

```
WifiMotion.sln
WifiMotion/
  WifiMotion.csproj          net9.0-windows, x64, WinForms, requireAdministrator
  app.manifest               Administrator + Win10/11 compatibility
  Program.cs                 Entry point (single-instance guard)
  wifi_motion_config.json    Settings (same JSON format as Python)
  Core/
    Stats.cs                 Statistics / signal processing  (lib/stats.py port)
    Settings.cs              Settings + JSON load/save (lib/config.py)
    DetectionState.cs        Runtime state                  (lib/config.py)
    ApSlot.cs                AP slot / BSS models
    RssiReader.cs            netsh parser                   (lib/rssi_reader.py)
    WlanApi.cs               wlanapi.dll P/Invoke           (lib/wlan_api.py)
    MotionEngine.cs          Detection pipeline             (wifi_motion.py logic)
  Testing/
    TestModels.cs            TestPhase/Definition/ALL_TESTS/Recording (test_runner.py)
    TestRecorder.cs          Test recorder
    HeatmapGenerator.cs      4-panel chart via GDI+ (replaces matplotlib)
  UI/
    MainForm.cs / .Designer.cs   Main window + logic
    RssiGraphControl.cs          Live RSSI graph (custom control)
    TestMenuForm.cs              Test selection dialog
```

---

## Differences from Python / Improvements

- **WLAN API fix:** The Python `WLAN_BSS_ENTRY` layout was wrong (missing `dot11BssPhyType`, wrong `uRateSetLength` type, kHz/MHz confusion). The C# port uses correct native offsets per the Windows SDK; RSSI, link quality, and channel read correctly.
- **Multithreaded:** Detection runs on a background thread so the GUI does not freeze; `netsh` calls do not block the UI.
- **Heatmap:** Drawn with built-in **GDI+ (System.Drawing)** instead of matplotlib — no extra packages.
- **JSON compatibility:** Reads/writes `wifi_motion_config.json` with the same field names (`SOUND_ALARM` included); existing Python configs work as-is.
- **MCP server** (`wifi_motion_server.py`) is not included in this desktop build (stdio-based, does not fit the GUI model). It can be added as a separate console project if needed.

Numeric output (sensitivity table, variance, FFT, signal quality, labels) was validated against Python behavior with **33 reference tests**.

---

## Limitations

- **No coordinate or direction fix.** With a single adapter, direction needs power from at least three APs or CSI hardware. With 2+ APs enabled, only a coarse spatial hint is produced.
- Detection depends on environmental conditions at calibration time. Moving large furniture or adding devices may require recalibration.
- Without location permission, `netsh` cannot read RSSI.

## License

MIT (same as the original project).
