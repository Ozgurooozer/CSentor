# WiFi Motion

## Purpose — why this repository exists

This project was built as a **job application prototype for Tremium Software**.

**Delivered now:** Windows Wi-Fi motion PoC (calibration, detection, operator UI) plus rigorous ML experiment reports in [`model-training/`](model-training/).

**Target system:** A **geometrically placed ESP32 grid** (e.g. across a mall floor) with **mandatory per-site calibration**, fusing RF observations into **(x, y, z) motion tracks** over that volume; **custom-trained sequence models** interpret events and cut false alarms; the stack runs **standalone** or **integrates with** existing SOC/VMS/camera workflows. Through-wall awareness is an RF+AI goal, not video analytics.

Full roadmap and deployment concept: [`PORTFOLIO.md`](PORTFOLIO.md).

---

Motion detection on **Windows 11** by analyzing Wi-Fi signal strength (RSSI). When people or objects move, absorbed and reflected radio energy changes; this project measures that variation and flags activity in real time.

This repository contains **two implementations** of the same core engine:

| Folder | Stack | Interface | Upstream |
|--------|-------|-----------|----------|
| [`python/`](python/) | Python 3.13+ | Terminal CLI (+ optional automation server) | Reference Python implementation |
| [`WifiMotionDotNet/`](WifiMotionDotNet/) | C# / .NET 9 | WinForms desktop app | Desktop port of the Python CLI |

Both use `netsh wlan show interfaces` for RSSI (dBm) and `wlanapi.dll` for multi–access point (AP) data.

For pipeline design, threading, metrics, and Python/.NET mapping, see **[`ARCHITECTURE.md`](ARCHITECTURE.md)**.  
For product vision, ML experiment findings, and the after-hours retail security roadmap, see **[`PORTFOLIO.md`](PORTFOLIO.md)**.  
For model-training reports (ablations, leakage, dual-path), see **[`model-training/`](model-training/)**.

---

## How It Works

Three metrics drive detection:

- **VAR (Variance)** — Spread within the sampling window; slow or sustained movement.
- **DEL (Delta)** — Step-to-step change; fast motion (e.g. hand waving).
- **PTP (Peak-to-Peak)** — Min–max range in the window; overall activity.

The .NET build adds RX rate variance, FFT-based motion class hints, EWMA baseline tracking, multi-AP slots, and coarse directional hints when two or more APs are enabled.

### Motion scale (VAR)

| Scale | VAR | Example |
|-------|-----|---------|
| Very Small | &lt; 3 | Finger, key press |
| Small | 3–10 | Hand, arm |
| Medium | 10–30 | Walking |
| Large | 30–100 | Moving around the room |
| Very Large | 100+ | Door, furniture |

---

## Requirements (both)

- **Windows 11** (24H2+ recommended for reliable RSSI in dBm)
- Wi-Fi adapter connected to a network
- **Run as administrator** (`netsh` and WLAN API)
- **Location services on** (Settings → Privacy & security → Location)

Without location permission, `netsh` cannot return RSSI. On the Python side, run `enable_location.bat` as admin if needed.

---

## Quick start

### Python CLI

```powershell
cd python
python wifi_motion.py
```

Press **S** to calibrate (stay still ~30 s), then detection runs automatically. See [`python/README.md`](python/README.md) for keys, MCP server, and project layout.

Optional MCP integration (Claude Code):

```powershell
pip install -r requirements.txt
python wifi_motion_server.py
```

### C# WinForms (.NET 9)

```powershell
cd WifiMotionDotNet
dotnet build WifiMotion.sln -c Release -p:Platform=x64
```

Launch **as administrator** (UAC). The app manifest requests elevation; `dotnet run` from a normal shell may fail without it.

```powershell
Start-Process "WifiMotionDotNet\WifiMotion\bin\x64\Release\net9.0-windows\WifiMotion.exe" -Verb RunAs
```

Or open `WifiMotion.sln` in Visual Studio 2022 (17.12+) / 2026, select **x64**, and press **F5**.

Details, keyboard shortcuts, tests, and publish options: [`WifiMotionDotNet/README.md`](WifiMotionDotNet/README.md).

---

## Repository layout

```
test_wifi/
├── README.md                 This file
├── PORTFOLIO.md              Vision, research, retail roadmap
├── ARCHITECTURE.md           Technical architecture
├── model-training/           Model training & experiment reports
│   ├── TECHNICAL_OVERVIEW.md
│   └── reports/            General + reports/bcm/ (BCM experiments)
├── python/                   WiFi Motion CLI (Python)
│   ├── wifi_motion.py
│   ├── wifi_motion_server.py
│   └── lib/
└── WifiMotionDotNet/         WiFi Motion — WinForms port (C#)
    ├── WifiMotion.sln
    └── WifiMotion/
        ├── Core/             Detection engine, WLAN API, RSSI
        ├── UI/               Main window, graphs, test menu
        └── Testing/          CSV recording, heatmaps
```

Shared config: `wifi_motion_config.json` uses the same field names in both projects (e.g. `SOUND_ALARM`).

---

## Python vs .NET

| Feature | Python (`python/`) | .NET (`WifiMotionDotNet/`) |
|---------|-------------------|----------------------------|
| UI | Console | WinForms + live RSSI graph |
| MCP server | Yes (`wifi_motion_server.py`) | No |
| Test suite / heatmaps | Via scripts | Built-in UI + `test_output/` CSV & PNG |
| WLAN struct fixes | — | Corrected `WLAN_BSS_ENTRY` layout vs Python |
| Dependencies | `requirements.txt` (MCP optional) | .NET 9 SDK / Desktop Runtime only |

---

## Limitations

- **No precise position or direction** with a single adapter; reliable direction needs multiple AP power readings or CSI hardware. With 2+ APs enabled, only a coarse spatial hint is available.
- Detection quality depends on conditions at **calibration** time; furniture moves or new devices may require recalibration.

---
