from __future__ import annotations

import os
import time
import threading
from dataclasses import fields

from mcp.server.fastmcp import FastMCP

from lib.config import Settings, DetectionState
from lib.rssi_reader import get_rssi
from lib.stats import variance, peak_to_peak, sensitivity_params, variance_mw
from lib.wlan_api import WlanApi
from test_runner import TestRecorder, generate_heatmap, _TEST_DIR

mcp = FastMCP("wifi-motion")

SETTINGS = Settings()
STATE = DetectionState()
WLAN_API = WlanApi()
TEST_RECORDER = TestRecorder()
_ap_slots: list = []
_det_thread: threading.Thread | None = None
_lock = threading.Lock()
_ap_slots_lock = threading.Lock()
_MOTION_LOG = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "motion_log.txt"
)


def _update_ap_slots(fresh: list) -> None:
    global _ap_slots
    with _ap_slots_lock:
        enabled_map = {s["bssid"]: s["enabled"] for s in _ap_slots}
        history_map = {s["bssid"]: s["history"] for s in _ap_slots}
    new = []
    for ap in fresh[:6]:
        b = ap["bssid"]
        hist = list(history_map.get(b, []))
        hist.append(ap["rssi"])
        if len(hist) > SETTINGS.window:
            hist.pop(0)
        new.append({
            "ssid": ap["ssid"],
            "bssid": b,
            "rssi": ap["rssi"],
            "quality": ap["quality"],
            "enabled": enabled_map.get(b, True),
            "history": hist,
        })
    with _ap_slots_lock:
        _ap_slots = new


def _detection_loop() -> None:
    ap_tick = 0
    while True:
        sig, dbm = get_rssi()[:2]
        ap_tick += 1
        if ap_tick >= 10:
            _update_ap_slots(WLAN_API.scan())
            ap_tick = 0
        if dbm is None:
            time.sleep(0.5)
            continue
        with _lock:
            if not STATE.running:
                break
            prev = STATE.prev_dbm
            delta = abs(dbm - prev) if prev is not None else 0.0
            STATE.history.append(dbm)
            STATE.prev_dbm = dbm
            STATE.last_rssi = sig or 0
            STATE.last_dbm = dbm
            STATE.last_delta = delta
            var = variance(list(STATE.history), linear_power=True)
            ptp_v = peak_to_peak(list(STATE.history))
            STATE.last_var = var
            STATE.last_ptp = ptp_v
            motion = var > SETTINGS.threshold_var or delta > SETTINGS.threshold_delta or ptp_v > SETTINGS.threshold_ptp
            STATE.motion_now = motion
            if TEST_RECORDER.active:
                TEST_RECORDER.record(
                    STATE.last_dbm, STATE.last_rssi,
                    STATE.last_var, STATE.last_delta, STATE.last_ptp,
                    STATE.last_rx_rate, STATE.last_tx_rate,
                    _ap_slots,
                )
            if motion:
                if var > STATE.peak_var:
                    STATE.peak_var = var
                ev = {
                    "time": time.strftime("%H:%M:%S"),
                    "var": round(var, 2),
                    "delta": round(delta, 2),
                    "ptp": round(ptp_v, 2),
                }
                STATE.motion_events.append(ev)
                if len(STATE.motion_events) > 50:
                    STATE.motion_events.pop(0)
        time.sleep(0.3)


@mcp.tool()
def get_status() -> dict:
    with _lock:
        events = STATE.motion_events
        d = {
            "running": STATE.running,
            "motion": STATE.motion_now,
            "rssi_pct": STATE.last_rssi,
            "rssi_dbm": STATE.last_dbm,
            "var": round(STATE.last_var, 2),
            "delta": round(STATE.last_delta, 2),
            "ptp": round(STATE.last_ptp, 2),
            "threshold_var": SETTINGS.threshold_var,
            "threshold_delta": SETTINGS.threshold_delta,
            "threshold_ptp": SETTINGS.threshold_ptp,
            "sensitivity": SETTINGS.sensitivity,
            "peak_var": round(STATE.peak_var, 2),
            "motion_events_count": len(events),
        }
    with _ap_slots_lock:
        d["ap_count"] = len(_ap_slots)
    return d


