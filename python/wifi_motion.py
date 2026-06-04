from __future__ import annotations

import atexit
from collections import deque
import ctypes
from dataclasses import fields
import math
import msvcrt
import os
import subprocess
import sys
import time
import winreg
import winsound
from typing import List, Optional

from lib.config import Settings, DetectionState
from lib.rssi_reader import get_rssi, can_read_rssi
from lib.stats import (
    _DEFAULT_SAMPLE_HZ,
    dbm_to_mw,
    dominant_freq,
    estimate_sampling_rate,
    freq_label,
    kurtosis,
    motion_magnitude,
    mw_to_dbm,
    peak_to_peak,
    sensitivity_params,
    signal_quality,
    skewness,
    variance,
    variance_mw,
    zero_crossing_rate,
)
from lib.wlan_api import WlanApi
from test_runner import TestRecorder, generate_heatmap, _TEST_DIR, ALL_TESTS, TestPhase

kernel32 = ctypes.windll.kernel32
kernel32.SetConsoleMode(kernel32.GetStdHandle(-11), 7)

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RED = "\033[91m"
GREEN = "\033[92m"
YELLOW = "\033[93m"
CYAN = "\033[96m"
RESET = "\033[0m"
BOLD = "\033[1m"

_DIR = os.path.dirname(os.path.abspath(__file__))
SETUP_FLAG = os.path.join(_DIR, ".setup_done")
PID_FILE = os.path.join(_DIR, ".wfm_pid")
MOTION_LOG = os.path.join(_DIR, "motion_log.txt")
RSSI_LOG = os.path.join(_DIR, "rssi_log.txt")
LOG_MAX_LINES = 5000

MB_YESNO = 4
MB_ICONQUESTION = 32
IDYES = 6

SETTINGS = Settings()
STATE = DetectionState()
WLAN_API = WlanApi()
TEST_RECORDER = TestRecorder()


def _append_motion_log(line: str) -> None:
    """Append a line to the motion log file. Silently ignores write errors."""
    try:
        with open(MOTION_LOG, "a", encoding="utf-8") as f:
            f.write(line)
    except OSError:
        pass


def _trim_log(path: str, max_lines: int = LOG_MAX_LINES) -> None:
    """Trim a log file to at most ``max_lines`` lines. Silently ignores errors."""
    try:
        with open(path, "r", encoding="utf-8") as f:
            lines = f.readlines()
        if len(lines) > max_lines:
            with open(path, "w", encoding="utf-8") as f:
                f.writelines(lines[-max_lines:])
    except OSError:
        pass


