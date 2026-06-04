from __future__ import annotations

from unittest.mock import patch, MagicMock
from lib.rssi_reader import get_rssi, can_read_rssi


def test_get_rssi_success_with_all_fields():
    """parse signal%, dBm, rx, tx from typical netsh output"""
    netsh_output = """
Signal: 81%
RSSI: -65
    Receive rate: 433.3 Mbps
    Transmit rate: 300.0 Mbps
"""
    with patch("lib.rssi_reader.subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(
            returncode=0,
            stdout=netsh_output,
            stderr="",
        )
        sig, dbm, rx, tx = get_rssi()
        assert sig == 81
        assert dbm == -65
        assert rx == 433.3
        assert tx == 300.0


def test_get_rssi_turkish_locale():
    """parse Turkish locale netsh output"""
    netsh_output = """
Sinyal: 75%
RSSI: -70
    Alım hızı: 300.0 Mbps
    Iletim hızı: 150.0 Mbps
"""
    with patch("lib.rssi_reader.subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(
            returncode=0,
            stdout=netsh_output,
            stderr="",
        )
        sig, dbm, rx, tx = get_rssi()
        assert sig == 75
        assert dbm == -70
        assert rx == 300.0
        assert tx == 150.0


def test_get_rssi_no_signal():
    with patch("lib.rssi_reader.subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(
            returncode=0,
            stdout="No interface",
            stderr="",
        )
        sig, dbm, rx, tx = get_rssi()
        assert sig is None
        assert dbm is None
        assert rx is None
        assert tx is None


def test_get_rssi_nonzero_returncode():
    with patch("lib.rssi_reader.subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(
            returncode=1,
            stdout="",
            stderr="error",
        )
        sig, dbm, rx, tx = get_rssi()
        assert all(v is None for v in (sig, dbm, rx, tx))


def test_get_rssi_exception_safety():
    with patch("lib.rssi_reader.subprocess.run", side_effect=OSError("network")):
        sig, dbm, rx, tx = get_rssi()
        assert all(v is None for v in (sig, dbm, rx, tx))


def test_can_read_rssi_allowed():
    with patch("lib.rssi_reader.subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(
            returncode=0,
            stdout="Signal: 50%",
            stderr="",
        )
        assert can_read_rssi() is True


def test_can_read_rssi_blocked_location():
    with patch("lib.rssi_reader.subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(
            returncode=0,
            stdout="",
            stderr="Location permission",
        )
        assert can_read_rssi() is False


def test_can_read_rssi_blocked_access_denied():
    with patch("lib.rssi_reader.subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(
            returncode=5,
            stdout="",
            stderr="Access is denied",
        )
        assert can_read_rssi() is False


def test_can_read_rssi_exception():
    with patch("lib.rssi_reader.subprocess.run", side_effect=OSError):
        assert can_read_rssi() is False


def test_get_rssi_partial_data():
    """only signal and dBm present, no rate data"""
    netsh_output = """
Signal: 60%
RSSI: -72
"""
    with patch("lib.rssi_reader.subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(
            returncode=0,
            stdout=netsh_output,
            stderr="",
        )
        sig, dbm, rx, tx = get_rssi()
        assert sig == 60
        assert dbm == -72
        assert rx is None
        assert tx is None
