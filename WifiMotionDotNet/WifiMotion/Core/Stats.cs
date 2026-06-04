using System;
using System.Collections.Generic;

namespace WifiMotion.Core;

/// <summary>
/// WiFi hareket algilama icin istatistik ve sinyal isleme yardimcilari.
/// Python <c>lib/stats.py</c> modulunun birebir portu.
/// </summary>
public static class Stats
{
    // -----------------------------------------------------------------
    // Sabitler
    // -----------------------------------------------------------------

    /// <summary>Varyans / tepe-tepe hesabi icin gereken minimum ornek sayisi.</summary>
    private const int MinSamples = 2;

    /// <summary>Frekans analizi (DFT) icin gereken minimum ornek sayisi.</summary>
    private const int MinSamplesFreq = 16;

    // Baskin frekans aramasi icin frekans bandi sinirlari (Hz)
    private const double FreqMinHz = 0.05;
    private const double FreqMaxHz = 3.5;

    /// <summary>WiFi CSI genlik verisi icin varsayilan ornekleme hizi (Hz).</summary>
    public const double DefaultSampleHz = 1.9;

    // Hareket tipi siniflandirma frekans esikleri (Hz)
    private const double FreqThresholdSakin = 0.15;
    private const double FreqThresholdNefes = 0.5;
    private const double FreqThresholdKucukHrt = 1.2;
    private const double FreqThresholdYurume = 2.5;

    // Hareket buyuklugu siniflandirma varyans esikleri
    private const double VarThresholdCokKucuk = 3.0;
    private const double VarThresholdKucuk = 10.0;
    private const double VarThresholdOrta = 30.0;
    private const double VarThresholdBuyuk = 100.0;

    // Referans seviye = 1.0'daki temel hassasiyet parametreleri
    private const double BaseMult = 6.0;
    private const double BaseMinVar = 25.0;
    private const double BaseMinDel = 8.0;
    private const double BaseMinPtp = 12.0;

    // Seviye > 1 icin uygulanan egim degerleri
    private const double SlopeMult = 0.35;
    private const double SlopeMinVar = 1.6;
    private const double SlopeMinDel = 0.45;
    private const double SlopeMinPtp = 0.7;

    // Hassasiyet parametreleri icin alt sinir degerleri
    private const double ClampMult = 0.05;
    private const double ClampParam = 0.1;

    // Sinyal kalitesi etiket esikleri (dBm)
    private const int SignalMukemmel = -50;
    private const int SignalCokIyi = -60;
    private const int SignalIyi = -70;
    private const int SignalOrta = -80;
    private const int SignalZayif = -90;

    // Sinyal kalitesi cubugu icin RSSI siralamasi
    private const int RssiMin = -100;
    private const int RssiMax = -30;
    private const int RssiRange = RssiMax - RssiMin; // 70

    /// <summary>Sinyal kalitesi gorsellestirmesindeki cubuk (bar) sayisi.</summary>
    private const int NumBars = 10;

    // -----------------------------------------------------------------
    // Temel donusumler
    // -----------------------------------------------------------------

    /// <summary>dBm degerini milliwatt'a (dogrusal guc) cevirir.</summary>
    public static double DbmToMw(double dbm) => Math.Pow(10.0, dbm / 10.0);

    /// <summary>Milliwatt'i tekrar dBm'e cevirir.</summary>
    public static double MwToDbm(double mw)
    {
        if (mw <= 0)
            return -100.0;
        return 10.0 * Math.Log10(mw);
    }

    // -----------------------------------------------------------------
    // Varyans
    // -----------------------------------------------------------------

    /// <summary>
    /// Varyansi dogrusal guc (mW) domeninde hesaplar. RSSI logaritmiktir;
    /// fiziksel olarak anlamli olmasi icin varyans dogrusal gucte hesaplanmalidir.
    /// </summary>
    public static double VarianceMw(IReadOnlyList<double> data, int ddof = 1)
    {
        if (data.Count < 2)
            return 0.0;
        var mw = new double[data.Count];
        double sum = 0.0;
        for (int i = 0; i < data.Count; i++)
        {
            mw[i] = DbmToMw(data[i]);
            sum += mw[i];
        }
        double mean = sum / mw.Length;
        double acc = 0.0;
        foreach (double v in mw)
            acc += (v - mean) * (v - mean);
        return acc / (mw.Length - ddof);
    }

