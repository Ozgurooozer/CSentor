using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WifiMotion.Testing;

/// <summary>Bir test asamasi: belirli bir saniyede gosterilecek yonerge ve not.</summary>
public sealed record TestPhase(double AtSec, string Instruction, string Annotation);

/// <summary>Bir uzman testinin tanimi. Python <c>TestDefinition</c> karsiligi.</summary>
public sealed record TestDefinition(string Key, string Name, int Duration, string Desc, IReadOnlyList<TestPhase> Phases);

/// <summary>Yerlesik uzman testleri. Python <c>ALL_TESTS</c> listesinin portu.</summary>
public static class Tests
{
    public static readonly IReadOnlyList<TestDefinition> All = new List<TestDefinition>
    {
        new("1", "FFT Profili", 30,
            "Farkli hizdaki hareketlerin frekans spektrumunu olcer",
            new[]
            {
                new TestPhase(0,  "10sn hareketsiz durun (normal nefes alin)", "BAS: sakin"),
                new TestPhase(10, "10sn elinizi 1 Hz'de sallayin (saniyede bir)", "BAS: 1Hz el"),
                new TestPhase(20, "10sn normal hizda yerinizde yuruyun", "BAS: yurume"),
            }),
        new("2", "Gurultu Karakterizasyonu", 60,
            "Bos ortamda 60sn tamamen hareketsiz, bazal gurultu seviyesi olculur",
            new[]
            {
                new TestPhase(0, "60sn boyunca hic hareket etmeyin", "BAS: 60sn sakin"),
            }),
        new("3", "Duvardan Algilama", 30,
            "Kapi arkasindaki hareketin algilanma basarimi",
            new[]
            {
                new TestPhase(0,  "Kapi arkasinda 10sn hareketsiz durun", "BAS: duvar-arka-sakin"),
                new TestPhase(10, "Kapi arkasinda 10sn el sallayin", "BAS: duvar-arka-el"),
                new TestPhase(20, "Kapi arkasinda 10sn yuruyun", "BAS: duvar-arka-yuru"),
            }),
        new("4", "Maskeleme", 30,
            "Gurultulu ortamda kucuk hareketleri algilama",
            new[]
            {
                new TestPhase(0,  "5sn TV/muzik acin, hareket etmeyin", "BAS: gurultu-sakin"),
                new TestPhase(10, "10sn gurultulu ortamda el sallayin", "BAS: gurultu-el"),
                new TestPhase(20, "5sn sessizlik, 5sn el sallayin", "BAS: sessiz-el"),
            }),
        new("5", "Hiz Skalasi", 30,
            "Cok yavastan kosuya kadar hiz skalasinda algilama",
            new[]
            {
                new TestPhase(0,  "6sn cok yavas yuruyun", "BAS: cok-yavas"),
                new TestPhase(6,  "6sn yavas yuruyun", "BAS: yavas"),
                new TestPhase(12, "6sn normal hizda yuruyun", "BAS: normal"),
                new TestPhase(18, "6sn hizli yuruyun", "BAS: hizli"),
                new TestPhase(24, "6sn kosun", "BAS: kosu"),
            }),
        new("6", "Tekrarlanabilirlik", 30,
            "Ayni hareketi 5 kez tekrarlayarak VAR/DEL/PTP kararliligi",
            new[]
            {
                new TestPhase(0,  "El sallama #1  (5sn)", "BAS: tekrar-1"),
                new TestPhase(6,  "El sallama #2  (5sn)", "BAS: tekrar-2"),
                new TestPhase(12, "El sallama #3  (5sn)", "BAS: tekrar-3"),
                new TestPhase(18, "El sallama #4  (5sn)", "BAS: tekrar-4"),
                new TestPhase(24, "El sallama #5  (5sn)", "BAS: tekrar-5"),
            }),
        new("7", "Oda Doluluk", 60,
            "Odaya girip cikma senaryosu, doluluk tespiti",
            new[]
            {
                new TestPhase(0,  "10sn odada hareketsiz durun", "BAS: icerde-sakin"),
                new TestPhase(10, "10sn odadan cikin ve kapati kapatip bekleyin", "BAS: cikis"),
                new TestPhase(20, "10sn oda bos (disarda bekleyin)", "BAS: oda-bos"),
                new TestPhase(30, "10sn odaya geri girin", "BAS: geri-giris"),
                new TestPhase(40, "10sn odada hareketsiz durun", "BAS: icerde-tekrar"),
            }),
        new("8", "Solunum Paterni", 30,
            "Farkli nefes derinliklerinde FFT frekans analizi",
            new[]
            {
                new TestPhase(0,  "10sn yuzeysel nefes alin (hizli ve hafif)", "BAS: yuzeysel-nefes"),
                new TestPhase(10, "10sn normal nefes alin", "BAS: normal-nefes"),
                new TestPhase(20, "10sn derin nefes alin (yavas ve derin)", "BAS: derin-nefes"),
            }),
        new("9", "Dusme Simulasyonu", 20,
            "Ani dusme hareketinin PTP ve VAR uzerindeki etkisi",
            new[]
            {
                new TestPhase(0,  "5sn hareketsiz ayakta durun", "BAS: ayakta"),
                new TestPhase(5,  "5sn yere dogru egilin (yavas)", "BAS: egilme"),
                new TestPhase(10, "5sn yerde hareketsiz durun", "BAS: yerde"),
                new TestPhase(15, "5sn hizlica kalkin", "BAS: kalkma"),
            }),
        new("0", "Gesture Kutuphanesi", 30,
            "5 farkli el hareketinin siniflandirma basarimi",
            new[]
            {
                new TestPhase(0,  "5sn elinizi yukari kaldirin", "BAS: gesture-yukari"),
                new TestPhase(6,  "5sn elinizi asagiya indirin", "BAS: gesture-asagi"),
                new TestPhase(12, "5sn elinizi sola dogru itin", "BAS: gesture-sola"),
                new TestPhase(18, "5sn elinizi saga dogru itin", "BAS: gesture-saga"),
                new TestPhase(24, "5sn elinizle daire cizin", "BAS: gesture-daire"),
            }),
        new("e", "El Sallama", 15,
            "Klasik el sallama testi 4 asamada",
            new[]
            {
                new TestPhase(0,  "Bekleyin (taban olcumu)", "BAS: taban"),
                new TestPhase(3,  "Saga dogru el sallayin", "BAS: saga-el"),
                new TestPhase(6,  "Sola dogru el sallayin", "BAS: sola-el"),
                new TestPhase(9,  "Hizli sallayin", "BAS: hizli"),
                new TestPhase(12, "Yavas sallayin", "BAS: yavas"),
            }),
        new("y", "Yon Testi", 18,
            "Saga-sola yon degisimi ile hareket algilama",
            new[]
            {
                new TestPhase(0,  "Bekleyin (taban)", "BAS: taban"),
                new TestPhase(3,  "Sola dogru yuruyun", "BAS: sola-git"),
                new TestPhase(6,  "Sagda durun", "BAS: sagda-bekle"),
                new TestPhase(9,  "Saga dogru yuruyun", "BAS: saga-git"),
                new TestPhase(12, "Solda durun", "BAS: solda-bekle"),
                new TestPhase(15, "Sola-saga hizli gidin", "BAS: saga-sola"),
            }),
    };
}

