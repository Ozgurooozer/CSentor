using System.Collections.Generic;

namespace WifiMotion.Core;

/// <summary>
/// Gorunen bir erisim noktasi (AP) yuvasini temsil eder. Python tarafinda
/// sozluk (dict) olarak tutulan yapinin yazili (typed) karsiligi.
/// </summary>
public sealed class ApSlot
{
    public string Ssid { get; set; } = "";
    public string Bssid { get; set; } = "";
    public int Rssi { get; set; }
    public int Quality { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Bu AP'ye ait son RSSI degerlerinin kayan penceresi.</summary>
    public List<double> History { get; set; } = new();

    /// <summary>Trend gostergesi: ▲ (yukari), ▼ (asagi), ▌ (sabit).</summary>
    public string Trend { get; set; } = "▌";

    public int ChannelFreq { get; set; }
    public int Channel { get; set; }
}

/// <summary>WLAN API taramasindan donen ham BSS girdisi.</summary>
public sealed class BssEntry
{
    public string Ssid { get; set; } = "";
    public string Bssid { get; set; } = "";
    public int Rssi { get; set; }
    public int Quality { get; set; }
    public int ChannelFreq { get; set; }
    public int Channel { get; set; }
}