def _rssi_graph() -> List[str]:
    """Build a terminal-friendly sparkline graph of recent RSSI values.

    Returns a list of strings, one per row, that when printed form a
    vertical bar chart of the signal strength history in ``STATE.graph_hist``.
    """
    data = list(STATE.graph_hist)
    while len(data) < SETTINGS.graph_w:
        data.insert(0, None)
    data = data[-SETTINGS.graph_w:]
    vals = [v for v in data if v is not None]
    if vals:
        lo = min(vals) - 2
        hi = max(vals) + 2
    else:
        lo, hi = -80, -40
    span = hi - lo
    if span < 8:
        mid = (hi + lo) / 2
        lo, hi = mid - 4, mid + 4
        span = hi - lo

    def to_px(dbm: int | None) -> int | None:
        if dbm is None:
            return None
        p = (dbm - lo) / span * (SETTINGS.graph_h - 1)
        return int(max(0, min(SETTINGS.graph_h - 1, p)))

    pixels = [to_px(d) for d in data]
    lines: List[str] = []
    for r in range(SETTINGS.graph_h - 1, -1, -1):
        dbm_at = lo + span * r / (SETTINGS.graph_h - 1)
        if r in (0, SETTINGS.graph_h - 1, SETTINGS.graph_h // 2):
            lbl = f"{int(round(dbm_at)):4d}\u2524"
        else:
            lbl = "    \u2502"
        chars: List[str] = []
        for p in pixels:
            if p is None:
                chars.append(" ")
            elif p == r:
                chars.append("\u2588")
            elif p > r:
                chars.append("\u2591")
            else:
                chars.append(" ")
        lines.append(lbl + "".join(chars))
    lines.append("    \u2514" + "\u2500" * SETTINGS.graph_w + "\u2192")
    return lines


def _save_snapshot() -> None:
    """Write a snapshot of current detection state to the RSSI log file."""
    ts = time.strftime("%Y-%m-%d %H:%M:%S")
    durum = "HAREKET" if STATE.motion_now else "sakin"
    hdr = (
        f"=== {ts}  RSSI:{STATE.last_dbm}dBm(taban:{STATE.baseline_dbm})  "
        f"RX:{STATE.last_rx_rate:.0f}Mbps  "
        f"VAR:{STATE.last_var:.2f}/{SETTINGS.threshold_var:.1f}  "
        f"DEL:{STATE.last_delta:.2f}/{SETTINGS.threshold_delta:.1f}  "
        f"PTP:{STATE.last_ptp:.2f}/{SETTINGS.threshold_ptp:.1f}  "
        f"freq:{STATE.fft_freq:.2f}Hz {STATE.fft_label}  "
        f"[{durum}] ==="
    )
    try:
        with open(RSSI_LOG, "a", encoding="utf-8") as f:
            f.write(hdr + "\n")
        _trim_log(RSSI_LOG)
    except OSError:
        pass


def is_admin() -> bool:
    """Return True if the current process has administrator privileges."""
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except OSError:
        return False


def elevate() -> None:
    """Re-launch the script with administrator privileges and exit."""
    script = os.path.abspath(__file__)
    ctypes.windll.shell32.ShellExecuteW(
        None, "runas", sys.executable, f'"{script}"', None, 1
    )
    sys.exit()


def _cleanup_pid() -> None:
    """Remove the PID file. Silently ignored if already absent."""
    try:
        os.remove(PID_FILE)
    except OSError:
        pass  # expected on first run


def _write_pid() -> None:
    """Write the current PID to the PID file and register cleanup on exit."""
    with open(PID_FILE, "w") as f:
        f.write(str(os.getpid()))
    atexit.register(_cleanup_pid)


def check_existing_instance() -> None:
    """Check for another running instance via the PID file.

    If another instance is found the user is prompted to kill it and restart.
    """
    if not os.path.exists(PID_FILE):
        _write_pid()
        return
    try:
        with open(PID_FILE) as f:
            old_pid = int(f.read().strip())
    except (OSError, ValueError):
        _write_pid()
        return
    r = subprocess.run(
        ["tasklist", "/FI", f"PID eq {old_pid}", "/NH"],
        capture_output=True, text=True, encoding="utf-8", errors="ignore",
    )
    if str(old_pid) not in r.stdout:
        _write_pid()
        return
    ret = ctypes.windll.user32.MessageBoxW(
        0,
        "WiFi Motion CLI is already running.\nKill the existing instance and restart?",
        "WiFi Motion CLI",
        MB_YESNO | MB_ICONQUESTION,
    )
    if ret == IDYES:
        subprocess.run(["taskkill", "/F", "/PID", str(old_pid)], capture_output=True)
        time.sleep(1)
        _write_pid()
    else:
        sys.exit(0)


def _enable_location_auto(warning: bool = True) -> bool:
    """Enable the Windows location service automatically via registry changes.

    NOTE: Registry changes are NOT rolled back on failure.
    In a production system, save original values before writing
    and restore on failure.

    Modifies Windows Registry keys and starts/stops services so that
    ``netsh wlan show interfaces`` returns RSSI data.

    Security note: this function changes HKCU and HKLM registry keys to
    grant location access.  It should only be called with explicit user
    consent.

    Args:
        warning: If True, display a confirmation dialog before making changes.

    Returns:
        True if location services are now enabled and RSSI is readable.
    """
    if warning:
        ret = ctypes.windll.user32.MessageBoxW(
            0,
            "WiFi Motion CLI will modify the Windows Registry to enable the location service automatically. Do you want to continue?",
            "WiFi Motion CLI",
            MB_YESNO | MB_ICONQUESTION,
        )
        if ret != IDYES:
            return False
    try:
        subprocess.run(["sc", "config", "lfsvc", "start=auto"], capture_output=True)
        subprocess.run(["sc", "start", "lfsvc"], capture_output=True)
        with winreg.OpenKey(
            winreg.HKEY_LOCAL_MACHINE,
            r"SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration",
            0,
            winreg.KEY_SET_VALUE,
        ) as k:
            winreg.SetValueEx(k, "Status", 0, winreg.REG_DWORD, 1)
        try:
            h = winreg.CreateKey(
                winreg.HKEY_CURRENT_USER,
                r"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
            )
            winreg.CloseKey(h)
        except OSError:
            pass
        with winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
            0,
            winreg.KEY_SET_VALUE,
        ) as k:
            winreg.SetValueEx(k, "Value", 0, winreg.REG_SZ, "Allow")
        try:
            h = winreg.CreateKey(
                winreg.HKEY_LOCAL_MACHINE,
                r"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
            )
            winreg.CloseKey(h)
        except OSError:
            pass
        with winreg.OpenKey(
            winreg.HKEY_LOCAL_MACHINE,
            r"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
            0,
            winreg.KEY_SET_VALUE,
        ) as k:
            winreg.SetValueEx(k, "LetAppsAccessLocation", 0, winreg.REG_DWORD, 1)
        subprocess.run(["net", "stop", "WlanSvc"], capture_output=True)
        time.sleep(1)
        subprocess.run(["net", "start", "WlanSvc"], capture_output=True)
        time.sleep(2)
        return can_read_rssi()
    except OSError:
        return False


def setup_wizard() -> None:
    """Run the first-time setup wizard: admin check, WiFi adapter, location."""
    sys.stdout.write("\033[2J\033[H")
    sys.stdout.flush()
    print("\u250c\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2510")
    print("\u2502       WiFi Motion CLI  \u2014  First-Time Setup       \u2502")
    print("\u2514\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2518")
    print()
    print("Step 1/3  Administrator privileges")
    if not is_admin():
        print("  [!] Administrator privileges required.")
        print("  [Enter] Restart as Administrator   [Q] Quit")
        if msvcrt.getwch().lower() == "q":
            sys.exit(0)
        elevate()
    print("  [OK] Running as Administrator.")
    print()
    print("Step 2/3  WiFi adapter")
    r = subprocess.run(
        ["netsh", "wlan", "show", "interfaces"],
        capture_output=True, text=True, encoding="utf-8", errors="ignore", timeout=5,
    )
    out = r.stdout + r.stderr
    if "There are 0 interfaces" in out or "There is 0" in out:
        print("  [!] No WiFi adapter found.")
        msvcrt.getwch()
        sys.exit(1)
    print("  [OK] WiFi adapter found.")
    print()
    print("Step 3/3  Location service")
    out_lower = out.lower()
    blocked = "location permission" in out_lower or "access is denied" in out_lower
    if not blocked:
        print("  [OK] Location service is active.")
    else:
        print("  [!] Location service is disabled.")
        print()
        print("  [A] Fix automatically   [M] Fix manually   [Q] Quit")
        while True:
            ch = msvcrt.getwch().lower()
            if ch == "q":
                sys.exit(0)
            elif ch == "a":
                print("  Fixing...")
                if _enable_location_auto():
                    print("  [OK] Location service enabled!")
                    break
                else:
                    print("  [!] Failed. Try [M] for manual setup.")
            elif ch == "m":
                subprocess.run(["start", "ms-settings:privacy-location"], shell=True)
                print("  Apply the settings then press any key...")
                msvcrt.getwch()
                if can_read_rssi():
                    print("  [OK] Active!")
                    break
                else:
                    print("  [!] Still disabled. [T] Retry   [Q] Quit")
                    if msvcrt.getwch().lower() == "q":
                        sys.exit(1)
    print()
    try:
        with open(SETUP_FLAG, "w") as f:
            f.write("ok")
    except OSError:
        pass
    print("  Setup complete!  Press any key...")
    msvcrt.getwch()


