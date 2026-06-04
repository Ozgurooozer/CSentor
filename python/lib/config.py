import json
from dataclasses import dataclass, field, fields, asdict
from pathlib import Path
from typing import List, Optional

SENSITIVITY_DEFAULT = 15.0


@dataclass
class Settings:
    """Configuration settings for motion detection.

    Fields:
        window: Number of RSSI samples in the sliding window.
        calib_samples: Number of samples to collect during initial calibration.
        cooldown: Seconds to wait after a motion event before re-arming detection.
        sensitivity: Detection sensitivity threshold; higher values reduce sensitivity.
        motion_confirm: Number of consecutive detections required to confirm motion.
        position_settle_frames: Frames to wait for the device position to stabilise after startup.
        snap_interval: Interval in seconds between automatic terminal snapshots.
        ap_scan_interval: Interval between scans for nearby access points.
        fft_interval: Interval between FFT (frequency-domain) computations.
        graph_w: Width in characters of the live terminal graph.
        graph_h: Height in characters of the live terminal graph.
        ewma_alpha: Smoothing factor for the exponentially-weighted moving average.
        rx_var_threshold: Variance threshold for the receive signal.
        threshold_var: Variance threshold for triggering motion detection.
        threshold_delta: Delta (change) threshold for triggering motion detection.
        threshold_ptp: Peak-to-peak amplitude threshold for triggering motion detection.
        SOUND_ALARM: Whether to play an audible alarm when motion is detected.
    """
    window: int = 10
    calib_samples: int = 30
    cooldown: int = 3
    sensitivity: float = SENSITIVITY_DEFAULT
    motion_confirm: int = 2
    position_settle_frames: int = 20
    snap_interval: float = 10.0
    ap_scan_interval: int = 10
    fft_interval: int = 8
    graph_w: int = 58
    graph_h: int = 6
    ewma_alpha: float = 0.03
    rx_var_threshold: float = 2.0
    threshold_var: float = 10.0
    threshold_delta: float = 3.0
    threshold_ptp: float = 5.0
    SOUND_ALARM: bool = True

    CONFIG_FILE: str = "wifi_motion_config.json"

    @classmethod
    def from_file(cls, path: str | None = None) -> "Settings":
        """Load settings from a JSON config file, falling back to defaults."""
        filepath = path or cls.CONFIG_FILE
        try:
            with open(filepath, "r", encoding="utf-8") as f:
                data = json.load(f)
            valid_keys = {f.name for f in fields(cls)}
            filtered = {k: v for k, v in data.items() if k in valid_keys}
            return cls(**filtered)
        except (FileNotFoundError, json.JSONDecodeError, TypeError):
            return cls()

    def to_file(self, path: str | None = None) -> bool:
        """Save current settings to a JSON config file."""
        filepath = path or self.CONFIG_FILE
        try:
            with open(filepath, "w", encoding="utf-8") as f:
                json.dump(asdict(self), f, indent=2, default=str)
            return True
        except OSError:
            return False


@dataclass
class DetectionState:
    """Runtime state of the motion detection engine.

    Fields:
        running: Whether the detection loop is currently active.
        motion_now: Whether motion is detected in the current frame.
        motion_cooldown: Number of remaining cooldown frames before re-arming.
        peak_var: Highest variance value observed during the current motion burst.
        prev_dbm: RSSI value from the previous frame, in dBm.
        history: Rolling window of recent RSSI samples used for variance/delta/ptp calculations.
        graph_hist: Data points rendered on the live terminal graph.
        motion_consec: Count of consecutive frames where motion was detected.
        sustained_motion_frames: Total frames since the start of the current motion event.
        last_snap_time: Timestamp (monotonic clock) of the most recent terminal snapshot.
        prev_motion_state: Motion state from the previous frame (used for edge detection).
        ewma_dbm: Exponentially-weighted moving average of the dBm signal.
        baseline_dbm: Calibrated baseline RSSI value (dBm) recorded during setup.
        last_rssi: Raw RSSI value from the most recent sample.
        last_dbm: Most recent RSSI value expressed in dBm.
        last_var: Variance computed from the last window of samples.
        last_delta: Delta (change) between the most recent sample and the baseline.
        last_ptp: Peak-to-peak amplitude of the last window of samples.
        last_rx_rate: Most recent receive bitrate (Mbps).
        last_tx_rate: Most recent transmit bitrate (Mbps).
        rx_hist: History of receive signal values used by the FFT analysis.
        fft_freq: Dominant frequency (Hz) identified by the FFT.
        fft_label: Human-readable label for the dominant frequency (e.g. \"0.1 Hz\").
        fft_tick: Counter used to throttle FFT computation to every N frames.
        spatial_hint: Hints about spatial location derived from access-point data.
        ap_scan_tick: Counter used to throttle AP scanning to every N frames.
        edit_metric: Name of the metric currently selected for threshold tuning (\"var\", \"delta\", or \"ptp\").
        motion_events: List of timestamps (monotonic clock) recorded when motion events start.
    """
    running: bool = False
    motion_now: bool = False
    motion_cooldown: int = 0
    peak_var: float = 0.0
    prev_dbm: int | None = None
    history: List[int] = field(default_factory=list)
    graph_hist: List[int] = field(default_factory=list)
    motion_consec: int = 0
    sustained_motion_frames: int = 0
    last_snap_time: float = 0.0
    prev_motion_state: bool = False
    ewma_dbm: float | None = None
    baseline_dbm: int = 0
    last_rssi: int = 0
    last_dbm: int = 0
    last_var: float = 0.0
    last_delta: float = 0.0
    last_ptp: float = 0.0
    last_rx_rate: float = 0.0
    last_tx_rate: float = 0.0
    rx_hist: List[float] = field(default_factory=list)
    fft_freq: float = 0.0
    fft_label: str = ""
    fft_tick: int = 0
    spatial_hint: str = ""
    ap_scan_tick: int = 0
    edit_metric: str = "var"
    ap_slots: list = field(default_factory=list)
    timestamps: List[float] = field(default_factory=list)
    motion_events: list = field(default_factory=list)
    max_motion_events: int = 50
    edit_index: int = 0

    def append_motion_event(self, event: dict) -> None:
        if len(self.motion_events) >= self.max_motion_events:
            self.motion_events.pop(0)
        self.motion_events.append(event)