    /// <summary>
    /// Bir veri kumesinin (orneklem) varyansini hesaplar. Bessel duzeltmesi
    /// (n - ddof) kullanir. <paramref name="linearPower"/> true ise veri once
    /// dBm'den mW'a cevrilir.
    /// </summary>
    public static double Variance(IReadOnlyList<double> data, int ddof = 1, bool linearPower = false)
    {
        if (data.Count < MinSamples)
            return 0.0;

        if (linearPower)
        {
            var mw = new double[data.Count];
            double s = 0.0;
            for (int i = 0; i < data.Count; i++)
            {
                mw[i] = DbmToMw(data[i]);
                s += mw[i];
            }
            double m = s / mw.Length;
            double a = 0.0;
            foreach (double v in mw)
                a += (v - m) * (v - m);
            return a / (mw.Length - ddof);
        }

        double sum = 0.0;
        foreach (double v in data)
            sum += v;
        double mean = sum / data.Count;
        double acc = 0.0;
        foreach (double v in data)
            acc += (v - mean) * (v - mean);
        return acc / (data.Count - ddof);
    }

    // -----------------------------------------------------------------
    // Tepe-tepe ve diger metrikler
    // -----------------------------------------------------------------

    /// <summary>Veri kumesinin tepe-tepe genligini (max - min) hesaplar.</summary>
    public static double PeakToPeak(IReadOnlyList<double> data)
    {
        if (data.Count < MinSamples)
            return 0.0;
        double min = data[0], max = data[0];
        foreach (double v in data)
        {
            if (v < min) min = v;
            if (v > max) max = v;
        }
        return max - min;
    }

    /// <summary>Ortalamaya gore merkezlenmis sinyalin sifir-gecis oranini (ZCR) hesaplar.</summary>
    public static double ZeroCrossingRate(IReadOnlyList<double> data)
    {
        if (data.Count < 3)
            return 0.0;
        double sum = 0.0;
        foreach (double v in data)
            sum += v;
        double mean = sum / data.Count;
        int crossings = 0;
        for (int i = 1; i < data.Count; i++)
        {
            bool prevPos = (data[i - 1] - mean) >= 0;
            bool curPos = (data[i] - mean) >= 0;
            if (prevPos != curPos)
                crossings++;
        }
        return (double)crossings / (data.Count - 1);
    }

    /// <summary>Verinin orneklem carpikligini (skewness) hesaplar.</summary>
    public static double Skewness(IReadOnlyList<double> data, int ddof = 1)
    {
        if (data.Count < 3)
            return 0.0;
        int n = data.Count;
        double sum = 0.0;
        foreach (double v in data)
            sum += v;
        double mean = sum / n;
        double m2 = 0.0, m3 = 0.0;
        foreach (double v in data)
        {
            double d = v - mean;
            m2 += d * d;
            m3 += d * d * d;
        }
        m2 /= (n - ddof);
        if (m2 == 0)
            return 0.0;
        m3 /= (n - ddof);
        return m3 / Math.Pow(m2, 1.5);
    }

    /// <summary>Verinin fazla basikligini (excess kurtosis) hesaplar (normal dagilim = 0).</summary>
    public static double Kurtosis(IReadOnlyList<double> data, int ddof = 1)
    {
        if (data.Count < 4)
            return 0.0;
        int n = data.Count;
        double sum = 0.0;
        foreach (double v in data)
            sum += v;
        double mean = sum / n;
        double m2 = 0.0, m4 = 0.0;
        foreach (double v in data)
        {
            double d = v - mean;
            double d2 = d * d;
            m2 += d2;
            m4 += d2 * d2;
        }
        m2 /= (n - ddof);
        if (m2 == 0)
            return 0.0;
        m4 /= (n - ddof);
        return m4 / (m2 * m2) - 3.0;
    }

    // -----------------------------------------------------------------
    // Frekans analizi
    // -----------------------------------------------------------------

    /// <summary>
    /// Zaman-domeni sinyalindeki baskin frekansi ve gucunu bulur. Ayrik Fourier
    /// binleri uzerinde (k = 1 .. n/2) kaba-kuvvet aramasi yapar; [FreqMin, FreqMax]
    /// araligindaki en yuksek guce sahip bin dondurulur.
    /// </summary>
    /// <returns>(frekans_hz, guc); MinSamplesFreq'ten az ornek varsa (0, 0).</returns>
    public static (double Freq, double Power) DominantFreq(IReadOnlyList<double> samples, double sampleHz)
    {
        int n = samples.Count;
        if (n < MinSamplesFreq)
            return (0.0, 0.0);

        double sum = 0.0;
        foreach (double v in samples)
            sum += v;
        double mean = sum / n;

        var x = new double[n];
        for (int i = 0; i < n; i++)
            x[i] = samples[i] - mean;

        double bestP = 0.0, bestF = 0.0;
        for (int k = 1; k <= n / 2; k++)
        {
            double freq = k * sampleHz / n;
            if (freq < FreqMinHz || freq > FreqMaxHz)
                continue;
            double re = 0.0, im = 0.0;
            for (int i = 0; i < n; i++)
            {
                double angle = 2 * Math.PI * k * i / n;
                re += x[i] * Math.Cos(angle);
                im += x[i] * Math.Sin(angle);
            }
            double p = re * re + im * im;
            if (p > bestP)
            {
                bestP = p;
                bestF = freq;
            }
        }
        return (bestF, bestP);
    }

