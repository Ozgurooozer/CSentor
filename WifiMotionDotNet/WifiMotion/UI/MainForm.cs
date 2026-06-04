using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WifiMotion.Core;
using WifiMotion.Testing;

namespace WifiMotion.UI;

/// <summary>
/// Ana uygulama penceresi. Python <c>wifi_motion.py</c> icindeki terminal arayuzu
/// ve ana dongunun (main key-loop, show_status) WinForms karsiligi.
/// </summary>
public partial class MainForm : Form
{
    private readonly Settings _settings;
    private readonly DetectionState _state;
    private readonly MotionEngine _engine;
    private readonly TestRecorder _recorder = new();

    private CancellationTokenSource? _cts;
    private Thread? _loopThread;
    private volatile bool _calibrating;
    private bool _syncing;
    private bool _updatingAps;
    private int _idleScanTick;

    public MainForm()
    {
        _settings = Settings.FromFile();
        _state = new DetectionState();
        _engine = new MotionEngine(_settings, _state, new WlanApi());

        InitializeComponent();
        WireEvents();
        SyncSettingsToControls();
        UpdateSoundButton();
        RefreshButtons();
    }

    // =================================================================
    // Kurulum
    // =================================================================

    private void WireEvents()
    {
        tsBtnStart.Click += async (_, _) => await StartDetectionAsync();
        tsBtnStop.Click += (_, _) => StopDetection();
        tsBtnCalibrate.Click += async (_, _) => await CalibrateOnlyAsync();
        tsBtnSound.Click += (_, _) => ToggleSound();
        tsBtnInfo.Click += (_, _) => ShowInfo();
        tsBtnHelp.Click += (_, _) => ShowHelp();

        btnHandwave.Click += (_, _) => StartNamedTest("e");
        btnDirection.Click += (_, _) => StartNamedTest("y");
        btnCustom.Click += (_, _) => StartCustomTest();
        btnAllTests.Click += (_, _) => StartFromMenu();
        btnStopTest.Click += (_, _) => StopTestEarly();

        numVar.ValueChanged += (_, _) => { if (!_syncing) _settings.ThresholdVar = (double)numVar.Value; };
        numDel.ValueChanged += (_, _) => { if (!_syncing) _settings.ThresholdDelta = (double)numDel.Value; };
        numPtp.ValueChanged += (_, _) => { if (!_syncing) _settings.ThresholdPtp = (double)numPtp.Value; };

        rbVar.CheckedChanged += (_, _) => { if (rbVar.Checked) _state.EditMetric = "var"; };
        rbDel.CheckedChanged += (_, _) => { if (rbDel.Checked) _state.EditMetric = "del"; };
        rbPtp.CheckedChanged += (_, _) => { if (rbPtp.Checked) _state.EditMetric = "ptp"; };

        trkSensitivity.ValueChanged += (_, _) =>
        {
            if (_syncing) return;
            _settings.Sensitivity = Math.Round(trkSensitivity.Value / 10.0, 1);
            lblSensVal.Text = _settings.Sensitivity.ToString("F1", CultureInfo.InvariantCulture);
        };

        lstAps.ItemChecked += OnApItemChecked;

        _engine.MotionStarted += OnMotionStarted;
        _engine.MotionEnded += OnMotionEnded;
        _engine.StatusChanged += msg => SafeInvoke(() => SetStatus(msg));

        Load += OnLoadForm;
        FormClosing += OnClosingForm;
        KeyDown += OnKeyDownForm;
    }

    private void OnLoadForm(object? sender, EventArgs e)
    {
        _cts = new CancellationTokenSource();

        // WLAN API'yi baslat ve ilk taramayi arka planda yap (trigger 2sn surer)
        Task.Run(() =>
        {
            try
            {
                _engine.Wlan.Init();
                _engine.ScanAps(trigger: true);
            }
            catch { /* yoksay */ }
            SafeInvoke(RefreshAps);
        });

        _loopThread = new Thread(() => LoopBody(_cts.Token))
        {
            IsBackground = true,
            Name = "DetectionLoop",
        };
        _loopThread.Start();

        AppendLog("Hosgeldiniz! [Baslat] ile kalibrasyon + algilamayi baslatin.");
    }

