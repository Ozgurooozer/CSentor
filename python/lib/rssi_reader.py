from __future__ import annotations

import subprocess


def get_rssi() -> tuple[int | None, int | None, float | None, float | None]:
    """Run netsh wlan show interfaces and parse signal, RSSI, and data rates.

    Handles both Turkish and English locale output.
    Returns (signal_percent, rssi_dbm, receive_rate_mbps, transmit_rate_mbps).
    Never raises; returns (None, None, None, None) on any failure.
    """
    try:
        result = subprocess.run(
            ["netsh", "wlan", "show", "interfaces"],
            capture_output=True, text=True, encoding="utf-8", errors="ignore", timeout=5,
        )
        if result.returncode == 0:
            sig, dbm, rx, tx = None, None, None, None
            for line in result.stdout.split("\n"):
                ll = line.lower()
                if ("signal" in ll or "sinyal" in ll) and "%" in line:
                    try:
                        sig = int(line.split(":")[1].replace("%", "").strip())
                    except Exception:
                        pass
                elif "rssi" in ll and "bssid" not in ll:
                    try:
                        dbm = int(line.split(":")[1].strip())
                    except Exception:
                        pass
                elif "receive rate" in ll or "al\u0131m h\u0131z\u0131" in ll or ("al" in ll and "mbps" in ll):
                    try:
                        rx = float(line.split(":")[1].replace("Mbps", "").strip())
                    except Exception:
                        pass
                elif "transmit rate" in ll or "iletim h\u0131z\u0131" in ll or ("ilet" in ll and "mbps" in ll):
                    try:
                        tx = float(line.split(":")[1].replace("Mbps", "").strip())
                    except Exception:
                        pass
            return sig, dbm, rx, tx
        return None, None, None, None
    except Exception:
        return None, None, None, None


def can_read_rssi() -> bool:
    """Check whether netsh wlan can be invoked without permission errors."""
    try:
        r = subprocess.run(
            ["netsh", "wlan", "show", "interfaces"],
            capture_output=True, text=True, encoding="utf-8", errors="ignore", timeout=5,
        )
        out = (r.stdout + r.stderr).lower()
        return "location permission" not in out and "access is denied" not in out
    except Exception:
        return False
