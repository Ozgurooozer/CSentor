from __future__ import annotations

from dataclasses import fields

from lib.config import Settings, DetectionState


def test_settings_defaults():
    s = Settings()
    assert s.window == 10
    assert s.calib_samples == 30
    assert s.cooldown == 3
    assert s.sensitivity == 15.0
    assert s.motion_confirm == 2
    assert s.SOUND_ALARM is True


def test_settings_custom():
    s = Settings(window=5, sensitivity=5.0, SOUND_ALARM=False)
    assert s.window == 5
    assert s.sensitivity == 5.0
    assert s.SOUND_ALARM is False


def test_detection_state_defaults():
    d = DetectionState()
    assert d.running is False
    assert d.motion_now is False
    assert d.motion_cooldown == 0
    assert d.peak_var == 0.0
    assert d.prev_dbm is None
    assert d.history == []
    assert d.motion_events == []
    assert d.edit_metric == "var"


def test_detection_state_custom():
    d = DetectionState(running=True, motion_now=True, edit_metric="del")
    assert d.running is True
    assert d.motion_now is True
    assert d.edit_metric == "del"


def test_motion_events_append():
    d = DetectionState()
    d.motion_events.append({"time": "12:00:00", "var": 5.0})
    assert len(d.motion_events) == 1
    assert d.motion_events[0]["var"] == 5.0


def test_settings_threshold_defaults():
    s = Settings()
    assert s.threshold_var == 10.0
    assert s.threshold_delta == 3.0
    assert s.threshold_ptp == 5.0


import json
import tempfile
from pathlib import Path


def test_settings_from_file_returns_instance():
    s = Settings.from_file("nonexistent.json")
    assert isinstance(s, Settings)


def test_settings_from_file_missing_file_returns_defaults():
    s = Settings.from_file("nonexistent_file_xyz.json")
    assert s.window == 10
    assert s.sensitivity == 15.0


def test_settings_from_file_loads_valid_json():
    data = {"window": 20, "sensitivity": 5.0}
    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
        json.dump(data, f)
        tmp = f.name
    try:
        s = Settings.from_file(tmp)
        assert s.window == 20
        assert s.sensitivity == 5.0
    finally:
        Path(tmp).unlink(missing_ok=True)


def test_settings_from_file_malformed_json_returns_defaults():
    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
        f.write("not valid json")
        tmp = f.name
    try:
        s = Settings.from_file(tmp)
        assert s.window == 10
        assert s.sensitivity == 15.0
    finally:
        Path(tmp).unlink(missing_ok=True)


def test_settings_to_file_writes_json():
    s = Settings(window=5)
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as f:
        tmp = f.name
    try:
        result = s.to_file(tmp)
        assert result is True
        assert Path(tmp).stat().st_size > 0
    finally:
        Path(tmp).unlink(missing_ok=True)


def test_settings_to_file_round_trip():
    s = Settings(window=7, sensitivity=3.0, SOUND_ALARM=False)
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as f:
        tmp = f.name
    try:
        s.to_file(tmp)
        s2 = Settings.from_file(tmp)
        assert s2.window == 7
        assert s2.sensitivity == 3.0
        assert s2.SOUND_ALARM is False
    finally:
        Path(tmp).unlink(missing_ok=True)


def test_settings_to_file_round_trip_all():
    s = Settings(window=15, calib_samples=50, cooldown=5, sensitivity=10.0,
                 motion_confirm=3, threshold_var=20.0, threshold_delta=6.0,
                 threshold_ptp=10.0, SOUND_ALARM=False)
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as f:
        tmp = f.name
    try:
        s.to_file(tmp)
        s2 = Settings.from_file(tmp)
        for fld in [f.name for f in fields(Settings)]:
            if fld == "CONFIG_FILE":
                continue
            assert getattr(s2, fld) == getattr(s, fld), f"Mismatch for {fld}"
    finally:
        Path(tmp).unlink(missing_ok=True)


def test_timestamps_defaults_empty():
    d = DetectionState()
    assert d.timestamps == []


def test_timestamps_append():
    d = DetectionState()
    d.timestamps.append(1.5)
    assert len(d.timestamps) == 1
    assert d.timestamps[0] == 1.5


def test_timestamps_type():
    d = DetectionState()
    d.timestamps.append(3.14)
    assert isinstance(d.timestamps[0], float)


def test_max_motion_events_default():
    d = DetectionState()
    assert d.max_motion_events == 50


def test_max_motion_events_custom():
    d = DetectionState(max_motion_events=10)
    assert d.max_motion_events == 10


def test_max_motion_events_caps_list():
    d = DetectionState(max_motion_events=3)
    for i in range(5):
        d.append_motion_event({"time": f"0{i}:00:00", "var": float(i)})
    assert len(d.motion_events) == 3


def test_append_motion_event_adds():
    d = DetectionState()
    d.append_motion_event({"time": "12:00:00", "var": 5.0})
    assert len(d.motion_events) == 1


def test_append_motion_event_caps_at_max():
    d = DetectionState(max_motion_events=50)
    for i in range(51):
        d.append_motion_event({"time": f"{i:02d}:00:00", "var": float(i)})
    assert len(d.motion_events) == 50


def test_append_motion_event_dict_structure():
    d = DetectionState()
    event = {"time": "12:00:00", "var": 5.0}
    d.append_motion_event(event)
    assert "time" in d.motion_events[0]
    assert "var" in d.motion_events[0]


def test_edit_index_defaults():
    d = DetectionState()
    assert d.edit_index == 0


def test_edit_index_custom():
    d = DetectionState(edit_index=5)
    assert d.edit_index == 5


def test_fft_tick_defaults():
    d = DetectionState()
    assert d.fft_tick == 0


def test_fft_tick_custom():
    d = DetectionState(fft_tick=10)
    assert d.fft_tick == 10