/// <summary>
/// Tek bir test kaydinin tum verileri. Python <c>TestRecording</c> dataclass'inin portu.
/// </summary>
public sealed class TestRecording
{
    public string Name { get; set; } = "";
    public double Duration { get; set; }
    public double StartTime { get; set; }

    public List<double> Timestamps { get; } = new();
    public List<int> RssiDbm { get; } = new();
    public List<int> RssiPct { get; } = new();
    public List<double> Var { get; } = new();
    public List<double> Delta { get; } = new();
    public List<double> Ptp { get; } = new();
    public List<double> RxRate { get; } = new();
    public List<double> TxRate { get; } = new();
    public Dictionary<string, List<double>> ApHistory { get; } = new();
    public List<(double Time, string Text)> Annotations { get; } = new();
    public IReadOnlyList<TestPhase> Phases { get; set; } = Array.Empty<TestPhase>();

    /// <summary>Wall-clock Unix saniyesi (Python <c>time.time()</c> karsiligi).</summary>
    internal static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    public double Elapsed
    {
        get
        {
            if (StartTime == 0 || Timestamps.Count == 0)
                return 0.0;
            return Timestamps[^1] - StartTime;
        }
    }

    public bool IsComplete
    {
        get
        {
            if (StartTime == 0)
                return true;
            return (Now() - StartTime) >= Duration;
        }
    }

