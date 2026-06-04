using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WifiMotion.Core;

/// <summary>Hareket basladiginda olusturulan bilgi (alarm/log icin).</summary>
public sealed record MotionInfo(string Magnitude, string Causes, int Dbm);

/// <summary>
/// Hareket algilama hatti. Python <c>wifi_motion.py</c> icindeki algilama
/// fonksiyonlarinin (calibrate, process_detection, _read_signal, _check_motion,
/// _handle_motion_event, _update_baseline, _update_fft, scan_aps) portu.
///
/// UI'dan bagimsizdir: durumu <see cref="State"/> uzerinden sunar ve onemli
/// gecisleri olaylarla (event) bildirir. Tek bir arka plan gorevi (Task) bu
/// motoru surekli calistirir.
/// </summary>
public sealed class MotionEngine
{
    public Settings Settings { get; }
    public DetectionState State { get; }
    public WlanApi Wlan { get; }

    /// <summary>Hareket basladiginda (yukselen kenar) tetiklenir.</summary>
    public event Action<MotionInfo>? MotionStarted;

    /// <summary>Hareket bittiginde (dusen kenar) tetiklenir.</summary>
    public event Action? MotionEnded;

    /// <summary>Gecici durum mesaji (orn. "HAREKET! Orta (VAR:12.3)").</summary>
    public event Action<string>? StatusChanged;

    private readonly string _motionLog;
    private readonly string _rssiLog;
    private const int LogMaxLines = 5000;

    private static readonly long ClockStart = Stopwatch.GetTimestamp();

    public MotionEngine(Settings settings, DetectionState state, WlanApi wlan)
    {
        Settings = settings;
        State = state;
        Wlan = wlan;
        _motionLog = Path.Combine(AppContext.BaseDirectory, "motion_log.txt");
        _rssiLog = Path.Combine(AppContext.BaseDirectory, "rssi_log.txt");
    }

    /// <summary>Programin baslangicindan beri gecen sure (saniye, monotonik).</summary>
    public static double NowSeconds() =>
        (Stopwatch.GetTimestamp() - ClockStart) / (double)Stopwatch.Frequency;

    // =================================================================
    // AP taramasi
    // =================================================================

    /// <summary>
    /// Yakindaki erisim noktalarini tarar ve <see cref="DetectionState.ApSlots"/>'u gunceller.
    /// Python <c>scan_aps</c> karsiligi.
    /// </summary>
    public void ScanAps(bool trigger = false)
    {
        List<BssEntry> fresh = Wlan.Scan(trigger);
        if (fresh.Count == 0)
            return;

        var enabledMap = State.ApSlots.ToDictionary(s => s.Bssid, s => s.Enabled);
        var historyMap = State.ApSlots.ToDictionary(s => s.Bssid, s => s.History);

        var newSlots = new List<ApSlot>();
        foreach (var ap in fresh.Take(6))
        {
            string b = ap.Bssid;
            var hist = historyMap.TryGetValue(b, out var h) ? new List<double>(h) : new List<double>();
            int newRssi = ap.Rssi;

            string trend;
            if (hist.Count >= 2)
            {
                double d = newRssi - hist[^1];
                trend = d > 2 ? "▲" : (d < -2 ? "▼" : "▌");
            }
            else
            {
                trend = "▌";
            }

            hist.Add(newRssi);
            if (hist.Count > Settings.Window)
                hist.RemoveAt(0);

            newSlots.Add(new ApSlot
            {
                Ssid = ap.Ssid,
                Bssid = b,
                Rssi = newRssi,
                Quality = ap.Quality,
                Enabled = enabledMap.TryGetValue(b, out bool en) ? en : true,
                History = hist,
                Trend = trend,
                ChannelFreq = ap.ChannelFreq,
                Channel = ap.Channel,
            });
        }
        State.ApSlots = newSlots;
    }

    // =================================================================
    // Kalibrasyon
    // =================================================================

