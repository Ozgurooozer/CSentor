using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WifiMotion.Core;

/// <summary>
/// Windows WLAN API (wlanapi.dll) icin sarmalayici. Python <c>lib/wlan_api.py</c>
/// modulunun portu.
///
/// NOT: Python surumundeki <c>WLAN_BSS_ENTRY</c> yapisi hatali idi
/// (<c>dot11BssPhyType</c> alani eksik, <c>uRateSetLength</c> yanlis tipte ve
/// frekans kHz yerine MHz varsayilmis). Bu C# portunda native bellek duzeni
/// (offset'ler) Windows SDK'ya gore duzeltilmistir; boylece lRssi, uLinkQuality
/// ve kanal frekansi dogru okunur.
/// </summary>
public sealed class WlanApi : IDisposable
{
    // -----------------------------------------------------------------
    // P/Invoke bildirimleri
    // -----------------------------------------------------------------

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved,
        out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved,
        out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid,
        IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetNetworkBssList(IntPtr hClientHandle, ref Guid pInterfaceGuid,
        IntPtr pDot11Ssid, int dot11BssType, int bSecurityEnabled, IntPtr pReserved,
        out IntPtr ppWlanBssList);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    private const uint ErrorSuccess = 0;
    private const int Dot11BssTypeAny = 3;

    // WLAN_BSS_ENTRY icindeki dogru native offset'ler (x64, 8-bayt hizalama)
    private const int BssEntrySize = 360;
    private const int OffSsidLen = 0;       // ULONG
    private const int OffSsidData = 4;      // UCHAR[32]
    private const int OffBssid = 40;        // UCHAR[6]
    private const int OffRssi = 56;         // LONG (isaretli)
    private const int OffLinkQuality = 60;  // ULONG
    private const int OffChFreq = 92;       // ULONG (kHz)

    // WLAN_INTERFACE_INFO_LIST / WLAN_BSS_LIST baslik offset'leri
    private const int OffListNumItems = 0;     // dwNumberOfItems
    private const int OffFirstIfaceGuid = 8;   // ilk WLAN_INTERFACE_INFO.InterfaceGuid
    private const int OffBssNumItems = 4;      // dwNumberOfItems
    private const int BssListHeaderSize = 8;   // dwTotalSize + dwNumberOfItems

    private readonly object _lock = new();
    private IntPtr _handle = IntPtr.Zero;
    private Guid? _ifaceGuid;
    private bool _opened;

    /// <summary>
    /// WLAN API tutamaci acar ve kablosuz arayuzleri sayar.
    /// </summary>
    /// <returns>Bir kablosuz arayuz bulunduysa ve API hazirsa true.</returns>
    public bool Init()
    {
        lock (_lock)
        {
            CleanupNoLock();
            try
            {
                uint negotiated;
                if (WlanOpenHandle(2, IntPtr.Zero, out negotiated, out IntPtr h) != ErrorSuccess)
                    return false;
                _handle = h;
                _opened = true;

                if (WlanEnumInterfaces(_handle, IntPtr.Zero, out IntPtr listPtr) != ErrorSuccess)
                    return false;
                if (listPtr == IntPtr.Zero)
                    return false;
                try
                {
                    int n = Marshal.ReadInt32(listPtr, OffListNumItems);
                    if (n == 0)
                        return false;
                    var guidBytes = new byte[16];
                    Marshal.Copy(IntPtr.Add(listPtr, OffFirstIfaceGuid), guidBytes, 0, 16);
                    _ifaceGuid = new Guid(guidBytes);
                    return true;
                }
                finally
                {
                    WlanFreeMemory(listPtr);
                }
            }
            catch (Exception)
            {
                CleanupNoLock();
                return false;
            }
        }
    }

    /// <summary>WLAN tutamacini kapatir ve dahili durumu sifirlar.</summary>
    public void Cleanup()
    {
        lock (_lock)
        {
            CleanupNoLock();
        }
    }

    private void CleanupNoLock()
    {
        if (_opened && _handle != IntPtr.Zero)
        {
            try { WlanCloseHandle(_handle, IntPtr.Zero); }
            catch { /* yoksay */ }
        }
        _handle = IntPtr.Zero;
        _ifaceGuid = null;
        _opened = false;
    }

