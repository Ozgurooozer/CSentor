from __future__ import annotations

import atexit
import ctypes
import logging
import time
from typing import List, Dict, Any, Optional

logger = logging.getLogger(__name__)


class DOT11_SSID(ctypes.Structure):
    """DOT11_SSID structure from Windows SDK."""
    _fields_ = [
        ("uSSIDLength", ctypes.c_ulong),
        ("ucSSID", ctypes.c_ubyte * 32),
    ]


class WLAN_RATE_SET(ctypes.Structure):
    """WLAN_RATE_SET structure from Windows SDK."""
    _fields_ = [
        ("uRateSetLength", ctypes.c_ushort),
        ("usRateSet", ctypes.c_ushort * 126),
    ]


class WLAN_BSS_ENTRY(ctypes.Structure):
    """WLAN_BSS_ENTRY structure from Windows SDK.

    See: https://docs.microsoft.com/en-us/windows/win32/api/wlanapi/ns-wlanapi-wlan_bss_entry
    """
    _fields_ = [
        ("dot11Ssid", DOT11_SSID),
        ("uPhyId", ctypes.c_ulong),
        ("dot11Bssid", ctypes.c_ubyte * 6),
        ("dot11BssType", ctypes.c_ulong),
        ("lRssi", ctypes.c_long),
        ("uLinkQuality", ctypes.c_ulong),
        ("bInRegDomain", ctypes.c_ubyte),
        ("usBeaconPeriod", ctypes.c_ushort),
        ("ullTimestamp", ctypes.c_ulonglong),
        ("ullHostTimestamp", ctypes.c_ulonglong),
        ("usCapabilityInformation", ctypes.c_ushort),
        ("ulChCenterFrequency", ctypes.c_ulong),
        ("wlanRateSet", WLAN_RATE_SET),
    ]


class WLAN_BSS_LIST(ctypes.Structure):
    """Header of WLAN_BSS_LIST structure used by WlanGetNetworkBssList."""
    _fields_ = [
        ("totalSize", ctypes.c_ulong),
        ("numberOfItems", ctypes.c_ulong),
    ]


def _freq_to_channel(freq_mhz: int) -> int:
    """Convert center frequency in MHz to WiFi channel number."""
    if 2412 <= freq_mhz <= 2484:
        return (freq_mhz - 2407) // 5
    if 5180 <= freq_mhz <= 5885:
        return (freq_mhz - 5000) // 5
    if 5955 <= freq_mhz <= 7115:
        return (freq_mhz - 5950) // 5
    return 0