def needs_setup() -> bool:
    """Return True if the setup flag is missing or location blocks RSSI reads."""
    return not os.path.exists(SETUP_FLAG) or not can_read_rssi()


def scan_aps(trigger: bool = False) -> None:
    """Scan for nearby access points and update ``STATE.ap_slots``.

    Args:
        trigger: If True, force a fresh scan via the WLAN API.
    """
    fresh = WLAN_API.scan(trigger=trigger)
    if not fresh:
        return
    enabled_map = {s["bssid"]: s["enabled"] for s in STATE.ap_slots}
    history_map = {s["bssid"]: s["history"] for s in STATE.ap_slots}
    new_slots = []
    for ap in fresh[:6]:
        b = ap["bssid"]
        hist = list(history_map.get(b, []))
        new_rssi = ap["rssi"]
        if len(hist) >= 2:
            d = new_rssi - hist[-1]
            trend = "\u25b2" if d > 2 else ("\u25bc" if d < -2 else "\u258c")
        else:
            trend = "\u258c"
        hist.append(new_rssi)
        if len(hist) > SETTINGS.window:
            hist.pop(0)
        new_slots.append({
            "ssid": ap["ssid"],
            "bssid": b,
            "rssi": new_rssi,
            "quality": ap["quality"],
            "enabled": enabled_map.get(b, True),
            "history": hist,
            "trend": trend,
        })
    STATE.ap_slots = new_slots


def calibrate() -> None:
    """Run calibration to determine baseline variance/delta/ptp thresholds.

    Collects samples while stationary, then sets thresholds as multiples
    of the observed noise floor.
    """
    mult, min_v, min_d, min_p = sensitivity_params(SETTINGS.sensitivity)
    show_status(f"Calibrating \u2014 stay still for {SETTINGS.calib_samples} seconds...")
    collected, deltas, ptps, rx_samples = [], [], [], []
    STATE.prev_dbm = None
    for i in range(SETTINGS.calib_samples):
        sig, dbm, rx, tx = get_rssi()
        if dbm is not None:
            if STATE.prev_dbm is not None:
                deltas.append(abs(dbm - STATE.prev_dbm))
            STATE.prev_dbm = dbm
            collected.append(dbm)
            if len(collected) >= SETTINGS.window:
                ptps.append(peak_to_peak(collected[-SETTINGS.window:]))
        if rx is not None:
            rx_samples.append(rx)
            STATE.last_rx_rate = rx
        if tx is not None:
            STATE.last_tx_rate = tx
        show_status(f"Calibration {i+1}/{SETTINGS.calib_samples}  RSSI: {dbm} dBm  RX:{rx} Mbps")
        time.sleep(1)
    var_samples = [
        variance(collected[i:i + SETTINGS.window], linear_power=True)
        for i in range(len(collected) - SETTINGS.window + 1)
    ]
    baseline_var = sum(var_samples) / len(var_samples) if var_samples else 0
    baseline_delta = sum(deltas) / len(deltas) if deltas else 0
    baseline_ptp = sum(ptps) / len(ptps) if ptps else 0
    SETTINGS.threshold_var = max(min_v, baseline_var * mult)
    SETTINGS.threshold_delta = max(min_d, baseline_delta * mult)
    SETTINGS.threshold_ptp = max(min_p, baseline_ptp * mult)
    if collected:
        STATE.ewma_dbm = sum(collected) / len(collected)
        STATE.baseline_dbm = round(STATE.ewma_dbm)
    STATE.rx_hist.clear()
    STATE.rx_hist.extend(rx_samples[-SETTINGS.window:] if len(rx_samples) >= SETTINGS.window else rx_samples)
    STATE.history.clear()
    STATE.history.extend(collected[-SETTINGS.window:] if len(collected) >= SETTINGS.window else collected)
    show_status(
        f"Calibration done!  VAR:{SETTINGS.threshold_var:.1f}  "
        f"DEL:{SETTINGS.threshold_delta:.1f}  "
        f"PTP:{SETTINGS.threshold_ptp:.1f}  "
        f"Base:{STATE.baseline_dbm}dBm"
    )
    time.sleep(2)


