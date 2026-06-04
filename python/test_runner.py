from __future__ import annotations

import csv
import os
import time
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime
from typing import List, Optional, Tuple


_TEST_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "test_output")


@dataclass
class TestPhase:
    at_sec: float
    instruction: str
    annotation: str


@dataclass
class TestDefinition:
    key: str
    name: str
    duration: int
    desc: str
    phases: List[TestPhase]


ALL_TESTS: List[TestDefinition] = [
    TestDefinition("1", "FFT Profile", 30,
        "Measures frequency spectrum of movements at different speeds",
        [
            TestPhase(0,   "Stay still for 10s (breathe normally)",           "START: still"),
            TestPhase(10,  "Wave your hand at 1 Hz for 10s (once per second)", "START: 1Hz-wave"),
            TestPhase(20,  "Walk in place at normal pace for 10s",             "START: walking"),
        ]),
    TestDefinition("2", "Noise Floor", 60,
        "Stay completely still for 60s to measure baseline noise level",
        [
            TestPhase(0,   "Do not move for 60 seconds",                       "START: 60s-still"),
        ]),
    TestDefinition("3", "Through-Wall Detection", 30,
        "Tests detection success for motion behind a door/wall",
        [
            TestPhase(0,   "Stand still behind the door for 10s",              "START: wall-back-still"),
            TestPhase(10,  "Wave your hand behind the door for 10s",           "START: wall-back-wave"),
            TestPhase(20,  "Walk behind the door for 10s",                     "START: wall-back-walk"),
        ]),
    TestDefinition("4", "Masking", 30,
        "Detecting small movements in a noisy environment",
        [
            TestPhase(0,   "Turn on TV/music for 5s, stay still",             "START: noise-still"),
            TestPhase(10,  "Wave your hand in noisy environment for 10s",      "START: noise-wave"),
            TestPhase(20,  "5s silence then wave for 5s",                     "START: quiet-wave"),
        ]),
    TestDefinition("5", "Speed Scale", 30,
        "Detection across the speed scale from very slow to running",
        [
            TestPhase(0,   "Walk very slowly for 6s",                          "START: very-slow"),
            TestPhase(6,   "Walk slowly for 6s",                               "START: slow"),
            TestPhase(12,  "Walk at normal pace for 6s",                       "START: normal"),
            TestPhase(18,  "Walk fast for 6s",                                 "START: fast"),
            TestPhase(24,  "Run for 6s",                                       "START: run"),
        ]),
    TestDefinition("6", "Repeatability", 30,
        "Repeat the same movement 5 times to check VAR/DEL/PTP stability",
        [
            TestPhase(0,   "Hand wave #1  (5s)",                               "START: repeat-1"),
            TestPhase(6,   "Hand wave #2  (5s)",                               "START: repeat-2"),
            TestPhase(12,  "Hand wave #3  (5s)",                               "START: repeat-3"),
            TestPhase(18,  "Hand wave #4  (5s)",                               "START: repeat-4"),
            TestPhase(24,  "Hand wave #5  (5s)",                               "START: repeat-5"),
        ]),
    TestDefinition("7", "Room Occupancy", 60,
        "Enter/exit room scenario for occupancy detection",
        [
            TestPhase(0,   "Stay still inside the room for 10s",               "START: inside-still"),
            TestPhase(10,  "Leave the room and close the door, wait 10s",      "START: exit"),
            TestPhase(20,  "Room empty — wait outside for 10s",                "START: room-empty"),
            TestPhase(30,  "Re-enter the room for 10s",                        "START: re-enter"),
            TestPhase(40,  "Stay still inside the room again for 10s",         "START: inside-again"),
        ]),
    TestDefinition("8", "Breathing Pattern", 30,
        "FFT frequency analysis at different breathing depths",
        [
            TestPhase(0,   "Breathe shallowly for 10s (fast and light)",       "START: shallow-breath"),
            TestPhase(10,  "Breathe normally for 10s",                         "START: normal-breath"),
            TestPhase(20,  "Breathe deeply for 10s (slow and deep)",           "START: deep-breath"),
        ]),
    TestDefinition("9", "Fall Simulation", 20,
        "Effect of a sudden fall on PTP and VAR",
        [
            TestPhase(0,   "Stand still upright for 5s",                       "START: standing"),
            TestPhase(5,   "Slowly lean toward the floor for 5s",              "START: leaning"),
            TestPhase(10,  "Stay still on the floor for 5s",                   "START: on-floor"),
            TestPhase(15,  "Get up quickly for 5s",                            "START: getting-up"),
        ]),
    TestDefinition("0", "Gesture Library", 30,
        "Classification accuracy for 5 different hand gestures",
        [
            TestPhase(0,   "Raise your hand upward for 5s",                    "START: gesture-up"),
            TestPhase(6,   "Lower your hand downward for 5s",                  "START: gesture-down"),
            TestPhase(12,  "Push your hand to the left for 5s",                "START: gesture-left"),
            TestPhase(18,  "Push your hand to the right for 5s",               "START: gesture-right"),
            TestPhase(24,  "Draw a circle with your hand for 5s",              "START: gesture-circle"),
        ]),
    TestDefinition("e", "Hand Wave", 15,
        "Classic hand wave test in 4 phases",
        [
            TestPhase(0,   "Wait (baseline measurement)",                      "START: baseline"),
            TestPhase(3,   "Wave your hand to the right",                      "START: right-wave"),
            TestPhase(6,   "Wave your hand to the left",                       "START: left-wave"),
            TestPhase(9,   "Wave fast",                                        "START: fast"),
            TestPhase(12,  "Wave slow",                                        "START: slow"),
        ]),
    TestDefinition("y", "Direction Test", 18,
        "Motion detection with left/right direction changes",
        [
            TestPhase(0,   "Wait (baseline)",                                  "START: baseline"),
            TestPhase(3,   "Walk to the left",                                 "START: go-left"),
            TestPhase(6,   "Stop on the right",                                "START: wait-right"),
            TestPhase(9,   "Walk to the right",                                "START: go-right"),
            TestPhase(12,  "Stop on the left",                                 "START: wait-left"),
            TestPhase(15,  "Move quickly left and right",                      "START: left-right"),
        ]),
]