    /// <summary>Bir frekans degerini okunabilir hareket etiketine (Turkce) esler.</summary>
    public static string FreqLabel(double hz)
    {
        if (hz <= 0)
            return "";
        if (hz < FreqThresholdSakin)
            return "Sakin";
        if (hz < FreqThresholdNefes)
            return "Nefes~";
        if (hz < FreqThresholdKucukHrt)
            return "Kucuk hrt";
        if (hz < FreqThresholdYurume)
            return "Yurume~";
        return "Hizli hrt";
    }

    /// <summary>Bir varyans degerini okunabilir hareket buyuklugu etiketine esler.</summary>
    public static string MotionMagnitude(double var)
    {
        if (var < VarThresholdCokKucuk)
            return "Cok Kucuk";
        if (var < VarThresholdKucuk)
            return "Kucuk";
        if (var < VarThresholdOrta)
            return "Orta";
        if (var < VarThresholdBuyuk)
            return "Buyuk";
        return "Cok Buyuk";
    }

    // -----------------------------------------------------------------
    // Hassasiyet ve ornekleme hizi
    // -----------------------------------------------------------------

    /// <summary>
    /// Hareket algilama esikleri icin hassasiyet parametrelerini hesaplar.
    /// Her parametre temel degerden, seviye-basina egim ile dogrusal interpolasyonla turetilir.
    /// </summary>
    /// <returns>(mult, minVar, minDel, minPtp), alt sinirlar uygulanmis halde.</returns>
    public static (double Mult, double MinVar, double MinDel, double MinPtp) SensitivityParams(double level)
    {
        double eff = Math.Max(1.0, level);

        double mult = BaseMult - (eff - 1) * SlopeMult;
        double minVar = BaseMinVar - (eff - 1) * SlopeMinVar;
        double minDel = BaseMinDel - (eff - 1) * SlopeMinDel;
        double minPtp = BaseMinPtp - (eff - 1) * SlopeMinPtp;

        if (level < 1.0)
        {
            double scale = level;
            mult *= scale;
            minVar *= scale;
            minDel *= scale;
            minPtp *= scale;
        }

        return (
            Math.Max(ClampMult, mult),
            Math.Max(ClampParam, minVar),
            Math.Max(ClampParam, minDel),
            Math.Max(ClampParam, minPtp));
    }

    /// <summary>
    /// Zaman damgalari listesinden (saniye) gercek ornekleme hizini kestirir.
    /// Aykiri degerlere karsi dayanikli olmasi icin medyan araliginin tersini dondurur.
    /// </summary>
    public static double EstimateSamplingRate(IReadOnlyList<double> timestamps)
    {
        if (timestamps.Count < 2)
            return 1.0;
        var intervals = new List<double>(timestamps.Count - 1);
        for (int i = 0; i < timestamps.Count - 1; i++)
            intervals.Add(timestamps[i + 1] - timestamps[i]);
        if (intervals.Count == 0)
            return 1.0;
        intervals.Sort();
        double median = intervals[intervals.Count / 2];
        return median > 0 ? 1.0 / median : 1.0;
    }

    /// <summary>
    /// WiFi sinyal gucu icin bir etiket ve metin tabanli cubuk gorseli dondurur.
    /// </summary>
    /// <returns>(etiket, cubuk). dbm null ise "sinyal yok" gosterilir.</returns>
    public static (string Label, string Bar) SignalQuality(int? dbm)
    {
        if (dbm is null)
            return ("---     ", "[" + new string(' ', NumBars) + "]");

        int value = dbm.Value;
        int clamped = Math.Max(RssiMin, Math.Min(RssiMax, value));
        int bars = (int)Math.Round((double)(clamped - RssiMin) / RssiRange * NumBars, MidpointRounding.AwayFromZero);
        string barStr = "[" + new string('█', bars) + new string('░', NumBars - bars) + "]";

        string label;
        if (value >= SignalMukemmel)
            label = "Mukemmel";
        else if (value >= SignalCokIyi)
            label = "Cok Iyi ";
        else if (value >= SignalIyi)
            label = "Iyi     ";
        else if (value >= SignalOrta)
            label = "Orta    ";
        else if (value >= SignalZayif)
            label = "Zayif   ";
        else
            label = "CokZayif";

        return (label, barStr);
    }
}
