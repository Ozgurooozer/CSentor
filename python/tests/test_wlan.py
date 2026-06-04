from unittest.mock import patch, MagicMock
from lib.wlan_api import WlanApi, DOT11_SSID, WLAN_BSS_ENTRY, WLAN_BSS_LIST


def test_wlan_init():
    api = WlanApi()
    result = api.init()
    assert isinstance(result, bool)


def test_wlan_available_when_not_init():
    api = WlanApi()
    assert api.is_available() is False


def test_wlan_scan_when_not_init():
    api = WlanApi()
    result = api.scan()
    assert result == []


def test_wlan_trigger_scan_when_not_init():
    api = WlanApi()
    api.trigger_scan()


@patch("lib.wlan_api.ctypes.windll")
def test_wlan_init_calls_load_library(mock_windll):
    mock_windll.LoadLibrary.side_effect = Exception("no wlanapi")
    api = WlanApi()
    result = api.init()
    assert result is False


def test_wlan_bss_entry_size():
    from ctypes import sizeof
    assert sizeof(WLAN_BSS_ENTRY) > 0


def test_wlan_bss_list_fields():
    from ctypes import sizeof
    assert sizeof(WLAN_BSS_LIST) >= 8


def test_dot11_ssid_fields():
    from ctypes import sizeof
    assert sizeof(DOT11_SSID) >= 36


def test_freq_to_channel_2ghz():
    from lib.wlan_api import _freq_to_channel
    assert _freq_to_channel(2412) == 1
    assert _freq_to_channel(2417) == 2
    assert _freq_to_channel(2437) == 6
    assert _freq_to_channel(2462) == 11


def test_freq_to_channel_5ghz():
    from lib.wlan_api import _freq_to_channel
    assert _freq_to_channel(5180) == 36
    assert _freq_to_channel(5200) == 40
    assert _freq_to_channel(5745) == 149


def test_freq_to_channel_unknown():
    from lib.wlan_api import _freq_to_channel
    assert _freq_to_channel(0) == 0
    assert _freq_to_channel(-1) == 0
    assert _freq_to_channel(2400) == 0


def test_cleanup_uninitialized():
    api = WlanApi()
    api._cleanup()


def test_cleanup_idempotent():
    api = WlanApi()
    api._cleanup()
    api._cleanup()


def test_cleanup_makes_unavailable():
    api = WlanApi()
    api._cleanup()
    assert api.is_available() is False


@patch("lib.wlan_api.atexit.register")
@patch("lib.wlan_api.ctypes.windll")
def test_atexit_registers_cleanup(mock_windll, mock_register):
    import ctypes
    mock_api = MagicMock()
    mock_windll.LoadLibrary.return_value = mock_api
    def open_handle(ver, reserved, pret, phandle):
        ctypes.memmove(phandle, ctypes.byref(ctypes.c_void_p(12345)), ctypes.sizeof(ctypes.c_void_p))
        return 0
    mock_api.WlanOpenHandle = open_handle
    iface_storage = ctypes.create_string_buffer(24)
    ctypes.memmove(iface_storage, ctypes.byref(ctypes.c_uint32(1)), 4)
    iface_storage[8] = 1
    mock_iface_ptr = ctypes.c_void_p(ctypes.addressof(iface_storage))
    def enum_interfaces(h, r, ptr):
        ctypes.memmove(ptr, ctypes.byref(mock_iface_ptr), ctypes.sizeof(ctypes.c_void_p))
        return 0
    mock_api.WlanEnumInterfaces = enum_interfaces
    api = WlanApi()
    api.init()
    mock_register.assert_called_once_with(api._cleanup)


@patch("lib.wlan_api.ctypes.windll")
def test_wlan_free_memory_called_on_exception(mock_windll):
    import ctypes
    mock_api = MagicMock()
    mock_windll.LoadLibrary.return_value = mock_api
    def open_handle(ver, reserved, pret, phandle):
        ctypes.memmove(phandle, ctypes.byref(ctypes.c_void_p(12345)), ctypes.sizeof(ctypes.c_void_p))
        return 0
    mock_api.WlanOpenHandle = open_handle
    iface_storage = ctypes.create_string_buffer(24)
    ctypes.memmove(iface_storage, ctypes.byref(ctypes.c_uint32(1)), 4)
    iface_storage[8] = 1
    mock_iface_ptr = ctypes.c_void_p(ctypes.addressof(iface_storage))
    def enum_interfaces(h, r, ptr):
        ctypes.memmove(ptr, ctypes.byref(mock_iface_ptr), ctypes.sizeof(ctypes.c_void_p))
        return 0
    mock_api.WlanEnumInterfaces = enum_interfaces
    api = WlanApi()
    api.init()
    hdr = WLAN_BSS_LIST()
    hdr.totalSize = ctypes.sizeof(WLAN_BSS_LIST) + ctypes.sizeof(WLAN_BSS_ENTRY)
    hdr.numberOfItems = 1
    entry = WLAN_BSS_ENTRY()
    entry.dot11Ssid.uSSIDLength = 4
    for i, b in enumerate(b"test"):
        entry.dot11Ssid.ucSSID[i] = b
    entry.ulChCenterFrequency = 2437
    total = ctypes.sizeof(WLAN_BSS_LIST) + ctypes.sizeof(WLAN_BSS_ENTRY)
    buf = (ctypes.c_uint8 * total)()
    base = ctypes.addressof(buf)
    ctypes.memmove(base, ctypes.byref(hdr), ctypes.sizeof(WLAN_BSS_LIST))
    ctypes.memmove(base + ctypes.sizeof(WLAN_BSS_LIST), ctypes.byref(entry), ctypes.sizeof(WLAN_BSS_ENTRY))
    mock_api.WlanGetNetworkBssList = lambda h, g, a, b, c, d, ptr: (ctypes.memmove(ptr, ctypes.byref(ctypes.c_void_p(base)), ctypes.sizeof(ctypes.c_void_p)) and 0)
    mock_api.WlanFreeMemory = MagicMock()
    with patch("lib.wlan_api.sorted", side_effect=ValueError("x")):
        api.scan()
    mock_api.WlanFreeMemory.assert_called_once()