@mcp.tool()
def start_detection() -> str:
    global _det_thread
    with _lock:
        if STATE.running:
            return "Already running."
        STATE.running = True
        STATE.prev_dbm = None
        STATE.peak_var = 0.0
        STATE.motion_events = []
        STATE.history.clear()
    _det_thread = threading.Thread(target=_detection_loop, daemon=True)
    _det_thread.start()
    return "Motion detection started."


@mcp.tool()
def stop_detection() -> str:
    with _lock:
        if not STATE.running:
            return "Already stopped."
        STATE.running = False
    return "Motion detection stopped."


@mcp.tool()
def calibrate(duration_sec: int = 15) -> str:
    with _lock:
        if STATE.running:
            return "Call stop_detection() first."
        sens = SETTINGS.sensitivity
    collected, deltas, prev = [], [], None
    for _ in range(max(5, duration_sec)):
        sig, dbm = get_rssi()[:2]
        if dbm is not None:
            if prev is not None:
                deltas.append(abs(dbm - prev))
            prev = dbm
            collected.append(dbm)
        time.sleep(1)
    if not collected:
        return "Could not read RSSI \u2014 check your WiFi connection."
    vs = [
        variance(collected[i : i + SETTINGS.window], linear_power=True)
        for i in range(max(1, len(collected) - SETTINGS.window + 1))
    ]
    bv = sum(vs) / len(vs)
    bd = sum(deltas) / len(deltas) if deltas else 0.0
    bp = peak_to_peak(collected)
    eff = max(1.0, float(sens))
    mult = max(0.05, 6.0 - (eff - 1) * 0.35)
    if sens < 1.0:
        mult *= sens
    with _lock:
        SETTINGS.threshold_var = round(max(0.1, bv * mult), 2)
        SETTINGS.threshold_delta = round(max(0.1, bd * mult), 2)
        SETTINGS.threshold_ptp = round(max(0.1, bp * mult), 2)
        tv = SETTINGS.threshold_var
        td = SETTINGS.threshold_delta
        tp = SETTINGS.threshold_ptp
    return f"Calibration done ({duration_sec}s). VAR={tv}  DEL={td}  PTP={tp}"


@mcp.tool()
def set_sensitivity(level: float) -> str:
    level = max(0.1, min(15.0, round(level, 1)))
    with _lock:
        SETTINGS.sensitivity = level
    return f"Sensitivity set to {level}/15.0."


@mcp.tool()
def set_threshold(metric: str, value: float) -> str:
    value = max(0.1, round(value, 2))
    key_map = {"var": "threshold_var", "del": "threshold_delta", "ptp": "threshold_ptp"}
    if metric not in key_map:
        return f"Invalid metric '{metric}'. Use 'var', 'del', or 'ptp'."
    with _lock:
        setattr(SETTINGS, key_map[metric], value)
    return f"{metric.upper()} threshold \u2192 {value}"


@mcp.tool()
def get_motion_events(last_n: int = 10) -> list:
    with _lock:
        events = STATE.motion_events
        return list(events[-last_n:])


@mcp.tool()
def get_ap_list() -> list:
    with _ap_slots_lock:
        return [
            {
                "slot": i + 1,
                "ssid": s["ssid"],
                "bssid": s["bssid"],
                "rssi_dbm": s["rssi"],
                "quality_pct": s["quality"],
                "enabled": s["enabled"],
                "channel_freq": s.get("channel_freq", 0),
                "channel": s.get("channel", 0),
            }
            for i, s in enumerate(_ap_slots)
        ]