@dataclass
class TestRecording:
    name: str = ""
    duration: float = 0.0
    start_time: float = 0.0
    timestamps: List[float] = field(default_factory=list)
    rssi_dbm: List[int] = field(default_factory=list)
    rssi_pct: List[int] = field(default_factory=list)
    var: List[float] = field(default_factory=list)
    delta: List[float] = field(default_factory=list)
    ptp: List[float] = field(default_factory=list)
    rx_rate: List[float] = field(default_factory=list)
    tx_rate: List[float] = field(default_factory=list)
    ap_history: dict = field(default_factory=lambda: defaultdict(list))
    annotations: List[Tuple[float, str]] = field(default_factory=list)
    phases: List[TestPhase] = field(default_factory=list)

    @property
    def elapsed(self) -> float:
        if not self.start_time or not self.timestamps:
            return 0.0
        return self.timestamps[-1] - self.start_time

    @property
    def is_complete(self) -> bool:
        if not self.start_time:
            return True
        return (time.time() - self.start_time) >= self.duration

    @property
    def current_instruction(self) -> str:
        if not self.phases or not self.start_time:
            return ""
        elapsed = time.time() - self.start_time
        instr = ""
        for p in self.phases:
            if elapsed >= p.at_sec:
                instr = p.instruction
        return instr

    @property
    def current_annotation(self) -> str:
        if not self.phases or not self.start_time:
            return ""
        elapsed = time.time() - self.start_time
        ann = ""
        for p in self.phases:
            if elapsed >= p.at_sec:
                ann = p.annotation
        return ann

    def add_sample(self, dbm: int, pct: int, var_val: float, delta_val: float, ptp_val: float, rx: float, tx: float) -> None:
        self.timestamps.append(time.time())
        self.rssi_dbm.append(dbm)
        self.rssi_pct.append(pct)
        self.var.append(var_val)
        self.delta.append(delta_val)
        self.ptp.append(ptp_val)
        self.rx_rate.append(rx)
        self.tx_rate.append(tx)

    def annotate(self, text: str) -> None:
        self.annotations.append((time.time(), text[:40]))

    def annotate_if_due(self) -> None:
        if not self.phases or not self.start_time:
            return
        for p in self.phases:
            ts = self.start_time + p.at_sec
            if abs(time.time() - ts) < 0.3:
                already = any(abs(a[0] - ts) < 1.0 for a in self.annotations)
                if not already and p.annotation:
                    self.annotate(p.annotation)

    def to_csv(self, path: str) -> str:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        t0 = self.start_time or (self.timestamps[0] if self.timestamps else time.time())
        with open(path, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["test_name", self.name])
            w.writerow(["duration_s", self.duration])
            w.writerow(["samples", len(self.timestamps)])
            w.writerow([])
            w.writerow(["time_abs", "time_elapsed_s", "rssi_dbm", "rssi_pct",
                        "var", "delta", "ptp", "rx_mbps", "tx_mbps"])
            for i in range(len(self.timestamps)):
                w.writerow([
                    datetime.fromtimestamp(self.timestamps[i]).isoformat(),
                    round(self.timestamps[i] - t0, 3),
                    self.rssi_dbm[i],
                    self.rssi_pct[i],
                    f"{self.var[i]:.6e}",
                    f"{self.delta[i]:.6e}",
                    f"{self.ptp[i]:.6e}",
                    self.rx_rate[i],
                    self.tx_rate[i],
                ])
            if self.annotations:
                w.writerow([])
                w.writerow(["annotations"])
                w.writerow(["time_abs", "time_elapsed_s", "text"])
                for ats, text in self.annotations:
                    w.writerow([
                        datetime.fromtimestamp(ats).isoformat(),
                        round(ats - t0, 3),
                        text,
                    ])
            if self.ap_history:
                w.writerow([])
                w.writerow(["ap_history", "bssid -> rssi_list"])
                for bssid, vals in self.ap_history.items():
                    w.writerow([bssid] + vals)
        return path