@patch("lib.wlan_api.ctypes.windll")
def test_scan_result_includes_channel_and_freq(mock_windll):
    import ctypes
    mock_api = MagicMock()
    mock_windll.LoadLibrary.return_value = mock_api
    def open_handle(ver, reserved, pret, phandle):
        ctypes.memmove(phandle, ctypes.byref(ctypes.c_void_p(12345)), ctypes.sizeof(ctypes.c_void_p))
        return 0
    mock_api.WlanOpenHandle = open_handle
    iface_storage = ctypes.create_string_buffer(24)
    ctypes.memmove(iface_storage, ctypes.byref(ctypes.c_uint32(1)), 4)
    iface_storage[8] = 1
    mock_iface_ptr = ctypes.c_void_p(ctypes.addressof(iface_storage))
    def enum_interfaces(h, r, ptr):
        ctypes.memmove(ptr, ctypes.byref(mock_iface_ptr), ctypes.sizeof(ctypes.c_void_p))
        return 0
    mock_api.WlanEnumInterfaces = enum_interfaces
    api = WlanApi()
    api.init()
    hdr = WLAN_BSS_LIST()
    hdr.totalSize = ctypes.sizeof(WLAN_BSS_LIST) + ctypes.sizeof(WLAN_BSS_ENTRY)
    hdr.numberOfItems = 1
    entry = WLAN_BSS_ENTRY()
    entry.dot11Ssid.uSSIDLength = 4
    for i, b in enumerate(b"test"):
        entry.dot11Ssid.ucSSID[i] = b
    for i, b in enumerate([0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff]):
        entry.dot11Bssid[i] = b
    entry.lRssi = -50
    entry.uLinkQuality = 80
    entry.ulChCenterFrequency = 5180
    total = ctypes.sizeof(WLAN_BSS_LIST) + ctypes.sizeof(WLAN_BSS_ENTRY)
    buf = (ctypes.c_uint8 * total)()
    base = ctypes.addressof(buf)
    ctypes.memmove(base, ctypes.byref(hdr), ctypes.sizeof(WLAN_BSS_LIST))
    ctypes.memmove(base + ctypes.sizeof(WLAN_BSS_LIST), ctypes.byref(entry), ctypes.sizeof(WLAN_BSS_ENTRY))
    mock_api.WlanGetNetworkBssList = lambda h, g, a, b, c, d, ptr: (ctypes.memmove(ptr, ctypes.byref(ctypes.c_void_p(base)), ctypes.sizeof(ctypes.c_void_p)) and 0)
    mock_api.WlanFreeMemory = MagicMock()
    results = api.scan()
    assert len(results) == 1
    assert "channel" in results[0]
    assert "channel_freq" in results[0]
    assert results[0]["channel"] == 36
    assert results[0]["channel_freq"] == 5180


def test_bss_entry_fields():
    from lib.wlan_api import _freq_to_channel
    import ctypes
    entry = WLAN_BSS_ENTRY()
    entry.dot11Ssid.uSSIDLength = 4
    for i, b in enumerate(b"foo"):
        entry.dot11Ssid.ucSSID[i] = b
    for i, b in enumerate([0x01, 0x02, 0x03, 0x04, 0x05, 0x06]):
        entry.dot11Bssid[i] = b
    entry.lRssi = -60
    entry.uLinkQuality = 70
    entry.ulChCenterFrequency = 2462
    ssid_raw = bytes(entry.dot11Ssid.ucSSID[:entry.dot11Ssid.uSSIDLength])
    ssid = ssid_raw.decode("utf-8", errors="replace").strip("\x00")
    bssid = ":".join(f"{b:02x}" for b in entry.dot11Bssid)
    d = {
        "ssid": ssid,
        "bssid": bssid,
        "rssi": entry.lRssi,
        "quality": min(100, entry.uLinkQuality),
        "channel_freq": entry.ulChCenterFrequency,
        "channel": _freq_to_channel(entry.ulChCenterFrequency),
    }
    assert "channel" in d
    assert "channel_freq" in d
    assert d["channel"] == 11
    assert d["channel_freq"] == 2462
