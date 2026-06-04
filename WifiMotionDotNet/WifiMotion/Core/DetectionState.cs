using System.Collections.Generic;

namespace WifiMotion.Core;

/// <summary>
/// Hareket algilama motorunun calisma-zamani durumu. Python <c>lib/config.py</c>
/// icindeki <c>DetectionState</c> dataclass'inin portu.
/// </summary>
public sealed class DetectionState
{
    /// <summary>Algilama dongusu su anda aktif mi.</summary>
    public bool Running { get; set; }

    /// <summary>Mevcut karede hareket algilandi mi.</summary>
    public bool MotionNow { get; set; }

    /// <summary>Yeniden tetiklenmeden once kalan cooldown kare sayisi.</summary>
    public int MotionCooldown { get; set; }

    /// <summary>Mevcut hareket patlamasinda gozlenen en yuksek varyans.</summary>
    public double PeakVar { get; set; }

    /// <summary>Onceki karedeki RSSI degeri (dBm).</summary>
    public int? PrevDbm { get; set; }

    /// <summary>Varyans/delta/ptp icin kullanilan son RSSI orneklerinin penceresi.</summary>
    public List<double> History { get; } = new();

    /// <summary>Canli grafikte cizilen veri noktalari.</summary>
    public List<double> GraphHist { get; } = new();

    /// <summary>Hareketin algilandigi ardisik kare sayisi.</summary>
    public int MotionConsec { get; set; }

    /// <summary>Mevcut hareket olayinin basindan beri toplam kare.</summary>
    public int SustainedMotionFrames { get; set; }

    /// <summary>En son anlik goruntunun zaman damgasi (saniye).</summary>
    public double LastSnapTime { get; set; }

    /// <summary>Onceki karedeki hareket durumu (kenar algilama icin).</summary>
    public bool PrevMotionState { get; set; }

    /// <summary>dBm sinyalinin ustel agirlikli hareketli ortalamasi.</summary>
    public double? EwmaDbm { get; set; }

    /// <summary>Kalibre edilmis taban RSSI degeri (dBm).</summary>
    public int BaselineDbm { get; set; }

    /// <summary>En son ornekteki ham RSSI yuzdesi.</summary>
    public int LastRssi { get; set; }

    /// <summary>En son RSSI degeri (dBm).</summary>
    public int LastDbm { get; set; }

    /// <summary>Son pencereden hesaplanan varyans.</summary>
    public double LastVar { get; set; }

    /// <summary>En son ornek ile bir onceki arasindaki delta.</summary>
    public double LastDelta { get; set; }

    /// <summary>Son pencerenin tepe-tepe genligi.</summary>
    public double LastPtp { get; set; }

    /// <summary>En son alim hizi (Mbps).</summary>
    public double LastRxRate { get; set; }

    /// <summary>En son iletim hizi (Mbps).</summary>
    public double LastTxRate { get; set; }

    /// <summary>FFT analizinde kullanilan alim sinyali gecmisi.</summary>
    public List<double> RxHist { get; } = new();

    /// <summary>FFT ile belirlenen baskin frekans (Hz).</summary>
    public double FftFreq { get; set; }

    /// <summary>Baskin frekans icin okunabilir etiket.</summary>
    public string FftLabel { get; set; } = "";

    /// <summary>FFT hesaplamasini her N karede bir tetiklemek icin sayac.</summary>
    public int FftTick { get; set; }

    /// <summary>AP verisinden turetilen konumsal ipucu.</summary>
    public string SpatialHint { get; set; } = "";

    /// <summary>AP taramasini her N karede bir tetiklemek icin sayac.</summary>
    public int ApScanTick { get; set; }

    /// <summary>Esik ayari icin secili metrik ("var", "del" veya "ptp").</summary>
    public string EditMetric { get; set; } = "var";

    /// <summary>Gorunen erisim noktasi yuvalari.</summary>
    public List<ApSlot> ApSlots { get; set; } = new();

    /// <summary>Ornek alma zaman damgalari (saniye).</summary>
    public List<double> Timestamps { get; } = new();

    /// <summary>Hareket olaylari listesi.</summary>
    public List<MotionEvent> MotionEvents { get; } = new();

    public int MaxMotionEvents { get; set; } = 50;

    public void AppendMotionEvent(MotionEvent ev)
    {
        if (MotionEvents.Count >= MaxMotionEvents)
            MotionEvents.RemoveAt(0);
        MotionEvents.Add(ev);
    }
}

/// <summary>Tek bir hareket olayinin ozeti.</summary>
public sealed class MotionEvent
{
    public string Time { get; set; } = "";
    public double Var { get; set; }
    public double Delta { get; set; }
    public double Ptp { get; set; }
}