    /// <summary>
    /// Taban varyans/delta/ptp esiklerini belirlemek icin kalibrasyon yapar.
    /// Python <c>calibrate</c> karsiligi (async; her ornek arasinda 1 sn bekler).
    /// </summary>
    public async Task CalibrateAsync(IProgress<string>? progress, CancellationToken ct)
    {
        var (mult, minV, minD, minP) = Stats.SensitivityParams(Settings.Sensitivity);
        progress?.Report($"Kalibrasyon — {Settings.CalibSamples} saniye hareketsiz bekleyin...");

        var collected = new List<double>();
        var deltas = new List<double>();
        var ptps = new List<double>();
        var rxSamples = new List<double>();
        State.PrevDbm = null;

        for (int i = 0; i < Settings.CalibSamples; i++)
        {
            ct.ThrowIfCancellationRequested();
            RssiReading r = await Task.Run(RssiReader.GetRssi, ct).ConfigureAwait(false);

            if (r.Dbm is int dbm)
            {
                if (State.PrevDbm is int prev)
                    deltas.Add(Math.Abs(dbm - prev));
                State.PrevDbm = dbm;
                collected.Add(dbm);
                if (collected.Count >= Settings.Window)
                    ptps.Add(Stats.PeakToPeak(LastN(collected, Settings.Window)));
            }
            if (r.Rx is double rx)
            {
                rxSamples.Add(rx);
                State.LastRxRate = rx;
            }
            if (r.Tx is double tx)
                State.LastTxRate = tx;

            progress?.Report(
                $"Kalibrasyon {i + 1}/{Settings.CalibSamples}  RSSI: {Fmt(r.Dbm)} dBm  RX:{Fmt(r.Rx)} Mbps");
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        // Kayan pencere varyans ornekleri
        var varSamples = new List<double>();
        for (int i = 0; i <= collected.Count - Settings.Window; i++)
            varSamples.Add(Stats.Variance(collected.GetRange(i, Settings.Window), linearPower: true));

        double baselineVar = varSamples.Count > 0 ? varSamples.Average() : 0;
        double baselineDelta = deltas.Count > 0 ? deltas.Average() : 0;
        double baselinePtp = ptps.Count > 0 ? ptps.Average() : 0;

        Settings.ThresholdVar = Math.Max(minV, baselineVar * mult);
        Settings.ThresholdDelta = Math.Max(minD, baselineDelta * mult);
        Settings.ThresholdPtp = Math.Max(minP, baselinePtp * mult);

        if (collected.Count > 0)
        {
            State.EwmaDbm = collected.Average();
            State.BaselineDbm = (int)Math.Round(State.EwmaDbm.Value, MidpointRounding.ToEven);
        }

        State.RxHist.Clear();
        State.RxHist.AddRange(rxSamples.Count >= Settings.Window ? LastN(rxSamples, Settings.Window) : rxSamples);
        State.History.Clear();
        State.History.AddRange(collected.Count >= Settings.Window ? LastN(collected, Settings.Window) : collected);

        progress?.Report(
            $"Kalibrasyon tamam!  VAR:{Settings.ThresholdVar:F1}  " +
            $"DEL:{Settings.ThresholdDelta:F1}  PTP:{Settings.ThresholdPtp:F1}  " +
            $"Taban:{State.BaselineDbm}dBm");
    }

    // =================================================================
    // Algilama dongusu (tek cevrim)
    // =================================================================

    /// <summary>
    /// Ana algilama cevrimi. Python <c>process_detection</c> karsiligi.
    /// </summary>
    public void ProcessDetection()
    {
        if (!ReadSignal())
            return;

        var (rawHit, slotHit) = CheckMotion();
        if (rawHit || slotHit)
            State.MotionConsec++;
        else
            State.MotionConsec = 0;

        bool confirmed = State.MotionConsec >= Settings.MotionConfirm;
        State.MotionNow = confirmed;
        bool risingEdge = confirmed && !State.PrevMotionState;

        HandleMotionEvent(risingEdge, rawHit, slotHit);
        UpdateBaseline();
        UpdateFft();

        int enabledCount = State.ApSlots.Count(s => s.Enabled);
        State.SpatialHint = enabledCount >= 2 ? CalcSpatialHint() : "";

        double now = NowSeconds();
        if (now - State.LastSnapTime >= Settings.SnapInterval)
        {
            State.LastSnapTime = now;
            SaveSnapshot();
        }
        TrimLog(_motionLog);
    }

    /// <summary>Sinyali okur ve durum tamponlarini gunceller. Python <c>_read_signal</c>.</summary>
    private bool ReadSignal()
    {
        RssiReading r = RssiReader.GetRssi();

        State.ApScanTick++;
        if (State.ApScanTick >= Settings.ApScanInterval)
        {
            ScanAps();
            State.ApScanTick = 0;
        }

        if (r.Dbm is not int dbm)
            return false;

        State.LastRssi = r.Signal ?? 0;
        State.LastDelta = State.PrevDbm is int prev ? Math.Abs(dbm - prev) : 0.0;
        State.PrevDbm = dbm;
        State.LastDbm = dbm;

        State.Timestamps.Add(NowSeconds());
        if (State.Timestamps.Count > Settings.Window * 2)
            State.Timestamps.RemoveAt(0);

        if (r.Rx is double rx)
        {
            State.LastRxRate = rx;
            State.RxHist.Add(rx);
            if (State.RxHist.Count > Settings.Window)
                State.RxHist.RemoveAt(0);
        }
        if (r.Tx is double tx)
            State.LastTxRate = tx;

        State.History.Add(dbm);
        if (State.History.Count > Settings.Window)
            State.History.RemoveAt(0);

        State.GraphHist.Add(dbm);
        if (State.GraphHist.Count > Settings.GraphW)
            State.GraphHist.RemoveAt(0);

        State.LastVar = Stats.Variance(State.History, linearPower: true);
        State.LastPtp = Stats.PeakToPeak(State.History);
        return true;
    }

    /// <summary>Ham ve AP-yuvasi esik asimlarini degerlendirir. Python <c>_check_motion</c>.</summary>
    private (bool RawHit, bool SlotHit) CheckMotion()
    {
        double rxVar = State.RxHist.Count >= 3 ? Stats.Variance(State.RxHist, linearPower: true) : 0.0;
        bool rxHit = rxVar > Settings.RxVarThreshold;
        bool rawHit =
            State.LastVar > Settings.ThresholdVar
            || State.LastDelta > Settings.ThresholdDelta
            || State.LastPtp > Settings.ThresholdPtp
            || rxHit;

        bool slotHit = false;
        foreach (var s in State.ApSlots)
        {
            if (!s.Enabled || s.History.Count < 3)
                continue;
            var h = s.History;
            double sv = Stats.Variance(h, linearPower: true);
            double sd = Math.Abs(h[^1] - h[^2]);
            double sp = Stats.PeakToPeak(h);
            if (sv > Settings.ThresholdVar || sd > Settings.ThresholdDelta || sp > Settings.ThresholdPtp)
            {
                slotHit = true;
                break;
            }
        }
        return (rawHit, slotHit);
    }

    /// <summary>Hareket gecislerini isler. Python <c>_handle_motion_event</c>.</summary>
    private void HandleMotionEvent(bool risingEdge, bool rawHit, bool slotHit)
    {
        if (risingEdge)
        {
            string magLabel = Stats.MotionMagnitude(State.PeakVar);
            string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var causes = new List<string>();
            if (State.LastVar > Settings.ThresholdVar) causes.Add($"VAR={State.LastVar:F1}");
            if (State.LastDelta > Settings.ThresholdDelta) causes.Add($"DEL={State.LastDelta:F1}");
            if (State.LastPtp > Settings.ThresholdPtp) causes.Add($"PTP={State.LastPtp:F1}");
            if (slotHit && !rawHit) causes.Add("SLOT");

            string apSummary = "  AP:";
            foreach (var s in State.ApSlots.Take(6))
            {
                if (s.History.Count >= 2)
                {
                    double v = Stats.Variance(s.History, linearPower: true);
                    if (v > 1.0)
                        apSummary += $" {Trunc(s.Ssid, 10)}(V:{v:F0})";
                }
            }

            string causesStr = string.Join(", ", causes);
            AppendMotionLog(
                $"[{ts}] HAREKET BASLADI  RSSI={State.LastDbm}dBm ({causesStr})  Boyut={magLabel}{apSummary}\n");
            MotionStarted?.Invoke(new MotionInfo(magLabel, causesStr, State.LastDbm));
        }
        else if (State.PrevMotionState && !State.MotionNow)
        {
            string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string apRssis = "  AP:";
            foreach (var s in State.ApSlots.Take(6))
            {
                if (s.History.Count >= 1)
                    apRssis += $" {Trunc(s.Ssid, 10)}({s.Rssi}dBm)";
            }
            AppendMotionLog($"[{ts}] HAREKET BITTI  Cooldown:{State.MotionCooldown}{apRssis}\n");
            MotionEnded?.Invoke();
        }

        State.PrevMotionState = State.MotionNow;

        if (State.MotionNow)
        {
            State.SustainedMotionFrames++;
            if (State.LastVar > State.PeakVar)
                State.PeakVar = State.LastVar;

            if (State.MotionCooldown <= 0)
            {
                var why = new List<string>();
                if (State.LastVar > Settings.ThresholdVar) why.Add($"VAR:{State.LastVar:F1}");
                if (State.LastDelta > Settings.ThresholdDelta) why.Add($"DEL:{State.LastDelta:F1}");
                if (State.LastPtp > Settings.ThresholdPtp) why.Add($"PTP:{State.LastPtp:F1}");
                if (slotHit && !rawHit) why.Add("SLOT");
                State.MotionCooldown = Settings.Cooldown;
                StatusChanged?.Invoke($"HAREKET! {Stats.MotionMagnitude(State.PeakVar)} ({string.Join(",", why)})");
            }
            else
            {
                State.MotionCooldown = Settings.Cooldown;
            }
        }
        else
        {
            State.SustainedMotionFrames = 0;
            if (State.MotionCooldown > 0)
                State.MotionCooldown--;
            if (State.MotionCooldown == 0)
                State.PeakVar = 0.0;
        }
    }

    /// <summary>EWMA taban guncellemesi ve konum-oturma mantigi. Python <c>_update_baseline</c>.</summary>
    private void UpdateBaseline()
    {
        State.EwmaDbm ??= State.LastDbm;
        State.BaselineDbm = (int)Math.Round(State.EwmaDbm.Value, MidpointRounding.ToEven);
        State.EwmaDbm = Settings.EwmaAlpha * State.LastDbm + (1 - Settings.EwmaAlpha) * State.EwmaDbm.Value;

        if (State.SustainedMotionFrames >= Settings.PositionSettleFrames)
        {
            double histMean = State.History.Count > 0 ? State.History.Average() : State.LastDbm;
            State.EwmaDbm = histMean;
            State.BaselineDbm = (int)Math.Round(histMean, MidpointRounding.ToEven);
            State.MotionConsec = 0;
            State.SustainedMotionFrames = 0;
            State.MotionCooldown = 0;
            State.PeakVar = 0.0;
            State.MotionNow = false;
        }
    }

    /// <summary>Grafik gecmisi uzerinde FFT frekans analizi. Python <c>_update_fft</c>.</summary>
    private void UpdateFft()
    {
        State.FftTick++;
        if (State.FftTick >= Settings.FftInterval && State.GraphHist.Count >= 16)
        {
            State.FftTick = 0;
            var samples = State.GraphHist.Count >= 32 ? LastN(State.GraphHist, 32) : new List<double>(State.GraphHist);
            double actualHz = Stats.EstimateSamplingRate(State.Timestamps);
            (State.FftFreq, _) = Stats.DominantFreq(samples, actualHz);
            State.FftLabel = Stats.FreqLabel(State.FftFreq);
        }
    }

    /// <summary>Aktif AP'ler arasi varyansa gore konumsal yon ipucu. Python <c>_calc_spatial_hint</c>.</summary>
    private string CalcSpatialHint()
    {
        var active = State.ApSlots
            .Where(s => s.Enabled && s.History.Count >= 3)
            .Select(s => (s.Ssid, Var: Stats.Variance(s.History, linearPower: true)))
            .ToList();
        if (active.Count < 2)
            return "";
        active.Sort((a, b) => b.Var.CompareTo(a.Var));
        var top = active[0];
        var second = active[1];
        double ratio = top.Var / (second.Var + 0.001);
        if (ratio < 1.5)
            return "Orta (her iki AP)";
        return $"{Trunc(top.Ssid, 12)} tarafi";
    }

    /// <summary>Secili esik metrigini <paramref name="step"/> kadar ayarlar. Python <c>_adjust_metric</c>.</summary>
    public void AdjustMetric(double step)
    {
        switch (State.EditMetric)
        {
            case "var":
                Settings.ThresholdVar = Math.Round(Clamp(Settings.ThresholdVar + step, 0.1, 500.0), 2, MidpointRounding.ToEven);
                break;
            case "del":
                Settings.ThresholdDelta = Math.Round(Clamp(Settings.ThresholdDelta + step, 0.1, 100.0), 2, MidpointRounding.ToEven);
                break;
            default:
                Settings.ThresholdPtp = Math.Round(Clamp(Settings.ThresholdPtp + step, 0.1, 200.0), 2, MidpointRounding.ToEven);
                break;
        }
    }

    /// <summary>Hassasiyeti <paramref name="step"/> kadar degistirir (0.1-15.0).</summary>
    public void AdjustSensitivity(double step)
    {
        Settings.Sensitivity = Math.Round(Clamp(Settings.Sensitivity + step, 0.1, 15.0), 1, MidpointRounding.ToEven);
    }

    // =================================================================
    // Loglama
    // =================================================================

    private void AppendMotionLog(string line)
    {
        try { File.AppendAllText(_motionLog, line); }
        catch (IOException) { /* yoksay */ }
        catch (UnauthorizedAccessException) { /* yoksay */ }
    }

    private void SaveSnapshot()
    {
        string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string durum = State.MotionNow ? "HAREKET" : "sakin";
        string hdr =
            $"=== {ts}  RSSI:{State.LastDbm}dBm(taban:{State.BaselineDbm})  " +
            $"RX:{State.LastRxRate:F0}Mbps  " +
            $"VAR:{State.LastVar:F2}/{Settings.ThresholdVar:F1}  " +
            $"DEL:{State.LastDelta:F2}/{Settings.ThresholdDelta:F1}  " +
            $"PTP:{State.LastPtp:F2}/{Settings.ThresholdPtp:F1}  " +
            $"freq:{State.FftFreq:F2}Hz {State.FftLabel}  " +
            $"[{durum}] ===";
        try
        {
            File.AppendAllText(_rssiLog, hdr + "\n");
            TrimLog(_rssiLog);
        }
        catch (IOException) { /* yoksay */ }
        catch (UnauthorizedAccessException) { /* yoksay */ }
    }

    private static void TrimLog(string path, int maxLines = LogMaxLines)
    {
        try
        {
            if (!File.Exists(path))
                return;
            var lines = File.ReadAllLines(path);
            if (lines.Length > maxLines)
                File.WriteAllLines(path, lines.Skip(lines.Length - maxLines));
        }
        catch (IOException) { /* yoksay */ }
        catch (UnauthorizedAccessException) { /* yoksay */ }
    }

    // =================================================================
    // Yardimcilar
    // =================================================================

    private static List<double> LastN(List<double> list, int n) =>
        list.Count <= n ? new List<double>(list) : list.GetRange(list.Count - n, n);

    private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];

    private static string Fmt(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "None";

    private static string Fmt(double? v) =>
        v?.ToString(CultureInfo.InvariantCulture) ?? "None";
}
