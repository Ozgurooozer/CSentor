using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WifiMotion.Core;

/// <summary>
/// Hareket algilama yapilandirma ayarlari. Python <c>lib/config.py</c> icindeki
/// <c>Settings</c> dataclass'inin portu. JSON dosya bicimi Python ile uyumludur
/// (ayni alan adlari, ozellikle <c>SOUND_ALARM</c>).
/// </summary>
public sealed class Settings
{
    public const double SensitivityDefault = 15.0;
    public const string ConfigFileName = "wifi_motion_config.json";

    /// <summary>Kayan penceredeki RSSI ornek sayisi.</summary>
    [JsonPropertyName("window")]
    public int Window { get; set; } = 10;

    /// <summary>Ilk kalibrasyon sirasinda toplanacak ornek sayisi.</summary>
    [JsonPropertyName("calib_samples")]
    public int CalibSamples { get; set; } = 30;

    /// <summary>Hareket olayindan sonra yeniden tetiklenmeden once beklenen kare.</summary>
    [JsonPropertyName("cooldown")]
    public int Cooldown { get; set; } = 3;

    /// <summary>Algilama hassasiyeti; yuksek deger hassasiyeti azaltir.</summary>
    [JsonPropertyName("sensitivity")]
    public double Sensitivity { get; set; } = SensitivityDefault;

    /// <summary>Hareketi onaylamak icin gereken ardisik algilama sayisi.</summary>
    [JsonPropertyName("motion_confirm")]
    public int MotionConfirm { get; set; } = 2;

    /// <summary>Baslangictan sonra cihaz konumunun oturmasi icin beklenen kare.</summary>
    [JsonPropertyName("position_settle_frames")]
    public int PositionSettleFrames { get; set; } = 20;

    /// <summary>Otomatik anlik goruntu (snapshot) araligi (saniye).</summary>
    [JsonPropertyName("snap_interval")]
    public double SnapInterval { get; set; } = 10.0;

    /// <summary>Yakindaki erisim noktalari icin tarama araligi (kare).</summary>
    [JsonPropertyName("ap_scan_interval")]
    public int ApScanInterval { get; set; } = 10;

    /// <summary>FFT (frekans domeni) hesaplama araligi (kare).</summary>
    [JsonPropertyName("fft_interval")]
    public int FftInterval { get; set; } = 8;

    /// <summary>Canli grafigin karakter genisligi.</summary>
    [JsonPropertyName("graph_w")]
    public int GraphW { get; set; } = 58;

    /// <summary>Canli grafigin karakter yuksekligi.</summary>
    [JsonPropertyName("graph_h")]
    public int GraphH { get; set; } = 6;

    /// <summary>Ustel agirlikli hareketli ortalama (EWMA) yumusatma faktoru.</summary>
    [JsonPropertyName("ewma_alpha")]
    public double EwmaAlpha { get; set; } = 0.03;

    /// <summary>Alim sinyali (RX) icin varyans esigi.</summary>
    [JsonPropertyName("rx_var_threshold")]
    public double RxVarThreshold { get; set; } = 2.0;

    /// <summary>Hareket algilama icin varyans esigi.</summary>
    [JsonPropertyName("threshold_var")]
    public double ThresholdVar { get; set; } = 10.0;

    /// <summary>Hareket algilama icin delta (degisim) esigi.</summary>
    [JsonPropertyName("threshold_delta")]
    public double ThresholdDelta { get; set; } = 3.0;

    /// <summary>Hareket algilama icin tepe-tepe genlik esigi.</summary>
    [JsonPropertyName("threshold_ptp")]
    public double ThresholdPtp { get; set; } = 5.0;

    /// <summary>Hareket algilandiginda sesli alarm calsin mi.</summary>
    [JsonPropertyName("SOUND_ALARM")]
    public bool SoundAlarm { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Python uyumlulugu icin: bilinmeyen alanlari sessizce yoksay.
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Yapilandirma dosyasinin tam yolu (yurutulebilir dosyanin yaninda).</summary>
    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, ConfigFileName);

    /// <summary>
    /// Ayarlari JSON dosyasindan yukler; hata olursa varsayilanlara doner.
    /// Python <c>Settings.from_file</c> karsiligi.
    /// </summary>
    public static Settings FromFile(string? path = null)
    {
        string filePath = path ?? DefaultPath;
        try
        {
            if (!File.Exists(filePath))
                return new Settings();
            string json = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
            return loaded ?? new Settings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Settings();
        }
    }

    /// <summary>
    /// Mevcut ayarlari JSON dosyasina kaydeder. Python <c>Settings.to_file</c> karsiligi.
    /// </summary>
    public bool ToFile(string? path = null)
    {
        string filePath = path ?? DefaultPath;
        try
        {
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Tum alanlari baska bir Settings ornegine kopyalar.</summary>
    public void CopyFrom(Settings other)
    {
        Window = other.Window;
        CalibSamples = other.CalibSamples;
        Cooldown = other.Cooldown;
        Sensitivity = other.Sensitivity;
        MotionConfirm = other.MotionConfirm;
        PositionSettleFrames = other.PositionSettleFrames;
        SnapInterval = other.SnapInterval;
        ApScanInterval = other.ApScanInterval;
        FftInterval = other.FftInterval;
        GraphW = other.GraphW;
        GraphH = other.GraphH;
        EwmaAlpha = other.EwmaAlpha;
        RxVarThreshold = other.RxVarThreshold;
        ThresholdVar = other.ThresholdVar;
        ThresholdDelta = other.ThresholdDelta;
        ThresholdPtp = other.ThresholdPtp;
        SoundAlarm = other.SoundAlarm;
    }
}