class TestRecorder:
    def __init__(self) -> None:
        self.active: bool = False
        self.recording: Optional[TestRecording] = None

    @property
    def remaining(self) -> float:
        if not self.active or not self.recording:
            return 0.0
        return max(0.0, self.recording.duration - (time.time() - self.recording.start_time))

    @property
    def elapsed(self) -> float:
        if not self.active or not self.recording:
            return 0.0
        return time.time() - self.recording.start_time

    @property
    def test_name(self) -> str:
        return self.recording.name if self.recording else ""

    @property
    def current_instruction(self) -> str:
        if not self.active or not self.recording:
            return ""
        return self.recording.current_instruction

    def start(self, name: str, duration: int,
              phases: Optional[List[TestPhase]] = None) -> None:
        self.recording = TestRecording(name=name, duration=duration)
        self.recording.start_time = time.time()
        self.recording.phases = phases or []
        self.active = True

    def record(self, dbm: int, rssi_pct: int, var_val: float, delta_val: float,
               ptp_val: float, rx: float, tx: float, ap_slots: list) -> None:
        if not self.active or not self.recording:
            return
        self.recording.add_sample(dbm, rssi_pct, var_val, delta_val, ptp_val, rx, tx)
        for s in ap_slots:
            self.recording.ap_history[s["bssid"]].append(s["rssi"])
        self.recording.annotate_if_due()

    def annotate(self, text: str) -> None:
        if self.active and self.recording:
            actual = f"[{time.strftime('%H:%M:%S')}] {text}"
            self.recording.annotate(actual)

    def stop(self) -> Optional[TestRecording]:
        if not self.active or not self.recording:
            return None
        rec = self.recording
        self.active = False
        self.recording = None
        return rec


def _ensure_test_dir() -> str:
    os.makedirs(_TEST_DIR, exist_ok=True)
    return _TEST_DIR