    // =================================================================
    // Arka plan algilama dongusu (Python ana while dongusu karsiligi)
    // =================================================================

    private void LoopBody(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_state.Running && !_calibrating)
            {
                try { _engine.ProcessDetection(); }
                catch { /* tek cevrim hatasini yut */ }

                if (_recorder.Active)
                {
                    _recorder.Record(
                        _state.LastDbm, _state.LastRssi,
                        _state.LastVar, _state.LastDelta, _state.LastPtp,
                        _state.LastRxRate, _state.LastTxRate,
                        _state.ApSlots);
                }

                SafeInvoke(RefreshUi);

                if (_recorder.Active && _recorder.Recording is { IsComplete: true })
                    SafeInvoke(HandleTestComplete);

                Sleep(300, ct);
            }
            else
            {
                // Bos durumda AP listesini hafifce tazele
                _idleScanTick++;
                if (!_calibrating && _idleScanTick >= 20)
                {
                    _idleScanTick = 0;
                    try { _engine.ScanAps(false); } catch { /* yoksay */ }
                    SafeInvoke(RefreshAps);
                }
                Sleep(150, ct);
            }
        }
    }

    private static void Sleep(int ms, CancellationToken ct)
    {
        try { ct.WaitHandle.WaitOne(ms); } catch { /* yoksay */ }
    }

    // =================================================================
    // Baslat / Durdur / Kalibrasyon
    // =================================================================

    private async Task StartDetectionAsync()
    {
        if (_state.Running) { SetStatus("Zaten calisiyor!"); return; }
        if (_calibrating) return;

        _calibrating = true;
        RefreshButtons();
        _state.PrevDbm = null;

        var progress = new Progress<string>(s => { SetStatus(s); });
        try
        {
            await _engine.CalibrateAsync(progress, _cts!.Token);
        }
        catch (OperationCanceledException)
        {
            _calibrating = false;
            RefreshButtons();
            return;
        }
        catch (Exception ex)
        {
            AppendLog("Kalibrasyon hatasi: " + ex.Message);
        }

        _state.History.Clear();
        SyncSettingsToControls();
        _state.Running = true;
        _calibrating = false;
        RefreshButtons();
        RefreshUi();
        SetStatus("Algilama basladi! [Durdur] ile durdurabilirsiniz.");
        AppendLog("Algilama basladi.");
    }

    private async Task CalibrateOnlyAsync()
    {
        if (_state.Running) { SetStatus("Once [Durdur] ile durdurun."); return; }
        if (_calibrating) return;

        _calibrating = true;
        RefreshButtons();
        _state.PrevDbm = null;

        var progress = new Progress<string>(s => SetStatus(s));
        try
        {
            await _engine.CalibrateAsync(progress, _cts!.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppendLog("Kalibrasyon hatasi: " + ex.Message); }

        SyncSettingsToControls();
        _calibrating = false;
        RefreshButtons();
        SetStatus("Kalibrasyon tamamlandi.");
        AppendLog("Kalibrasyon tamamlandi.");
    }

    private void StopDetection()
    {
        if (!_state.Running) { SetStatus("Zaten durmus."); return; }
        _state.Running = false;
        RefreshButtons();
        SetStatus("Algilama durduruldu.");
        AppendLog("Algilama durduruldu.");
    }

    // =================================================================
    // Testler
    // =================================================================

    private void StartNamedTest(string key)
    {
        var td = Tests.All.FirstOrDefault(t => t.Key == key);
        if (td is not null)
            StartTest(td);
    }

    private void StartCustomTest()
    {
        if (!CanStartTest()) return;
        _recorder.Start("Custom Test", 30);
        SetStatus("Custom test! 30sn — [Not Ekle] ile isaretleyin.");
        AppendLog("Custom test basladi (30sn).");
        RefreshButtons();
    }

    private void StartFromMenu()
    {
        if (!CanStartTest()) return;
        using var dlg = new TestMenuForm();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Selected is not null)
            StartTest(dlg.Selected);
    }

    private void StartTest(TestDefinition td)
    {
        if (!CanStartTest()) return;
        _recorder.Start(td.Name, td.Duration, td.Phases);
        SetStatus($"{td.Name} basladi! {td.Duration}sn");
        AppendLog($"Test basladi: {td.Name} ({td.Duration}sn)");
        RefreshButtons();
    }

    private bool CanStartTest()
    {
        if (!_state.Running) { SetStatus("Once [Baslat] ile baslatin."); return false; }
        if (_recorder.Active) { SetStatus("Zaten bir test calisiyor!"); return false; }
        return true;
    }

    private void StopTestEarly()
    {
        if (_recorder.Active)
            HandleTestComplete();
    }

    private void HandleTestComplete()
    {
        var rec = _recorder.Stop();
        RefreshButtons();
        if (rec is null || rec.Timestamps.Count == 0)
        {
            SetStatus("Test kaydedilecek veri yok.");
            return;
        }

        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string csvPath = Path.Combine(TestPaths.EnsureTestDir(), $"{rec.Name}_{ts}.csv");
        try { rec.ToCsv(csvPath); }
        catch (Exception ex) { AppendLog("CSV yazilamadi: " + ex.Message); }

        string? heatPath = HeatmapGenerator.Generate(rec);

        string msg = $"Test bitti! CSV: {csvPath}";
        if (heatPath is not null)
        {
            msg += $"  |  Heatmap: {heatPath}";
            try { Process.Start(new ProcessStartInfo(heatPath) { UseShellExecute = true }); }
            catch { /* yoksay */ }
        }
        SetStatus(msg);
        AppendLog(msg);
    }

    private void Annotate()
    {
        if (_recorder.Active)
        {
            _recorder.Annotate("ANOT");
            SetStatus("Not eklendi!");
        }
    }

    // =================================================================
    // Ses / Bilgi / Yardim
    // =================================================================

    private void ToggleSound()
    {
        _settings.SoundAlarm = !_settings.SoundAlarm;
        UpdateSoundButton();
        SetStatus(_settings.SoundAlarm ? "Sesli alarm ACIK" : "Sesli alarm KAPALI");
    }

    private void UpdateSoundButton() =>
        tsBtnSound.Text = _settings.SoundAlarm ? "Ses: ACIK (Z)" : "Ses: KAPALI (Z)";

    private void ShowInfo()
    {
        var (mult, _, _, _) = Stats.SensitivityParams(_settings.Sensitivity);
        var sb = new StringBuilder();
        sb.AppendLine($"Calisiyor   : {_state.Running}");
        sb.AppendLine($"Hassasiyet  : {_settings.Sensitivity:F1}/15.0  carpan={mult:F2}");
        sb.AppendLine($"Thresholds  : VAR={_settings.ThresholdVar:F1}  DEL={_settings.ThresholdDelta:F1}  PTP={_settings.ThresholdPtp:F1}");
        sb.AppendLine($"Son RSSI    : {_state.LastDbm} dBm   VAR/DEL/PTP: {_state.LastVar:F2}/{_state.LastDelta:F2}/{_state.LastPtp:F2}");
        sb.AppendLine($"WLAN API    : {(_engine.Wlan.IsAvailable() ? "Aktif" : "Devre disi")}");
        sb.AppendLine($"Gorunen AP  : {_state.ApSlots.Count}");
        sb.AppendLine();
        int i = 1;
        foreach (var s in _state.ApSlots.Take(6))
        {
            string st = s.Enabled ? "ACIK  " : "KAPALI";
            sb.AppendLine($"[{i}] {Trunc(s.Ssid, 22),-22}  {s.Rssi,4}dBm  {s.Quality,3}%  {st}  {s.Bssid}");
            i++;
        }
        MessageBox.Show(this, sb.ToString(), "WiFi Motion — Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowHelp()
    {
        const string help =
            "WiFi Motion — Yardim\n\n" +
            "[Baslat] (S)   Kalibrasyon + algilamayi baslatir\n" +
            "[Durdur] (T)   Algilamayi durdurur\n" +
            "[Kalibrasyon] (C)   Yalnizca kalibrasyon\n" +
            "[Ses] (Z)   Sesli alarmi ac/kapat\n\n" +
            "Esikler: VAR/DEL/PTP kutularindan dogrudan duzenleyin.\n" +
            "  Klavye: [V]/[D]/[P] ile sec, ok tuslari ile ±1.0 / ±0.1.\n" +
            "Hassasiyet: kaydirici veya [+]/[-] (0.1–15.0).\n\n" +
            "AP'ler: listede isaretle = ac/kapat, ya da [1]-[6] tuslari.\n" +
            "  Her acik AP kendi gecmisinden VAR/DEL/PTP hesaplar; biri\n" +
            "  esigi asarsa hareket bildirilir. 2+ AP acikken yon ipucu cikar.\n\n" +
            "Testler (algilama acikken):\n" +
            "  [E] El Sallama   [Y] Yon Testi   [K] Custom   [M] Tum Testler\n" +
            "  [Space] test sirasinda not ekle. Test bitince CSV + heatmap uretilir.\n\n" +
            "[I] Bilgi   [H] Yardim";
        MessageBox.Show(this, help, "WiFi Motion — Yardim", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // =================================================================
    // Ekran tazeleme
    // =================================================================

    private void RefreshUi()
    {
        // Hareket durumu
        if (_state.MotionNow)
        {
            lblMotion.Text = "Hareket: EVET!";
            lblMotion.ForeColor = System.Drawing.Color.Firebrick;
        }
        else
        {
            lblMotion.Text = "Hareket: HAYIR";
            lblMotion.ForeColor = System.Drawing.Color.ForestGreen;
        }

        string drift = _state.BaselineDbm != 0
            ? $"Δ{(_state.LastDbm - _state.BaselineDbm):+0;-0;0}"
            : "Δ---";
        string rxStr = _state.LastRxRate > 0 ? $"RX:{_state.LastRxRate:F0}" : "RX:---";
        string txStr = _state.LastTxRate > 0 ? $"TX:{_state.LastTxRate:F0}" : "TX:---";
        lblSignalLine.Text =
            $"Sinyal: {_state.LastRssi}%    RSSI: {_state.LastDbm} dBm ({drift})    {rxStr} {txStr} Mbps";

        int? dbmForQuality = _state.LastDbm == 0 ? null : _state.LastDbm;
        var (qLabel, qBar) = Stats.SignalQuality(dbmForQuality);
        lblQuality.Text = $"{qBar} {qLabel}";

        string mag = _state.MotionNow ? Stats.MotionMagnitude(_state.LastVar) : "-";
        string freq = !string.IsNullOrEmpty(_state.FftLabel) ? $"{_state.FftFreq:F2}Hz {_state.FftLabel}" : "-";
        string spatial = string.IsNullOrEmpty(_state.SpatialHint) ? "-" : _state.SpatialHint;
        lblExtra.Text = $"Boyut: {mag}    Frekans: {freq}    Yon: {spatial}";

        // Metrik degerleri
        SetMetricLabel(lblVarVal, _state.LastVar, _settings.ThresholdVar);
        SetMetricLabel(lblDelVal, _state.LastDelta, _settings.ThresholdDelta);
        SetMetricLabel(lblPtpVal, _state.LastPtp, _settings.ThresholdPtp);

        double rxVar = _state.RxHist.Count >= 3 ? Stats.Variance(_state.RxHist, linearPower: true) : 0.0;
        lblRxv.Text = $"RXv: {rxVar:F1}     |     Secili metrik: {_state.EditMetric.ToUpperInvariant()}";

        // Grafik
        graph.UpdateData(_state.GraphHist, _state.LastDbm, _state.BaselineDbm, _settings.GraphW);

        RefreshAps();
        RefreshTestStatus();
        RefreshButtons();
    }

    private void SetMetricLabel(Label lbl, double value, double threshold)
    {
        lbl.Text = value.ToString("F1", CultureInfo.InvariantCulture);
        lbl.ForeColor = value > threshold ? System.Drawing.Color.Firebrick : System.Drawing.SystemColors.ControlText;
    }

    private void RefreshAps()
    {
        var slots = _state.ApSlots;
        int count = Math.Min(slots.Count, 6);

        bool rebuild = lstAps.Items.Count != count;
        if (!rebuild)
        {
            for (int i = 0; i < count; i++)
            {
                if (lstAps.Items[i].Tag as string != slots[i].Bssid)
                {
                    rebuild = true;
                    break;
                }
            }
        }

        _updatingAps = true;
        try
        {
            if (rebuild)
            {
                lstAps.BeginUpdate();
                lstAps.Items.Clear();
                for (int i = 0; i < count; i++)
                {
                    var s = slots[i];
                    var it = new ListViewItem((i + 1).ToString(CultureInfo.InvariantCulture))
                    {
                        Tag = s.Bssid,
                        Checked = s.Enabled,
                    };
                    it.SubItems.Add(Trunc(s.Ssid, 22));
                    it.SubItems.Add(s.Rssi.ToString(CultureInfo.InvariantCulture));
                    it.SubItems.Add($"{s.Quality}%");
                    it.SubItems.Add(s.Trend);
                    it.SubItems.Add(VarStr(s));
                    lstAps.Items.Add(it);
                }
                lstAps.EndUpdate();
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var s = slots[i];
                    var it = lstAps.Items[i];
                    if (it.Checked != s.Enabled) it.Checked = s.Enabled;
                    it.SubItems[1].Text = Trunc(s.Ssid, 22);
                    it.SubItems[2].Text = s.Rssi.ToString(CultureInfo.InvariantCulture);
                    it.SubItems[3].Text = $"{s.Quality}%";
                    it.SubItems[4].Text = s.Trend;
                    it.SubItems[5].Text = VarStr(s);
                }
            }
        }
        finally
        {
            _updatingAps = false;
        }

        gbAps.Text = _engine.Wlan.IsAvailable()
            ? "Gorunen AP'ler — WLAN API (isaretle = ac/kapat)"
            : "Gorunen AP'ler — netsh (isaretle = ac/kapat)";
    }

    private static string VarStr(ApSlot s) =>
        s.History.Count >= 2 ? Stats.Variance(s.History, linearPower: true).ToString("F1", CultureInfo.InvariantCulture) : "--";

    private void OnApItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_updatingAps) return;
        int idx = e.Item.Index;
        if (idx >= 0 && idx < _state.ApSlots.Count)
        {
            _state.ApSlots[idx].Enabled = e.Item.Checked;
            string st = e.Item.Checked ? "ACIK" : "KAPALI";
            SetStatus($"Slot {idx + 1} ({Trunc(_state.ApSlots[idx].Ssid, 20)}) → {st}");
        }
    }

    private void RefreshTestStatus()
    {
        if (_recorder.Active && _recorder.Recording is { } rec)
        {
            double remain = _recorder.Remaining;
            double pct = rec.Duration > 0 ? Math.Clamp(_recorder.Elapsed / rec.Duration * 100, 0, 100) : 0;
            prgTest.Value = (int)pct;
            string instr = _recorder.CurrentInstruction;
            lblTestStatus.Text = $"{rec.Name}\n{instr}\nKalan: {remain:F0}s  |  Ornek: {rec.Timestamps.Count}";
        }
        else
        {
            prgTest.Value = 0;
            lblTestStatus.Text = "Test yok.";
        }
    }

    private void RefreshButtons()
    {
        tsBtnStart.Enabled = !_state.Running && !_calibrating;
        tsBtnStop.Enabled = _state.Running;
        tsBtnCalibrate.Enabled = !_state.Running && !_calibrating;

        bool canTest = _state.Running && !_recorder.Active;
        btnHandwave.Enabled = canTest;
        btnDirection.Enabled = canTest;
        btnCustom.Enabled = canTest;
        btnAllTests.Enabled = canTest;
        btnStopTest.Enabled = _recorder.Active;

        lblRunning.Text = _calibrating
            ? "Durum: KALIBRASYON"
            : (_state.Running ? "Durum: CALISIYOR" : "Durum: DURAKLATILDI");
    }

    private void SyncSettingsToControls()
    {
        _syncing = true;
        try
        {
            numVar.Value = ClampDec(_settings.ThresholdVar, numVar.Minimum, numVar.Maximum);
            numDel.Value = ClampDec(_settings.ThresholdDelta, numDel.Minimum, numDel.Maximum);
            numPtp.Value = ClampDec(_settings.ThresholdPtp, numPtp.Minimum, numPtp.Maximum);

            int sv = (int)Math.Round(_settings.Sensitivity * 10, MidpointRounding.AwayFromZero);
            trkSensitivity.Value = Math.Clamp(sv, trkSensitivity.Minimum, trkSensitivity.Maximum);
            lblSensVal.Text = _settings.Sensitivity.ToString("F1", CultureInfo.InvariantCulture);

            rbVar.Checked = _state.EditMetric == "var";
            rbDel.Checked = _state.EditMetric == "del";
            rbPtp.Checked = _state.EditMetric == "ptp";
        }
        finally
        {
            _syncing = false;
        }
    }

    // =================================================================
    // Motor olaylari
    // =================================================================

    private void OnMotionStarted(MotionInfo info)
    {
        SafeInvoke(() =>
        {
            if (_settings.SoundAlarm)
            {
                try { SystemSounds.Hand.Play(); } catch { /* yoksay */ }
            }
            AppendLog($"[{DateTime.Now:HH:mm:ss}] HAREKET BASLADI  {info.Magnitude}  ({info.Causes})  RSSI={info.Dbm}dBm");
        });
    }

    private void OnMotionEnded() =>
        SafeInvoke(() => AppendLog($"[{DateTime.Now:HH:mm:ss}] HAREKET BITTI"));

    // =================================================================
    // Klavye kisayollari (Python tus dongusu karsiligi)
    // =================================================================

    private async void OnKeyDownForm(object? sender, KeyEventArgs e)
    {
        // Bir giris kontrolu odaktayken tek-tus kisayollarini devralma
        if (ActiveControl is NumericUpDown or TextBoxBase or TrackBar or ListView)
            return;

        bool handled = true;
        switch (e.KeyCode)
        {
            case Keys.S: await StartDetectionAsync(); break;
            case Keys.T: StopDetection(); break;
            case Keys.C: await CalibrateOnlyAsync(); break;
            case Keys.Z: ToggleSound(); break;
            case Keys.I: ShowInfo(); break;
            case Keys.H: ShowHelp(); break;
            case Keys.E: StartNamedTest("e"); break;
            case Keys.Y: StartNamedTest("y"); break;
            case Keys.K: StartCustomTest(); break;
            case Keys.M: StartFromMenu(); break;
            case Keys.V: rbVar.Checked = true; SetStatus($"Duzenleniyor: VAR [{_settings.ThresholdVar:F2}]"); break;
            case Keys.D: rbDel.Checked = true; SetStatus($"Duzenleniyor: DEL [{_settings.ThresholdDelta:F2}]"); break;
            case Keys.P: rbPtp.Checked = true; SetStatus($"Duzenleniyor: PTP [{_settings.ThresholdPtp:F2}]"); break;
            case Keys.Space: Annotate(); break;

            case Keys.Up: AdjustMetricKey(+1.0); break;
            case Keys.Down: AdjustMetricKey(-1.0); break;
            case Keys.Right: AdjustMetricKey(+0.1); break;
            case Keys.Left: AdjustMetricKey(-0.1); break;

            case Keys.Oemplus:
            case Keys.Add: AdjustSensitivityKey(+0.1); break;
            case Keys.OemMinus:
            case Keys.Subtract: AdjustSensitivityKey(-0.1); break;

            case Keys.D1: case Keys.NumPad1: ToggleApSlot(0); break;
            case Keys.D2: case Keys.NumPad2: ToggleApSlot(1); break;
            case Keys.D3: case Keys.NumPad3: ToggleApSlot(2); break;
            case Keys.D4: case Keys.NumPad4: ToggleApSlot(3); break;
            case Keys.D5: case Keys.NumPad5: ToggleApSlot(4); break;
            case Keys.D6: case Keys.NumPad6: ToggleApSlot(5); break;

            default: handled = false; break;
        }

        if (handled)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void AdjustMetricKey(double step)
    {
        _engine.AdjustMetric(step);
        SyncSettingsToControls();
        string val = _state.EditMetric switch
        {
            "var" => _settings.ThresholdVar.ToString("F2", CultureInfo.InvariantCulture),
            "del" => _settings.ThresholdDelta.ToString("F2", CultureInfo.InvariantCulture),
            _ => _settings.ThresholdPtp.ToString("F2", CultureInfo.InvariantCulture),
        };
        SetStatus($"{_state.EditMetric.ToUpperInvariant()} esigi: {val}");
    }

    private void AdjustSensitivityKey(double step)
    {
        _engine.AdjustSensitivity(step);
        SyncSettingsToControls();
        SetStatus($"Hassasiyet {_settings.Sensitivity:F1}/15.0");
    }

    private void ToggleApSlot(int idx)
    {
        if (idx < _state.ApSlots.Count)
        {
            _state.ApSlots[idx].Enabled = !_state.ApSlots[idx].Enabled;
            string st = _state.ApSlots[idx].Enabled ? "ACIK" : "KAPALI";
            SetStatus($"Slot {idx + 1} ({Trunc(_state.ApSlots[idx].Ssid, 20)}) → {st}");
            RefreshAps();
        }
        else
        {
            SetStatus($"Slot {idx + 1} bos.");
        }
    }

    // =================================================================
    // Kapanis
    // =================================================================

    private void OnClosingForm(object? sender, FormClosingEventArgs e)
    {
        try { _cts?.Cancel(); } catch { /* yoksay */ }
        _state.Running = false;
        if (_recorder.Active)
            _recorder.Stop();
        _settings.ToFile();
        try { _loopThread?.Join(1000); } catch { /* yoksay */ }
        try { _engine.Wlan.Cleanup(); } catch { /* yoksay */ }
    }

    // =================================================================
    // Yardimcilar
    // =================================================================

    private void SetStatus(string text)
    {
        lblStatus.Text = text;
    }

    private void AppendLog(string text)
    {
        if (txtLog.IsDisposed)
            return;
        txtLog.AppendText(text + Environment.NewLine);
        if (txtLog.Lines.Length > 300)
            txtLog.Lines = txtLog.Lines.Skip(txtLog.Lines.Length - 200).ToArray();
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    private void SafeInvoke(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try { BeginInvoke(action); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private static decimal ClampDec(double value, decimal min, decimal max)
    {
        decimal d = (decimal)value;
        if (d < min) return min;
        if (d > max) return max;
        return d;
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
}