    public string CurrentInstruction
    {
        get
        {
            if (Phases.Count == 0 || StartTime == 0)
                return "";
            double elapsed = Now() - StartTime;
            string instr = "";
            foreach (var p in Phases)
                if (elapsed >= p.AtSec)
                    instr = p.Instruction;
            return instr;
        }
    }

    public string CurrentAnnotation
    {
        get
        {
            if (Phases.Count == 0 || StartTime == 0)
                return "";
            double elapsed = Now() - StartTime;
            string ann = "";
            foreach (var p in Phases)
                if (elapsed >= p.AtSec)
                    ann = p.Annotation;
            return ann;
        }
    }

    public void AddSample(int dbm, int pct, double varVal, double deltaVal, double ptpVal, double rx, double tx)
    {
        Timestamps.Add(Now());
        RssiDbm.Add(dbm);
        RssiPct.Add(pct);
        Var.Add(varVal);
        Delta.Add(deltaVal);
        Ptp.Add(ptpVal);
        RxRate.Add(rx);
        TxRate.Add(tx);
    }

    public void Annotate(string text)
    {
        Annotations.Add((Now(), text.Length > 40 ? text[..40] : text));
    }

    public void AnnotateIfDue()
    {
        if (Phases.Count == 0 || StartTime == 0)
            return;
        foreach (var p in Phases)
        {
            double ts = StartTime + p.AtSec;
            if (Math.Abs(Now() - ts) < 0.3)
            {
                bool already = Annotations.Any(a => Math.Abs(a.Time - ts) < 1.0);
                if (!already && !string.IsNullOrEmpty(p.Annotation))
                    Annotate(p.Annotation);
            }
        }
    }

    /// <summary>Kaydi CSV dosyasina yazar. Python <c>to_csv</c> karsiligi.</summary>
    public string ToCsv(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        double t0 = StartTime != 0 ? StartTime : (Timestamps.Count > 0 ? Timestamps[0] : Now());

        var sb = new StringBuilder();
        WriteRow(sb, "test_name", Name);
        WriteRow(sb, "duration_s", Duration.ToString(CultureInfo.InvariantCulture));
        WriteRow(sb, "samples", Timestamps.Count.ToString(CultureInfo.InvariantCulture));
        sb.Append('\n');
        WriteRow(sb, "time_abs", "time_elapsed_s", "rssi_dbm", "rssi_pct",
            "var", "delta", "ptp", "rx_mbps", "tx_mbps");

        for (int i = 0; i < Timestamps.Count; i++)
        {
            WriteRow(sb,
                IsoFromUnix(Timestamps[i]),
                Round3(Timestamps[i] - t0),
                RssiDbm[i].ToString(CultureInfo.InvariantCulture),
                RssiPct[i].ToString(CultureInfo.InvariantCulture),
                Exp6(Var[i]),
                Exp6(Delta[i]),
                Exp6(Ptp[i]),
                RxRate[i].ToString(CultureInfo.InvariantCulture),
                TxRate[i].ToString(CultureInfo.InvariantCulture));
        }

        if (Annotations.Count > 0)
        {
            sb.Append('\n');
            WriteRow(sb, "annotations");
            WriteRow(sb, "time_abs", "time_elapsed_s", "text");
            foreach (var (ats, text) in Annotations)
                WriteRow(sb, IsoFromUnix(ats), Round3(ats - t0), text);
        }

        if (ApHistory.Count > 0)
        {
            sb.Append('\n');
            WriteRow(sb, "ap_history", "bssid -> rssi_list");
            foreach (var kvp in ApHistory)
            {
                var cells = new List<string> { kvp.Key };
                cells.AddRange(kvp.Value.Select(v => v.ToString(CultureInfo.InvariantCulture)));
                WriteRow(sb, cells.ToArray());
            }
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }

    private static string IsoFromUnix(double unixSeconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)(unixSeconds * 1000)).LocalDateTime
            .ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);

    private static string Round3(double v) =>
        Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);

    private static string Exp6(double v) =>
        v.ToString("0.000000e+00", CultureInfo.InvariantCulture);

    private static void WriteRow(StringBuilder sb, params string[] cells)
    {
        sb.Append(string.Join(",", cells.Select(EscapeCsv)));
        sb.Append('\n');
    }

    private static string EscapeCsv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