def show_status(msg: str = "") -> None:
    """Clear the console and render the main status display.

    Args:
        msg: Optional message to show at the bottom of the screen.
    """
    sys.stdout.write("\033[2J\033[H")
    sys.stdout.flush()
    durum = "RUNNING" if STATE.running else "PAUSED"
    hareket = f"{RED}{BOLD}YES!{RESET}" if STATE.motion_now else f"{GREEN}NO{RESET}"
    mag = motion_magnitude(STATE.last_var) if STATE.motion_now else "-"
    filled = round(SETTINGS.sensitivity)
    sens_bar = "[" + ("|" * filled) + (" " * max(0, 15 - filled)) + "]"
    qlabel, qbar = signal_quality(STATE.last_dbm)

    def _m(tag: str, val: float, thr: float) -> str:
        s = f"{tag}:{val:.1f}/{thr:.1f}"
        if STATE.edit_metric == tag.lower():
            return f"{CYAN}>{s}<{RESET}"
        return f" {s} "

    rx_var_now = variance(STATE.rx_hist, linear_power=True) if len(STATE.rx_hist) >= 3 else 0.0
    rx_str = f"RX:{STATE.last_rx_rate:.0f}" if STATE.last_rx_rate else "RX:---"
    tx_str = f"TX:{STATE.last_tx_rate:.0f}" if STATE.last_tx_rate else "TX:---"
    drift = STATE.last_dbm - STATE.baseline_dbm if STATE.baseline_dbm else 0
    drift_str = f"\u0394{drift:+d}" if STATE.baseline_dbm else "\u0394---"
    freq_str = f"{STATE.fft_freq:.2f}Hz {STATE.fft_label}" if STATE.fft_label else ""
    if TEST_RECORDER.active:
        instr = TEST_RECORDER.current_instruction
        remain = TEST_RECORDER.remaining
        pct = TEST_RECORDER.elapsed / max(0.1, TEST_RECORDER.recording.duration) * 100 if TEST_RECORDER.recording else 0
        bar_w = 40
        filled = int(pct / 100 * bar_w)
        prog = "\u2588" * filled + "\u2591" * (bar_w - filled)
        print(f" {YELLOW}{BOLD}\u250c{' TEST ' + TEST_RECORDER.test_name:^56}\u2510{RESET}")
        print(f" {YELLOW}\u2502{' ' * 56}\u2502{RESET}")
        print(f" {YELLOW}\u2502  {instr:54s}\u2502{RESET}")
        print(f" {YELLOW}\u2502  Remaining: {remain:4.0f}s  [{prog}]  {pct:5.1f}%\u2502{RESET}")
        print(f" {YELLOW}\u2514{'─' * 56}\u2518{RESET}")
        print()
    print("=== WiFi Motion CLI ===")
    print(
        f"Signal: {STATE.last_rssi}%  |  RSSI: {STATE.last_dbm} dBm ({drift_str})  |  "
        f"{rx_str} {tx_str} Mbps  |  {qbar} {qlabel}"
    )
    print(
        _m("VAR", STATE.last_var, SETTINGS.threshold_var)
        + _m("DEL", STATE.last_delta, SETTINGS.threshold_delta)
        + _m("PTP", STATE.last_ptp, SETTINGS.threshold_ptp)
        + f"  RXv:{rx_var_now:.1f}"
    )
    extra = f"  [{freq_str}]" if freq_str else ""
    extra += f"  [{STATE.spatial_hint}]" if STATE.spatial_hint else ""
    print(
        f"Status: {durum} | Motion: {hareket} | Magnitude: {mag} | "
        f"Sens: {SETTINGS.sensitivity:.1f}/15 {sens_bar}{extra}"
    )
    if TEST_RECORDER.active:
        remain = TEST_RECORDER.remaining
        print(f"  {YELLOW}TEST: {TEST_RECORDER.test_name}  |  Remaining: {remain:.0f}s  |  "
              f"Samples: {len(TEST_RECORDER.recording.timestamps) if TEST_RECORDER.recording else 0}{RESET}")
    print()
    hdr_line = f" \u2500\u2500 RSSI Signal  {STATE.last_dbm} dBm  (last {SETTINGS.graph_w} readings) "
    print(hdr_line + "\u2500" * max(0, SETTINGS.graph_w + 6 - len(hdr_line)))
    for row in _rssi_graph():
        print(" " + row)
    print()
    if STATE.ap_slots:
        api_tag = "(WLAN API)" if WLAN_API.is_available() else "(netsh)"
        print(f" \u2500\u2500 Visible APs {api_tag}  [1-6] toggle \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500")
        for i, s in enumerate(STATE.ap_slots[:6], 1):
            st = "\u25cf" if s["enabled"] else "\u25cb"
            v = f"v{variance(s['history'], linear_power=True):.1f}" if len(s["history"]) >= 2 else "v--"
            print(f"  [{i}]{st} {s['ssid'][:22]:22s} {s['rssi']:4d}dBm {s['quality']:3d}% {s.get('trend', '\u258c')} {v}")
    else:
        print("  Waiting for AP scan...")
    print()
    if msg:
        print(f"  >> {msg}")
        print()
    alarm_str = "ALARM:ON" if SETTINGS.SOUND_ALARM else "ALARM:OFF"
    print(" [S] Start  [T] Stop  [C] Calibrate  [1-6] Toggle AP  [Q] Quit")
    if TEST_RECORDER.active:
        print(f" [SPACE] Annotate  |  Remaining: {TEST_RECORDER.remaining:.0f}s")
    else:
        print(f" [E] Hand wave  [Y] Direction test  [K] Custom test  [M] All tests  [R] Calibrate")
    print(f" [V]AR [D]EL [P]TP select  |  [^][v] \u00b11.0   [<][>] \u00b10.1  |  active: {STATE.edit_metric.upper()}")
    print(f" [+]/[-] Sensitivity   [Z] {alarm_str}   [I] Info   [H] Help")