    /// <summary>Mevcut arayuzde arka planda bir WLAN taramasi tetikler.</summary>
    public void TriggerScan()
    {
        lock (_lock)
        {
            if (!_opened || _handle == IntPtr.Zero || _ifaceGuid is null)
                return;
            try
            {
                Guid guid = _ifaceGuid.Value;
                WlanScan(_handle, ref guid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception)
            {
                // yoksay
            }
        }
    }

    /// <summary>
    /// Kablosuz arayuzden BSS girdileri listesini alir. Kaliteye gore azalan sirada doner.
    /// </summary>
    /// <param name="trigger">true ise once taze bir tarama tetikler ve ~2sn bekler.</param>
    public List<BssEntry> Scan(bool trigger = false)
    {
        if (trigger)
        {
            TriggerScan();
            System.Threading.Thread.Sleep(2000);
        }

        lock (_lock)
        {
            if (!_opened || _handle == IntPtr.Zero || _ifaceGuid is null)
                return new List<BssEntry>();

            IntPtr bssListPtr = IntPtr.Zero;
            try
            {
                Guid guid = _ifaceGuid.Value;
                uint ret = WlanGetNetworkBssList(_handle, ref guid, IntPtr.Zero,
                    Dot11BssTypeAny, 0, IntPtr.Zero, out bssListPtr);
                if (ret != ErrorSuccess || bssListPtr == IntPtr.Zero)
                    return new List<BssEntry>();

                int num = Marshal.ReadInt32(bssListPtr, OffBssNumItems);
                if (num <= 0)
                    return new List<BssEntry>();

                var results = new List<BssEntry>(num);
                for (int i = 0; i < num; i++)
                {
                    IntPtr e = IntPtr.Add(bssListPtr, BssListHeaderSize + i * BssEntrySize);

                    int ssidLen = Marshal.ReadInt32(e, OffSsidLen);
                    if (ssidLen < 0) ssidLen = 0;
                    if (ssidLen > 32) ssidLen = 32;

                    string ssid;
                    if (ssidLen > 0)
                    {
                        var ssidRaw = new byte[ssidLen];
                        Marshal.Copy(IntPtr.Add(e, OffSsidData), ssidRaw, 0, ssidLen);
                        ssid = Encoding.UTF8.GetString(ssidRaw).Trim('\0');
                    }
                    else
                    {
                        ssid = "";
                    }

                    var bssidBytes = new byte[6];
                    Marshal.Copy(IntPtr.Add(e, OffBssid), bssidBytes, 0, 6);
                    string bssid = string.Join(":", bssidBytes.Select(b => b.ToString("x2")));

                    int rssi = Marshal.ReadInt32(e, OffRssi);
                    int quality = Math.Min(100, Marshal.ReadInt32(e, OffLinkQuality));
                    int freqKhz = Marshal.ReadInt32(e, OffChFreq);
                    int freqMhz = freqKhz / 1000;

                    if (!string.IsNullOrEmpty(ssid))
                    {
                        results.Add(new BssEntry
                        {
                            Ssid = ssid,
                            Bssid = bssid,
                            Rssi = rssi,
                            Quality = quality,
                            ChannelFreq = freqMhz,
                            Channel = FreqToChannel(freqMhz),
                        });
                    }
                }

                return results.OrderByDescending(x => x.Quality).ToList();
            }
            catch (Exception)
            {
                return new List<BssEntry>();
            }
            finally
            {
                if (bssListPtr != IntPtr.Zero)
                {
                    try { WlanFreeMemory(bssListPtr); }
                    catch { /* yoksay */ }
                }
            }
        }
    }

    /// <summary>WLAN API tutamaci acik mi.</summary>
    public bool IsAvailable()
    {
        lock (_lock)
        {
            return _opened && _handle != IntPtr.Zero;
        }
    }

    /// <summary>Merkez frekansini (MHz) WiFi kanal numarasina cevirir.</summary>
    private static int FreqToChannel(int freqMhz)
    {
        if (freqMhz is >= 2412 and <= 2484)
            return (freqMhz - 2407) / 5;
        if (freqMhz is >= 5180 and <= 5885)
            return (freqMhz - 5000) / 5;
        if (freqMhz is >= 5955 and <= 7115)
            return (freqMhz - 5950) / 5;
        return 0;
    }

    public void Dispose() => Cleanup();
}