class WlanApi:
    """Wrapper for the Windows WLAN API (wlanapi.dll)."""

    def __init__(self) -> None:
        """Initialize the API wrapper with default state."""
        self._handle: Optional[int] = None
        self._iface_guid: Optional[bytes] = None
        self._api: Optional[ctypes.WinDLL] = None

    def init(self) -> bool:
        """Open a handle to the WLAN API and enumerate wireless interfaces.

        Returns:
            True if a wireless interface was found and the API is ready.
        """
        self._cleanup()
        try:
            w = ctypes.windll.LoadLibrary("wlanapi.dll")
            ver = ctypes.c_ulong(0)
            h = ctypes.c_void_p()
            if w.WlanOpenHandle(2, None, ctypes.byref(ver), ctypes.byref(h)) != 0:
                logger.error("WlanOpenHandle failed")
                return False
            self._handle = h.value
            ilist = ctypes.c_void_p()
            if w.WlanEnumInterfaces(h, None, ctypes.byref(ilist)) != 0:
                logger.error("WlanEnumInterfaces failed")
                return False
            base = ilist.value
            if not base:
                logger.error("WlanEnumInterfaces returned null pointer")
                return False
            n = ctypes.c_uint32.from_address(base).value
            if n == 0:
                logger.warning("No wireless interfaces found")
                w.WlanFreeMemory(ilist)
                return False
            self._iface_guid = bytes((ctypes.c_uint8 * 16).from_address(base + 8))
            w.WlanFreeMemory(ilist)
            self._api = w
            atexit.register(self._cleanup)
            return True
        except Exception as e:
            logger.error("Failed to initialize WLAN API: %s", e)
            self._cleanup()
            return False

    def _cleanup(self) -> None:
        """Close the WLAN handle and reset internal state."""
        if self._api is not None and self._handle is not None:
            try:
                self._api.WlanCloseHandle(ctypes.c_void_p(self._handle), None)
            except Exception as e:
                logger.warning("WlanCloseHandle failed: %s", e)
        self._handle = None
        self._iface_guid = None
        self._api = None

    def trigger_scan(self) -> None:
        """Request a background WLAN scan on the current interface."""
        if not self._api or self._handle is None or self._iface_guid is None:
            logger.warning("trigger_scan called but API is not initialized")
            return
        try:
            guid = (ctypes.c_uint8 * 16)(*self._iface_guid)
            ret = self._api.WlanScan(
                ctypes.c_void_p(self._handle),
                ctypes.byref(guid), None, None, None,
            )
            if ret != 0:
                logger.warning("WlanScan returned %d", ret)
        except Exception as e:
            logger.error("Failed to trigger scan: %s", e)

    def scan(self, trigger: bool = False) -> List[Dict[str, Any]]:
        """Retrieve the list of BSS entries from the wireless interface.

        Args:
            trigger: If True, trigger a fresh scan and wait 2 seconds.

        Returns:
            A list of dicts with keys: ssid, bssid, rssi, quality.
            Sorted by quality descending.
        """
        if not self._api or self._handle is None or self._iface_guid is None:
            logger.warning("scan called but API is not initialized")
            return []
        if trigger:
            self.trigger_scan()
            time.sleep(2.0)
        bss_ptr = ctypes.c_void_p()
        try:
            guid = (ctypes.c_uint8 * 16)(*self._iface_guid)
            ret = self._api.WlanGetNetworkBssList(
                ctypes.c_void_p(self._handle),
                ctypes.byref(guid),
                None,
                ctypes.c_uint32(3),
                ctypes.c_int32(0),
                None,
                ctypes.byref(bss_ptr),
            )
            if ret != 0:
                logger.warning("WlanGetNetworkBssList returned %d", ret)
                return []
            if not bss_ptr.value:
                logger.warning("WlanGetNetworkBssList returned null pointer")
                return []

            base = bss_ptr.value
            header = WLAN_BSS_LIST.from_address(base)
            n = header.numberOfItems
            if n == 0:
                return []

            entry_size = ctypes.sizeof(WLAN_BSS_ENTRY)
            results: list = []
            pos = base + ctypes.sizeof(WLAN_BSS_LIST)
            for _ in range(n):
                entry = WLAN_BSS_ENTRY.from_address(pos)
                ssid_len = min(entry.dot11Ssid.uSSIDLength, 32)
                ssid_raw = bytes(entry.dot11Ssid.ucSSID[:ssid_len])
                try:
                    ssid = ssid_raw.decode("utf-8", errors="replace").strip("\x00")
                except Exception:
                    ssid = "?"
                bssid = ":".join(f"{b:02x}" for b in entry.dot11Bssid)
                rssi = entry.lRssi
                quality = min(100, entry.uLinkQuality)
                if ssid:
                    results.append({
                        "ssid": ssid,
                        "bssid": bssid,
                        "rssi": rssi,
                        "quality": quality,
                        "channel_freq": entry.ulChCenterFrequency,
                        "channel": _freq_to_channel(entry.ulChCenterFrequency),
                    })
                pos += entry_size

            return sorted(results, key=lambda x: x["quality"], reverse=True)
        except Exception as e:
            logger.error("Failed to scan BSS list: %s", e)
            return []
        finally:
            if bss_ptr.value:
                try:
                    self._api.WlanFreeMemory(bss_ptr)
                except Exception:
                    pass

    def is_available(self) -> bool:
        """Check whether the WLAN API handle is open."""
        return self._api is not None