def show_help() -> None:
    """Display the help screen."""
    sys.stdout.write("\033[2J\033[H")
    sys.stdout.flush()
    print("=== WiFi Motion CLI - HELP ===")
    print()
    print("  [S]      Start  [T] Stop  [C] Calibrate")
    print("  [+]/[-]  Sensitivity 0.1\u201315.0, step 0.1")
    print("  [V]      Select VAR threshold (default)")
    print("  [D]      Select DEL threshold")
    print("  [P]      Select PTP threshold")
    print("  [^][v]   Adjust selected threshold \u00b11.0")
    print("  [<][>]   Adjust selected threshold \u00b10.1")
    print("  [1]-[6]  Toggle AP slot on/off")
    print("  [Z]      Toggle audible alarm")
    print("  [E]      Hand wave test (15 s)")
    print("  [Y]      Direction test (18 s \u2014 left/right)")
    print("  [K]      Custom test (30 s)")
    print("  [M]      All tests menu (10 expert tests)")
    print("  [Space]  Annotate during a test")
    print("  [R]      Calibrate (outside of a test)")
    print("  [I] Info   [H] Help   [Q] Quit")
    print()
    print("  TESTS:")
    print("    [1] FFT Profile       [6] Repeatability")
    print("    [2] Noise Floor       [7] Room Occupancy")
    print("    [3] Through-Wall      [8] Breathing Pattern")
    print("    [4] Masking           [9] Fall Simulation")
    print("    [5] Speed Scale       [0] Gesture Library")
    print()
    print("  MULTI-AP:")
    print("    Each enabled slot computes VAR/DEL/PTP over its own history.")
    print("    Any slot exceeding a threshold triggers a motion event.")
    print("    New APs are added automatically; BSSIDs are preserved.")
    print()
    print("  SENSITIVITY:")
    print("    0.1 = very sensitive (noisy)   5.0 = default   15.0 = coarse")
    print()
    print("  Press any key...")
    msvcrt.getch()


def show_info() -> None:
    """Display the information screen with current settings and AP list."""
    sys.stdout.write("\033[2J\033[H")
    sys.stdout.flush()
    mult, min_v, min_d, min_p = sensitivity_params(SETTINGS.sensitivity)
    print("=== WiFi Motion CLI - INFO ===")
    print(f"  Running     : {STATE.running}")
    print(f"  Sensitivity : {SETTINGS.sensitivity:.1f}/15.0  multiplier={mult:.2f}")
    print(f"  Thresholds  : VAR={SETTINGS.threshold_var:.1f}  DEL={SETTINGS.threshold_delta:.1f}  PTP={SETTINGS.threshold_ptp:.1f}")
    print(f"  Last RSSI   : {STATE.last_dbm} dBm   VAR/DEL/PTP: {STATE.last_var:.2f}/{STATE.last_delta:.2f}/{STATE.last_ptp:.2f}")
    print(f"  WLAN API    : {'Active' if WLAN_API.is_available() else 'Disabled'}")
    print(f"  Visible APs : {len(STATE.ap_slots)}")
    for i, s in enumerate(STATE.ap_slots[:6], 1):
        st = "ON    " if s["enabled"] else "OFF   "
        print(f"    [{i}] {s['ssid'][:22]:22s}  {s['rssi']:4d}dBm  {s['quality']:3d}%  {st}  {s['bssid']}")
    print()
    print("  Press any key...")
    msvcrt.getch()


def show_test_menu() -> Optional[TestDefinition]:
    sys.stdout.write("\033[2J\033[H")
    sys.stdout.flush()
    print("=== WiFi Motion CLI - TEST MENU ===")
    print()
    print(f"  {CYAN}Select an expert test:{RESET}")
    print()
    for t in ALL_TESTS:
        key_display = f"[{t.key.upper()}]" if t.key in "ey" else f"[{t.key}]"
        dur = f"{t.duration}s"
        print(f"  {key_display}  {t.name:25s}  {dur:5s}  {t.desc}")
    print()
    print(f"  {YELLOW}[Q] Back to main menu{RESET}")
    print()
    print("  Make your selection...")
    while True:
        ch = msvcrt.getwch().lower()
        if ch == "q":
            return None
        for t in ALL_TESTS:
            if ch == t.key.lower():
                return t


def _calc_spatial_hint() -> str:
    """Determine spatial direction hint based on variance across active APs."""
    active = [
        (s["ssid"], variance(s["history"], linear_power=True))
        for s in STATE.ap_slots
        if s["enabled"] and len(s["history"]) >= 3
    ]
    if len(active) < 2:
        return ""
    active.sort(key=lambda x: x[1], reverse=True)
    top, second = active[0], active[1]
    ratio = top[1] / (second[1] + 0.001)
    if ratio < 1.5:
        return "Center (both APs)"
    return f"Toward {top[0][:12]}"


def _read_signal() -> bool:
    """Read RSSI from the network interface and update detection state.

    Calls ``get_rssi()`` to obtain the current signal, dBm, and data rates.
    Updates ``STATE`` fields including history buffers, variance, and
    peak-to-peak.  Also triggers periodic AP scans.

    Returns:
        True if a valid dBm value was read.  False if the read failed
        (caller should skip the rest of the detection cycle).
    """
    sig, dbm, rx, tx = get_rssi()
    STATE.ap_scan_tick += 1
    if STATE.ap_scan_tick >= SETTINGS.ap_scan_interval:
        scan_aps()
        STATE.ap_scan_tick = 0
    if dbm is None:
        return False
    STATE.last_rssi = sig or 0
    STATE.last_delta = abs(dbm - STATE.prev_dbm) if STATE.prev_dbm is not None else 0.0
    STATE.prev_dbm = dbm
    STATE.last_dbm = dbm
    STATE.timestamps.append(time.time())
    if len(STATE.timestamps) > SETTINGS.window * 2:
        STATE.timestamps.pop(0)
    if rx is not None:
        STATE.last_rx_rate = rx
    if tx is not None:
        STATE.last_tx_rate = tx
    if rx is not None:
        STATE.rx_hist.append(rx)
        if len(STATE.rx_hist) > SETTINGS.window:
            STATE.rx_hist.pop(0)
    STATE.history.append(dbm)
    if len(STATE.history) > SETTINGS.window:
        STATE.history.pop(0)
    STATE.graph_hist.append(dbm)
    if len(STATE.graph_hist) > SETTINGS.graph_w:
        STATE.graph_hist.pop(0)
    STATE.last_var = variance(STATE.history, linear_power=True)
    STATE.last_ptp = peak_to_peak(STATE.history)
    return True