def generate_heatmap(recording: TestRecording,
                     output_path: Optional[str] = None) -> Optional[str]:
    if not recording or not recording.timestamps:
        return None
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        import numpy as np
    except ImportError:
        return None

    output_path = output_path or os.path.join(
        _ensure_test_dir(),
        f"{recording.name}_{datetime.now().strftime('%Y%m%d_%H%M%S')}.png",
    )
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    t0 = recording.start_time or recording.timestamps[0]
    t = [(ts - t0) for ts in recording.timestamps]
    n = min(len(t), len(recording.rssi_dbm), len(recording.var),
            len(recording.delta), len(recording.ptp))
    if n < 2:
        return None
    t = t[:n]
    recording.rssi_dbm = recording.rssi_dbm[:n]
    recording.rssi_pct = recording.rssi_pct[:n]
    recording.var = recording.var[:n]
    recording.delta = recording.delta[:n]
    recording.ptp = recording.ptp[:n]
    recording.rx_rate = (recording.rx_rate[:n] if len(recording.rx_rate) >= n else
                         recording.rx_rate + [0] * (n - len(recording.rx_rate)))
    recording.tx_rate = (recording.tx_rate[:n] if len(recording.tx_rate) >= n else
                         recording.tx_rate + [0] * (n - len(recording.tx_rate)))
    duration = t[-1] if t else 0

    fig, axes = plt.subplots(4, 1, figsize=(14, 12), sharex=True)
    fig.suptitle(f"WiFi Motion Test: {recording.name}  |  "
                 f"{len(t)} samples  |  {duration:.1f}s",
                 fontsize=14, fontweight="bold")

    colors_rssi = "#2196F3"
    colors_var = "#FF5722"
    colors_delta = "#4CAF50"
    colors_ptp = "#9C27B0"

    ax1 = axes[0]
    ax1.plot(t, recording.rssi_dbm, color=colors_rssi, linewidth=1.5, label="RSSI (dBm)", zorder=3)
    ax1.set_ylabel("RSSI (dBm)", fontsize=10)
    ax1.legend(loc="upper right", fontsize=8)
    ax1.grid(True, alpha=0.3)
    ax1.set_ylim(min(recording.rssi_dbm) - 5, max(recording.rssi_dbm) + 5)

    for ats, label in recording.annotations:
        ax1.axvline(x=ats - t0, color="red", linestyle="--", alpha=0.4, linewidth=0.8)
        ax1.text(ats - t0, max(recording.rssi_dbm) + 1, label,
                 rotation=60, fontsize=7, ha="left", va="bottom",
                 bbox=dict(boxstyle="round,pad=0.2", facecolor="yellow", alpha=0.7))

    for p in recording.phases:
        ax1.axvline(x=p.at_sec, color="gray", linestyle=":", alpha=0.3, linewidth=0.5)

    if recording.var:
        ax2 = axes[1]
        ax2.plot(t, recording.var, color=colors_var, linewidth=1.2, label="Variance", alpha=0.9)
        ax2.plot(t, recording.delta, color=colors_delta, linewidth=1.2, label="Delta", alpha=0.9)
        ax2.plot(t, recording.ptp, color=colors_ptp, linewidth=1.2, label="Peak-to-Peak", alpha=0.9)
        ax2.set_ylabel("Metrics", fontsize=10)
        ax2.legend(loc="upper right", fontsize=8)
        ax2.grid(True, alpha=0.3)
        if max(recording.var + recording.delta + recording.ptp) > 0:
            ax2.set_yscale("log")
            ax2.set_ylabel("Metrics (log)", fontsize=10)

    ax3 = axes[2]
    if recording.ap_history:
        ap_bssids = list(recording.ap_history.keys())[:8]
        ap_short = [b.split(":")[0] + ".." + b[-2:] for b in ap_bssids]
        ap_matrix = []
        for bssid in ap_bssids:
            vals = list(recording.ap_history[bssid])
            padded = vals[:len(t)]
            while len(padded) < len(t):
                padded.append(None)
            ap_matrix.append(padded)
        if ap_matrix:
            arr = np.ma.masked_invalid(ap_matrix)
            im = ax3.imshow(arr, aspect="auto", cmap="RdYlGn_r", interpolation="bilinear")
            ax3.set_yticks(range(len(ap_short)))
            ax3.set_yticklabels(ap_short, fontsize=7)
            ax3.set_ylabel("AP (BSSID)", fontsize=10)
            cbar = plt.colorbar(im, ax=ax3, label="RSSI (dBm)", shrink=0.8)
            cbar.ax.tick_params(labelsize=7)
            for p in recording.phases:
                x = p.at_sec / duration * len(t) if duration > 0 else 0
                ax3.axvline(x=x, color="gray", linestyle=":", alpha=0.3, linewidth=0.5)
        else:
            ax3.text(0.5, 0.5, "No AP data", ha="center", va="center",
                     transform=ax3.transAxes, fontsize=10)
    else:
        ax3.text(0.5, 0.5, "No AP data\n(WLAN API may be disabled)",
                 ha="center", va="center", transform=ax3.transAxes, fontsize=10)

    ax4 = axes[3]
    if recording.rx_rate and len(recording.rx_rate) == len(t):
        ax4.plot(t, recording.rx_rate, color="#00BCD4", linewidth=1.2, label="RX (Mbps)")
    if recording.tx_rate and len(recording.tx_rate) == len(t):
        ax4.plot(t, recording.tx_rate, color="#FF9800", linewidth=1.2, label="TX (Mbps)")
    ax4.set_xlabel("Time (seconds)", fontsize=10)
    ax4.set_ylabel("Rate (Mbps)", fontsize=10)
    ax4b = ax4.twinx()
    ax4b.fill_between(t, recording.rssi_pct, alpha=0.12, color="gray", label="Signal %")
    ax4b.set_ylabel("Signal %", fontsize=10, color="gray")
    ax4b.tick_params(axis="y", colors="gray")
    lines1, labels1 = ax4.get_legend_handles_labels()
    lines2, labels2 = ax4b.get_legend_handles_labels()
    ax4.legend(lines1 + lines2, labels1 + labels2, loc="upper right", fontsize=8)
    ax4.grid(True, alpha=0.3)

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches="tight")
    plt.close(fig)

    return output_path
