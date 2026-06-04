# WiFi Motion CLI

A Python CLI tool that detects motion by analyzing Wi-Fi signal variations. On Windows 11, it reads RSSI (dBm) values via `netsh wlan show interfaces`.

## How It Works

Wi-Fi signals are absorbed and reflected by physical objects in the environment (human body, furniture). When someone moves, signal strength changes. This tool detects motion using three metrics derived from RSSI readings:

- **VAR (Variance)** – Signal spread within the sampling window. Captures slow or sustained position changes.
- **DEL (Delta)** – Instant difference between consecutive readings. Captures fast motion (e.g. hand waving).
- **PTP (Peak-to-Peak)** – Range between the lowest and highest values in the window. Measures overall activity level.

### Motion Scale

| Scale | VAR Value | Example |
|-------|-----------|---------|
| Very Small | < 3 | Finger, keyboard key |
| Small | 3–10 | Hand, arm |
| Medium | 10–30 | Walking |
| Large | 30–100 | Moving around the room |
| Very Large | 100+ | Door, furniture |

## Requirements

- Windows 11 (24H2+)
- Python 3.13+
- Wi-Fi adapter connected to a network
- **Administrator privileges** (required for `netsh`)
- **Location services enabled** (Settings > Privacy > Location)

If you run into location permission issues, run `enable_location.bat` as administrator.

## Installation

If Python 3.13+ is not installed, download it from [python.org](https://python.org).

```
git clone https://github.com/Ozgurooozer/CSentor.git
cd CSentor
python wifi_motion.py
```

Optional dependencies (for the MCP server):

```
pip install -r requirements.txt
```

## Usage

### Keys

| Key | Action |
|-----|--------|
| S | Start (calibration + detection) |
| T | Stop |
| C | Calibration only |
| + / = | Increase sensitivity (0.1–15.0) |
| - / _ | Decrease sensitivity (0.1–15.0) |
| V/D/P | Select VAR/DEL/PTP threshold |
| Up/Down | Adjust selected threshold by ±1.0 |
| Left/Right | Adjust selected threshold by ±0.1 |
| 1–6 | Toggle AP slot on/off |
| Z | Toggle audio alarm |
| I | Info screen (thresholds, multiplier) |
| H | Help |
| Q | Quit |

### Step by Step

1. Launch with `python wifi_motion.py`.
2. Press **S** to start calibration.
3. Stay still during calibration (30 seconds).
4. Detection starts automatically when calibration completes.
5. Motion events are displayed on screen in real time.
6. Adjust sensitivity with **+/-**, then press **C** to recalibrate if needed.

### Sensitivity Levels

Each level changes the threshold multiplier and minimum threshold values:

| Level | Multiplier | minVAR | minDEL | minPTP | Description |
|-------|------------|--------|--------|--------|-------------|
| 1 | 5.65 | 25.0 | 8.0 | 12.0 | Low sensitivity |
| 5 | 4.60 | 18.6 | 6.2 | 9.2 | Medium |
| 10 | 2.85 | 10.6 | 3.95 | 5.7 | High |
| 15 | 1.10 | 2.6 | 1.7 | 2.2 | Maximum |

### MCP Server (Claude Code Integration)

```
pip install -r requirements.txt
python wifi_motion_server.py
```

## Limitations

- **Position/direction detection is not possible** – With a single Wi-Fi adapter, direction finding requires power measurements from at least three access points (APs) or CSI (Channel State Information) hardware.
- Motion detection depends on environmental conditions at calibration time. Moving large objects or adding new devices may require recalibration.
- RSSI cannot be read via `netsh` without location permission. Run `enable_location.bat` as administrator.

## Project Structure

```
wifi_motion.py           Main application (CLI)
wifi_motion_server.py    MCP server (Claude Code)
lib/
  __init__.py            Module definition
  config.py              Settings and state classes
  stats.py               Statistics functions
  wlan_api.py            WLAN API wrapper
tests/
  test_stats.py          Unit tests
  test_wlan.py           WLAN API tests
enable_location.bat      Location permission setup (admin)
requirements.txt         Python dependencies
pyproject.toml           Project metadata
LICENSE                  MIT License
.gitignore
README.md                This file
```