def _check_motion() -> tuple[bool, bool]:
    """Evaluate raw and per-slot (AP) threshold hits.

    Returns:
        A tuple ``(raw_hit, slot_hit)``:

        - ``raw_hit``: True if any primary metric (VAR / DEL / PTP / RXv)
          exceeds its configured threshold.
        - ``slot_hit``: True if any enabled AP slot exceeds thresholds.
    """
    rx_var = variance(STATE.rx_hist, linear_power=True) if len(STATE.rx_hist) >= 3 else 0.0
    rx_hit = rx_var > SETTINGS.rx_var_threshold
    raw_hit = (
        STATE.last_var > SETTINGS.threshold_var
        or STATE.last_delta > SETTINGS.threshold_delta
        or STATE.last_ptp > SETTINGS.threshold_ptp
        or rx_hit
    )
    slot_hit = False
    for s in STATE.ap_slots:
        if not s["enabled"] or len(s["history"]) < 3:
            continue
        h = s["history"]
        sv = variance(h, linear_power=True)
        sd = abs(h[-1] - h[-2])
        sp = peak_to_peak(h)
        if sv > SETTINGS.threshold_var or sd > SETTINGS.threshold_delta or sp > SETTINGS.threshold_ptp:
            slot_hit = True
            break
    return raw_hit, slot_hit


def _handle_motion_event(rising_edge: bool, raw_hit: bool, slot_hit: bool) -> None:
    """Process motion state transitions and update cooldown / status / logging.

    On a rising edge (motion start): plays an alarm if enabled, logs the
    event with causes, and shows a status message.
    On a falling edge (motion end): logs the end of the motion event.
    Maintains ``sustained_motion_frames``, ``peak_var`` and
    ``motion_cooldown``.

    Args:
        rising_edge: True if motion just started in this frame.
        raw_hit: Whether the primary signal exceeded thresholds.
        slot_hit: Whether any AP slot exceeded thresholds.
    """
    if rising_edge:
        if SETTINGS.SOUND_ALARM:
            try:
                winsound.PlaySound("SystemHand", winsound.SND_ALIAS)
            except OSError:
                winsound.Beep(880, 150)
        mag_label = motion_magnitude(STATE.peak_var)
        ts = time.strftime("%Y-%m-%d %H:%M:%S")
        causes = []
        if STATE.last_var > SETTINGS.threshold_var:
            causes.append(f"VAR={STATE.last_var:.1f}")
        if STATE.last_delta > SETTINGS.threshold_delta:
            causes.append(f"DEL={STATE.last_delta:.1f}")
        if STATE.last_ptp > SETTINGS.threshold_ptp:
            causes.append(f"PTP={STATE.last_ptp:.1f}")
        if slot_hit and not raw_hit:
            causes.append("SLOT")
        ap_summary = "  AP:"
        for s in STATE.ap_slots[:6]:
            if len(s["history"]) >= 2:
                v = variance(s["history"], linear_power=True)
                if v > 1.0:
                    ap_summary += f" {s['ssid'][:10]}(V:{v:.0f})"
        line = (
            f"[{ts}] MOTION START  RSSI={STATE.last_dbm}dBm "
            f"({', '.join(causes)})  Magnitude={mag_label}{ap_summary}\n"
        )
        _append_motion_log(line)
    elif STATE.prev_motion_state and not STATE.motion_now:
        ts = time.strftime("%Y-%m-%d %H:%M:%S")
        ap_rssis = "  AP:"
        for s in STATE.ap_slots[:6]:
            if len(s["history"]) >= 1:
                ap_rssis += f" {s['ssid'][:10]}({s['rssi']}dBm)"
        _append_motion_log(f"[{ts}] MOTION END  Cooldown:{STATE.motion_cooldown}{ap_rssis}\n")
    STATE.prev_motion_state = STATE.motion_now
    if STATE.motion_now:
        STATE.sustained_motion_frames += 1
        if STATE.last_var > STATE.peak_var:
            STATE.peak_var = STATE.last_var
        if STATE.motion_cooldown <= 0:
            why = []
            if STATE.last_var > SETTINGS.threshold_var:
                why.append(f"VAR:{STATE.last_var:.1f}")
            if STATE.last_delta > SETTINGS.threshold_delta:
                why.append(f"DEL:{STATE.last_delta:.1f}")
            if STATE.last_ptp > SETTINGS.threshold_ptp:
                why.append(f"PTP:{STATE.last_ptp:.1f}")
            if slot_hit and not raw_hit:
                why.append("SLOT")
            STATE.motion_cooldown = SETTINGS.cooldown
            show_status(f"MOTION! {motion_magnitude(STATE.peak_var)} ({','.join(why)})")
        else:
            STATE.motion_cooldown = SETTINGS.cooldown
    else:
        STATE.sustained_motion_frames = 0
        if STATE.motion_cooldown > 0:
            STATE.motion_cooldown -= 1
        if STATE.motion_cooldown == 0:
            STATE.peak_var = 0.0


def _update_baseline() -> None:
    """Update the EWMA baseline and handle position-settle logic.

    Initialises the EWMA on the first frame, applies EWMA smoothing to
    the current dBm value, and resets detection state if sustained
    motion has persisted long enough (position settle).
    """
    if STATE.ewma_dbm is None:
        STATE.ewma_dbm = float(STATE.last_dbm)
    STATE.baseline_dbm = round(STATE.ewma_dbm)
    STATE.ewma_dbm = SETTINGS.ewma_alpha * STATE.last_dbm + (1 - SETTINGS.ewma_alpha) * STATE.ewma_dbm
    if STATE.sustained_motion_frames >= SETTINGS.position_settle_frames:
        hist_mean = sum(STATE.history) / len(STATE.history) if STATE.history else float(STATE.last_dbm)
        STATE.ewma_dbm = hist_mean
        STATE.baseline_dbm = round(hist_mean)
        STATE.motion_consec = 0
        STATE.sustained_motion_frames = 0
        STATE.motion_cooldown = 0
        STATE.peak_var = 0.0
        STATE.motion_now = False