@mcp.tool()
def get_motion_log(last_n: int = 20) -> str:
    if not os.path.exists(_MOTION_LOG):
        return "(No motion events recorded yet.)"
    try:
        with open(_MOTION_LOG, "r", encoding="utf-8") as f:
            lines = f.readlines()
        return "".join(lines[-last_n:]).strip()
    except Exception as e:
        return f"Error: {e}"


@mcp.tool()
def toggle_ap(slot: int) -> str:
    idx = slot - 1
    with _ap_slots_lock:
        if idx < 0 or idx >= len(_ap_slots):
            return f"Slot {slot} not found. Available slots: {len(_ap_slots)}"
        _ap_slots[idx]["enabled"] = not _ap_slots[idx]["enabled"]
        state = "ON" if _ap_slots[idx]["enabled"] else "OFF"
        ssid = _ap_slots[idx]['ssid']
    return f"Slot {slot} ({ssid}) \u2192 {state}"


@mcp.tool()
def scan_networks() -> list:
    fresh = WLAN_API.scan(trigger=True)
    _update_ap_slots(fresh)
    return fresh


@mcp.tool()
def start_test(test_type: str = "custom", duration: int = 30) -> str:
    if not STATE.running:
        return "Call start_detection() first."
    if TEST_RECORDER.active:
        return "A test is already running."
    test_names = {"handwave": "Hand Wave", "direction": "Direction Test", "custom": "Custom Test"}
    name = test_names.get(test_type, "Custom Test")
    TEST_RECORDER.start(name, max(5, duration))
    return f"{name} started ({duration}s)."

@mcp.tool()
def stop_test() -> str:
    if not TEST_RECORDER.active:
        return "No active test."
    rec = TEST_RECORDER.stop()
    if rec and rec.timestamps:
        ts = time.strftime("%Y%m%d_%H%M%S")
        csv_path = os.path.join(_TEST_DIR, f"{rec.name}_{ts}.csv")
        rec.to_csv(csv_path)
        heat_path = generate_heatmap(rec)
        result = {"csv": csv_path, "samples": len(rec.timestamps), "duration_s": rec.elapsed}
        if heat_path:
            result["heatmap"] = heat_path
        return f"Test stopped. CSV: {csv_path}, Heatmap: {heat_path}"
    return "Test stopped (no data)."

@mcp.tool()
def get_test_status() -> dict:
    if not TEST_RECORDER.active:
        return {"active": False, "remaining": 0, "test_name": ""}
    return {
        "active": True,
        "test_name": TEST_RECORDER.test_name,
        "remaining_s": round(TEST_RECORDER.remaining, 1),
        "elapsed_s": round(TEST_RECORDER.elapsed, 1),
        "samples": len(TEST_RECORDER.recording.timestamps) if TEST_RECORDER.recording else 0,
    }

@mcp.tool()
def annotate_test(text: str = "") -> str:
    if not TEST_RECORDER.active:
        return "No active test."
    TEST_RECORDER.annotate(text or "NOTE")
    return f"Annotation added: {text or 'NOTE'}"

@mcp.tool()
def generate_test_heatmap() -> str:
    if TEST_RECORDER.active or not TEST_RECORDER.recording:
        return "Stop the test first (stop_test)."
    rec = TEST_RECORDER.recording
    ts = time.strftime("%Y%m%d_%H%M%S")
    path = generate_heatmap(rec, os.path.join(_TEST_DIR, f"{rec.name}_{ts}.png"))
    return path or "Could not generate heatmap (matplotlib may be missing)."


if __name__ == "__main__":
    loaded = Settings.from_file()
    for f in fields(loaded):
        setattr(SETTINGS, f.name, getattr(loaded, f.name))
    WLAN_API.init()
    first_scan = WLAN_API.scan(trigger=True)
    if first_scan:
        _update_ap_slots(first_scan)
    mcp.run(transport="stdio")