def _update_fft() -> None:
    """Run FFT-based frequency analysis on the graph history at set intervals."""
    STATE.fft_tick += 1
    if STATE.fft_tick >= SETTINGS.fft_interval and len(STATE.graph_hist) >= 16:
        STATE.fft_tick = 0
        samples = STATE.graph_hist[-32:] if len(STATE.graph_hist) >= 32 else STATE.graph_hist
        actual_hz = estimate_sampling_rate(STATE.timestamps)
        STATE.fft_freq, _ = dominant_freq(samples, actual_hz)
        STATE.fft_label = freq_label(STATE.fft_freq)


def _adjust_metric(step: float) -> None:
    """Adjust the currently selected threshold metric by ``step``."""
    if STATE.edit_metric == "var":
        SETTINGS.threshold_var = round(max(0.1, min(500.0, SETTINGS.threshold_var + step)), 2)
    elif STATE.edit_metric == "del":
        SETTINGS.threshold_delta = round(max(0.1, min(100.0, SETTINGS.threshold_delta + step)), 2)
    else:
        SETTINGS.threshold_ptp = round(max(0.1, min(200.0, SETTINGS.threshold_ptp + step)), 2)


def process_detection() -> None:
    """Main detection cycle: read signal, check motion, handle events, update baseline and FFT.

    Orchestrates the detection pipeline:
        1. :func:`_read_signal` — fetch fresh RSSI data; exit early if no signal.
        2. :func:`_check_motion` — evaluate all threshold metrics.
        3. Consecutive-frame confirmation logic.
        4. :func:`_handle_motion_event` — process start/stop edges and cooldown.
        5. :func:`_update_baseline` — EWMA smoothing and position settle.
        6. :func:`_update_fft` — frequency analysis.
        7. Spatial hint, snapshot, and log trimming.
    """
    if not _read_signal():
        return
    raw_hit, slot_hit = _check_motion()
    if raw_hit or slot_hit:
        STATE.motion_consec += 1
    else:
        STATE.motion_consec = 0
    confirmed = STATE.motion_consec >= SETTINGS.motion_confirm
    STATE.motion_now = confirmed
    rising_edge = confirmed and not STATE.prev_motion_state
    _handle_motion_event(rising_edge, raw_hit, slot_hit)
    _update_baseline()
    _update_fft()
    enabled_count = sum(1 for s in STATE.ap_slots if s["enabled"])
    STATE.spatial_hint = _calc_spatial_hint() if enabled_count >= 2 else ""
    now = time.time()
    if now - STATE.last_snap_time >= SETTINGS.snap_interval:
        STATE.last_snap_time = now
        _save_snapshot()
    _trim_log(MOTION_LOG)


def _watchdog_pet() -> None:
    """Update the PID file timestamp as a health check."""
    try:
        with open(PID_FILE, "a") as f:
            os.utime(PID_FILE, None)
    except OSError:
        pass


def _handle_test_complete() -> None:
    rec = TEST_RECORDER.stop()
    if rec is None or not rec.timestamps:
        show_status("Test finished with no data to save.")
        return
    ts = time.strftime("%Y%m%d_%H%M%S")
    csv_path = os.path.join(_TEST_DIR, f"{rec.name}_{ts}.csv")
    rec.to_csv(csv_path)
    heat_path = generate_heatmap(rec)
    msg = f"Test done! CSV: {csv_path}"
    if heat_path:
        msg += f"  Heatmap: {heat_path}"
        try:
            os.startfile(heat_path)
        except OSError:
            pass
    show_status(msg)


def _finish_test_if_due() -> None:
    if TEST_RECORDER.active and TEST_RECORDER.recording and TEST_RECORDER.recording.is_complete:
        _handle_test_complete()


def main() -> None:
    """Entry point: check instance, run setup if needed, enter the key-loop."""
    check_existing_instance()
    loaded = Settings.from_file()
    for f in fields(loaded):
        setattr(SETTINGS, f.name, getattr(loaded, f.name))
    if needs_setup():
        if not is_admin():
            elevate()
        setup_wizard()
    elif not is_admin():
        elevate()
    WLAN_API.init()
    atexit.register(WLAN_API._cleanup)
    scan_aps(trigger=True)
    ctypes.windll.kernel32.SetConsoleTitleW("WiFi Motion CLI")
    show_status("Welcome! Press [S] to start.")
    while True:
        if STATE.running:
            process_detection()
            if TEST_RECORDER.active:
                TEST_RECORDER.record(
                    STATE.last_dbm, STATE.last_rssi,
                    STATE.last_var, STATE.last_delta, STATE.last_ptp,
                    STATE.last_rx_rate, STATE.last_tx_rate,
                    STATE.ap_slots,
                )
            show_status()
            _finish_test_if_due()
            _watchdog_pet()
            time.sleep(0.3)
        if msvcrt.kbhit():
            ch = msvcrt.getwch()
            if ch in ("\x00", "\xe0"):
                arrow = msvcrt.getwch()
                if arrow == "H":
                    _adjust_metric(+1.0)
                elif arrow == "P":
                    _adjust_metric(-1.0)
                elif arrow == "M":
                    _adjust_metric(+0.1)
                elif arrow == "K":
                    _adjust_metric(-0.1)
                if arrow in ("H", "P", "M", "K"):
                    key = STATE.edit_metric
                    new_val = {
                        "var": SETTINGS.threshold_var,
                        "del": SETTINGS.threshold_delta,
                        "ptp": SETTINGS.threshold_ptp,
                    }[key]
                    show_status(f"{STATE.edit_metric.upper()} threshold: {new_val:.2f}")
                    time.sleep(0.12)
                continue
            if ch in "123456":
                idx = int(ch) - 1
                if idx < len(STATE.ap_slots):
                    STATE.ap_slots[idx]["enabled"] = not STATE.ap_slots[idx]["enabled"]
                    state = "ON" if STATE.ap_slots[idx]["enabled"] else "OFF"
                    show_status(f"Slot {idx+1} ({STATE.ap_slots[idx]['ssid'][:20]}) \u2192 {state}")
                    time.sleep(0.4)
                else:
                    show_status(f"Slot {int(ch)} is empty.")
                    time.sleep(0.4)
                continue
            ch = ch.lower()
            if ch == "q":
                if TEST_RECORDER.active:
                    TEST_RECORDER.stop()
                STATE.running = False
                SETTINGS.to_file()
                sys.stdout.write("\033[2J\033[H")
                sys.stdout.flush()
                print("Goodbye!")
                break
            elif ch == "s":
                if not STATE.running:
                    STATE.prev_dbm = None
                    calibrate()
                    STATE.history.clear()
                    STATE.running = True
                    show_status("Detection started! [T] to stop.")
                else:
                    show_status("Already running!")
                    time.sleep(0.5)
            elif ch == "t":
                if STATE.running:
                    STATE.running = False
                    show_status("Detection stopped.")
                else:
                    show_status("Already stopped.")
                time.sleep(0.5)
            elif ch == "c":
                if STATE.running:
                    show_status("Stop detection first with [T].")
                    time.sleep(1)
                else:
                    calibrate()
                    show_status()
            elif ch in ("+", "="):
                SETTINGS.sensitivity = round(min(15.0, SETTINGS.sensitivity + 0.1), 1)
                show_status(f"Sensitivity {SETTINGS.sensitivity:.1f}/15.0")
                time.sleep(0.2)
            elif ch in ("-", "_"):
                SETTINGS.sensitivity = round(max(0.1, SETTINGS.sensitivity - 0.1), 1)
                show_status(f"Sensitivity {SETTINGS.sensitivity:.1f}/15.0")
                time.sleep(0.2)
            elif ch == "v":
                STATE.edit_metric = "var"
                show_status(f"Editing: VAR  [{SETTINGS.threshold_var:.2f}]")
                time.sleep(0.3)
            elif ch == "d":
                STATE.edit_metric = "del"
                show_status(f"Editing: DEL  [{SETTINGS.threshold_delta:.2f}]")
                time.sleep(0.3)
            elif ch == "p":
                STATE.edit_metric = "ptp"
                show_status(f"Editing: PTP  [{SETTINGS.threshold_ptp:.2f}]")
                time.sleep(0.3)
            elif ch == "z":
                SETTINGS.SOUND_ALARM = not SETTINGS.SOUND_ALARM
                state = "ON" if SETTINGS.SOUND_ALARM else "OFF"
                show_status(f"Audible alarm {state}")
                time.sleep(0.3)
            elif ch == "e":
                if not STATE.running:
                    show_status("Start detection first with [S].")
                    time.sleep(0.5)
                elif TEST_RECORDER.active:
                    show_status("A test is already running!")
                    time.sleep(0.5)
                else:
                    td = next((t for t in ALL_TESTS if t.key == "e"), None)
                    if td:
                        TEST_RECORDER.start(td.name, td.duration, td.phases)
                        show_status(f"{td.name} started! {td.duration}s")
            elif ch == "y":
                if not STATE.running:
                    show_status("Start detection first with [S].")
                    time.sleep(0.5)
                elif TEST_RECORDER.active:
                    show_status("A test is already running!")
                    time.sleep(0.5)
                else:
                    td = next((t for t in ALL_TESTS if t.key == "y"), None)
                    if td:
                        TEST_RECORDER.start(td.name, td.duration, td.phases)
                        show_status(f"{td.name} started! {td.duration}s")
            elif ch == "k":
                if not STATE.running:
                    show_status("Start detection first with [S].")
                    time.sleep(0.5)
                elif TEST_RECORDER.active:
                    show_status("A test is already running!")
                    time.sleep(0.5)
                else:
                    TEST_RECORDER.start("Custom Test", 30)
                    show_status("Custom test started! 30s — annotate with [Space].")
            elif ch == "m":
                if TEST_RECORDER.active:
                    show_status("Wait for the current test to finish first.")
                    time.sleep(0.5)
                elif not STATE.running:
                    show_status("Start detection first with [S].")
                    time.sleep(0.5)
                else:
                    td = show_test_menu()
                    if td:
                        TEST_RECORDER.start(td.name, td.duration, td.phases)
                        show_status(f"{td.name} started! {td.duration}s")
                    else:
                        show_status()
            elif ch == " ":
                if TEST_RECORDER.active:
                    TEST_RECORDER.annotate("NOTE")
                    show_status("Annotation added!")
                    time.sleep(0.3)
            elif ch == "r":
                if TEST_RECORDER.active:
                    show_status("Stop the test with [T] before calibrating.")
                    time.sleep(1)
                elif STATE.running:
                    show_status("Stop detection first with [T].")
                    time.sleep(0.5)
                else:
                    calibrate()
                    show_status()
            elif ch == "i":
                show_info()
                show_status()
            elif ch == "h":
                show_help()
                show_status()
        elif not STATE.running:
            time.sleep(0.2)


if __name__ == "__main__":
    main()
