namespace WifiMotion.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            _cts?.Dispose();
            _engine?.Wlan.Dispose();
        }
        base.Dispose(disposing);
    }

    private System.Windows.Forms.ToolStrip toolStripMain;
    private System.Windows.Forms.ToolStripButton tsBtnStart;
    private System.Windows.Forms.ToolStripButton tsBtnStop;
    private System.Windows.Forms.ToolStripButton tsBtnCalibrate;
    private System.Windows.Forms.ToolStripButton tsBtnSound;
    private System.Windows.Forms.ToolStripButton tsBtnInfo;
    private System.Windows.Forms.ToolStripButton tsBtnHelp;

    private System.Windows.Forms.StatusStrip statusStripMain;
    private System.Windows.Forms.ToolStripStatusLabel lblStatus;

    private System.Windows.Forms.TableLayoutPanel layoutRoot;

    private System.Windows.Forms.GroupBox gbStatus;
    private System.Windows.Forms.Label lblMotion;
    private System.Windows.Forms.Label lblSignalLine;
    private System.Windows.Forms.Label lblQuality;
    private System.Windows.Forms.Label lblExtra;
    private System.Windows.Forms.Label lblRunning;

    private System.Windows.Forms.GroupBox gbGraph;
    private WifiMotion.UI.RssiGraphControl graph;

    private System.Windows.Forms.GroupBox gbMetrics;
    private System.Windows.Forms.TableLayoutPanel metricsLayout;
    private System.Windows.Forms.Label lblHdrMetric;
    private System.Windows.Forms.Label lblHdrValue;
    private System.Windows.Forms.Label lblHdrThr;
    private System.Windows.Forms.Label lblHdrSel;
    private System.Windows.Forms.Label lblVarName;
    private System.Windows.Forms.Label lblVarVal;
    private System.Windows.Forms.NumericUpDown numVar;
    private System.Windows.Forms.RadioButton rbVar;
    private System.Windows.Forms.Label lblDelName;
    private System.Windows.Forms.Label lblDelVal;
    private System.Windows.Forms.NumericUpDown numDel;
    private System.Windows.Forms.RadioButton rbDel;
    private System.Windows.Forms.Label lblPtpName;
    private System.Windows.Forms.Label lblPtpVal;
    private System.Windows.Forms.NumericUpDown numPtp;
    private System.Windows.Forms.RadioButton rbPtp;
    private System.Windows.Forms.Label lblRxv;
    private System.Windows.Forms.Label lblSensCaption;
    private System.Windows.Forms.TrackBar trkSensitivity;
    private System.Windows.Forms.Label lblSensVal;

    private System.Windows.Forms.TableLayoutPanel rightLayout;
    private System.Windows.Forms.GroupBox gbAps;
    private System.Windows.Forms.ListView lstAps;
    private System.Windows.Forms.ColumnHeader chSlot;
    private System.Windows.Forms.ColumnHeader chSsid;
    private System.Windows.Forms.ColumnHeader chDbm;
    private System.Windows.Forms.ColumnHeader chQuality;
    private System.Windows.Forms.ColumnHeader chTrend;
    private System.Windows.Forms.ColumnHeader chVar;

    private System.Windows.Forms.GroupBox gbTest;
    private System.Windows.Forms.TableLayoutPanel testLayout;
    private System.Windows.Forms.Button btnHandwave;
    private System.Windows.Forms.Button btnDirection;
    private System.Windows.Forms.Button btnCustom;
    private System.Windows.Forms.Button btnAllTests;
    private System.Windows.Forms.Label lblTestStatus;
    private System.Windows.Forms.ProgressBar prgTest;
    private System.Windows.Forms.Button btnAnnotate;
    private System.Windows.Forms.Button btnStopTest;

    private System.Windows.Forms.GroupBox gbLog;
    private System.Windows.Forms.TextBox txtLog;

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.toolStripMain = new System.Windows.Forms.ToolStrip();
        this.tsBtnStart = new System.Windows.Forms.ToolStripButton();
        this.tsBtnStop = new System.Windows.Forms.ToolStripButton();
        this.tsBtnCalibrate = new System.Windows.Forms.ToolStripButton();
        this.tsBtnSound = new System.Windows.Forms.ToolStripButton();
        this.tsBtnInfo = new System.Windows.Forms.ToolStripButton();
        this.tsBtnHelp = new System.Windows.Forms.ToolStripButton();
        this.statusStripMain = new System.Windows.Forms.StatusStrip();
        this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
        this.layoutRoot = new System.Windows.Forms.TableLayoutPanel();
        this.gbStatus = new System.Windows.Forms.GroupBox();
        this.lblMotion = new System.Windows.Forms.Label();
        this.lblSignalLine = new System.Windows.Forms.Label();
        this.lblQuality = new System.Windows.Forms.Label();
        this.lblExtra = new System.Windows.Forms.Label();
        this.lblRunning = new System.Windows.Forms.Label();
        this.gbGraph = new System.Windows.Forms.GroupBox();
        this.graph = new WifiMotion.UI.RssiGraphControl();
        this.gbMetrics = new System.Windows.Forms.GroupBox();
        this.metricsLayout = new System.Windows.Forms.TableLayoutPanel();
        this.lblHdrMetric = new System.Windows.Forms.Label();
        this.lblHdrValue = new System.Windows.Forms.Label();
        this.lblHdrThr = new System.Windows.Forms.Label();
        this.lblHdrSel = new System.Windows.Forms.Label();
        this.lblVarName = new System.Windows.Forms.Label();
        this.lblVarVal = new System.Windows.Forms.Label();
        this.numVar = new System.Windows.Forms.NumericUpDown();
        this.rbVar = new System.Windows.Forms.RadioButton();
        this.lblDelName = new System.Windows.Forms.Label();
        this.lblDelVal = new System.Windows.Forms.Label();
        this.numDel = new System.Windows.Forms.NumericUpDown();
        this.rbDel = new System.Windows.Forms.RadioButton();
        this.lblPtpName = new System.Windows.Forms.Label();
        this.lblPtpVal = new System.Windows.Forms.Label();
        this.numPtp = new System.Windows.Forms.NumericUpDown();
        this.rbPtp = new System.Windows.Forms.RadioButton();
        this.lblRxv = new System.Windows.Forms.Label();
        this.lblSensCaption = new System.Windows.Forms.Label();
        this.trkSensitivity = new System.Windows.Forms.TrackBar();
        this.lblSensVal = new System.Windows.Forms.Label();
        this.rightLayout = new System.Windows.Forms.TableLayoutPanel();
        this.gbAps = new System.Windows.Forms.GroupBox();
        this.lstAps = new System.Windows.Forms.ListView();
        this.chSlot = new System.Windows.Forms.ColumnHeader();
        this.chSsid = new System.Windows.Forms.ColumnHeader();
        this.chDbm = new System.Windows.Forms.ColumnHeader();
        this.chQuality = new System.Windows.Forms.ColumnHeader();
        this.chTrend = new System.Windows.Forms.ColumnHeader();
        this.chVar = new System.Windows.Forms.ColumnHeader();
        this.gbTest = new System.Windows.Forms.GroupBox();
        this.testLayout = new System.Windows.Forms.TableLayoutPanel();
        this.btnHandwave = new System.Windows.Forms.Button();
        this.btnDirection = new System.Windows.Forms.Button();
        this.btnCustom = new System.Windows.Forms.Button();
        this.btnAllTests = new System.Windows.Forms.Button();
        this.lblTestStatus = new System.Windows.Forms.Label();
        this.prgTest = new System.Windows.Forms.ProgressBar();
        this.btnAnnotate = new System.Windows.Forms.Button();
        this.btnStopTest = new System.Windows.Forms.Button();
        this.gbLog = new System.Windows.Forms.GroupBox();
        this.txtLog = new System.Windows.Forms.TextBox();

        this.toolStripMain.SuspendLayout();
        this.statusStripMain.SuspendLayout();
        this.layoutRoot.SuspendLayout();
        this.gbStatus.SuspendLayout();
        this.gbGraph.SuspendLayout();
        this.gbMetrics.SuspendLayout();
        this.metricsLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.numVar)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.numDel)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.numPtp)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.trkSensitivity)).BeginInit();
        this.rightLayout.SuspendLayout();
        this.gbAps.SuspendLayout();
        this.gbTest.SuspendLayout();
        this.testLayout.SuspendLayout();
        this.gbLog.SuspendLayout();
        this.SuspendLayout();

        // toolStripMain
        this.toolStripMain.ImageScalingSize = new System.Drawing.Size(20, 20);
        this.toolStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsBtnStart,
            this.tsBtnStop,
            this.tsBtnCalibrate,
            new System.Windows.Forms.ToolStripSeparator(),
            this.tsBtnSound,
            new System.Windows.Forms.ToolStripSeparator(),
            this.tsBtnInfo,
            this.tsBtnHelp});
        this.toolStripMain.Location = new System.Drawing.Point(0, 0);
        this.toolStripMain.Name = "toolStripMain";
        this.toolStripMain.Padding = new System.Windows.Forms.Padding(6, 2, 0, 2);
        this.toolStripMain.Size = new System.Drawing.Size(1014, 27);
        this.toolStripMain.TabIndex = 0;

        this.tsBtnStart.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsBtnStart.Name = "tsBtnStart";
        this.tsBtnStart.Text = "Baslat (S)";
        this.tsBtnStart.ToolTipText = "Kalibrasyon + algilamayi baslatir";

        this.tsBtnStop.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsBtnStop.Name = "tsBtnStop";
        this.tsBtnStop.Text = "Durdur (T)";

        this.tsBtnCalibrate.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsBtnCalibrate.Name = "tsBtnCalibrate";
        this.tsBtnCalibrate.Text = "Kalibrasyon (C)";

        this.tsBtnSound.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsBtnSound.Name = "tsBtnSound";
        this.tsBtnSound.Text = "Ses: ACIK (Z)";

        this.tsBtnInfo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsBtnInfo.Name = "tsBtnInfo";
        this.tsBtnInfo.Text = "Bilgi (I)";

        this.tsBtnHelp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsBtnHelp.Name = "tsBtnHelp";
        this.tsBtnHelp.Text = "Yardim (H)";

        // statusStripMain
        this.statusStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.lblStatus });
        this.statusStripMain.Location = new System.Drawing.Point(0, 705);
        this.statusStripMain.Name = "statusStripMain";
        this.statusStripMain.Size = new System.Drawing.Size(1014, 22);
        this.statusStripMain.TabIndex = 2;
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Text = "Hosgeldiniz! [Baslat] ile baslayin.";

        // layoutRoot
        this.layoutRoot.ColumnCount = 2;
        this.layoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
        this.layoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
        this.layoutRoot.RowCount = 4;
        this.layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        this.layoutRoot.Controls.Add(this.gbStatus, 0, 0);
        this.layoutRoot.Controls.Add(this.gbGraph, 0, 1);
        this.layoutRoot.Controls.Add(this.gbMetrics, 0, 2);
        this.layoutRoot.Controls.Add(this.rightLayout, 1, 0);
        this.layoutRoot.Controls.Add(this.gbLog, 0, 3);
        this.layoutRoot.SetRowSpan(this.rightLayout, 3);
        this.layoutRoot.SetColumnSpan(this.gbLog, 2);
        this.layoutRoot.Dock = System.Windows.Forms.DockStyle.Fill;
        this.layoutRoot.Location = new System.Drawing.Point(0, 27);
        this.layoutRoot.Name = "layoutRoot";
        this.layoutRoot.Padding = new System.Windows.Forms.Padding(6);
        this.layoutRoot.Size = new System.Drawing.Size(1014, 678);
        this.layoutRoot.TabIndex = 1;

        // gbStatus
        this.gbStatus.Controls.Add(this.lblMotion);
        this.gbStatus.Controls.Add(this.lblSignalLine);
        this.gbStatus.Controls.Add(this.lblQuality);
        this.gbStatus.Controls.Add(this.lblExtra);
        this.gbStatus.Controls.Add(this.lblRunning);
        this.gbStatus.Dock = System.Windows.Forms.DockStyle.Fill;
        this.gbStatus.Name = "gbStatus";
        this.gbStatus.Padding = new System.Windows.Forms.Padding(8, 4, 8, 8);
        this.gbStatus.Text = "Durum";
        this.gbStatus.AutoSize = true;
        this.gbStatus.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

        this.lblMotion.AutoSize = true;
        this.lblMotion.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblMotion.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
        this.lblMotion.ForeColor = System.Drawing.Color.ForestGreen;
        this.lblMotion.Name = "lblMotion";
        this.lblMotion.Text = "Hareket: HAYIR";
        this.lblMotion.Padding = new System.Windows.Forms.Padding(0, 4, 0, 2);

        this.lblSignalLine.AutoSize = true;
        this.lblSignalLine.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblSignalLine.Font = new System.Drawing.Font("Segoe UI", 9.5F);
        this.lblSignalLine.Name = "lblSignalLine";
        this.lblSignalLine.Text = "Sinyal: --%   RSSI: -- dBm   RX:-- TX:-- Mbps";

        this.lblQuality.AutoSize = true;
        this.lblQuality.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblQuality.Font = new System.Drawing.Font("Consolas", 9.5F);
        this.lblQuality.Name = "lblQuality";
        this.lblQuality.Text = "[░░░░░░░░░░] ---";

        this.lblExtra.AutoSize = true;
        this.lblExtra.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblExtra.Font = new System.Drawing.Font("Segoe UI", 9.5F);
        this.lblExtra.Name = "lblExtra";
        this.lblExtra.Text = "Boyut: -   Frekans: -   Yon: -";

        this.lblRunning.AutoSize = true;
        this.lblRunning.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblRunning.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
        this.lblRunning.ForeColor = System.Drawing.Color.DimGray;
        this.lblRunning.Name = "lblRunning";
        this.lblRunning.Text = "Durum: DURAKLATILDI";

        // gbGraph
        this.gbGraph.Controls.Add(this.graph);
        this.gbGraph.Dock = System.Windows.Forms.DockStyle.Fill;
        this.gbGraph.Name = "gbGraph";
        this.gbGraph.Padding = new System.Windows.Forms.Padding(6);
        this.gbGraph.Text = "RSSI Dalgasi";

        this.graph.Dock = System.Windows.Forms.DockStyle.Fill;
        this.graph.Name = "graph";
        this.graph.TabStop = false;

        // gbMetrics
        this.gbMetrics.Controls.Add(this.metricsLayout);
        this.gbMetrics.Dock = System.Windows.Forms.DockStyle.Fill;
        this.gbMetrics.Name = "gbMetrics";
        this.gbMetrics.Padding = new System.Windows.Forms.Padding(6);
        this.gbMetrics.Text = "Metrikler & Hassasiyet";
        this.gbMetrics.AutoSize = true;
        this.gbMetrics.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

        // metricsLayout
        this.metricsLayout.ColumnCount = 4;
        this.metricsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 60F));
        this.metricsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.metricsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
        this.metricsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 60F));
        this.metricsLayout.RowCount = 6;
        for (int i = 0; i < 5; i++)
            this.metricsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.metricsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.metricsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.metricsLayout.Name = "metricsLayout";
        this.metricsLayout.AutoSize = true;
        this.metricsLayout.Controls.Add(this.lblHdrMetric, 0, 0);
        this.metricsLayout.Controls.Add(this.lblHdrValue, 1, 0);
        this.metricsLayout.Controls.Add(this.lblHdrThr, 2, 0);
        this.metricsLayout.Controls.Add(this.lblHdrSel, 3, 0);
        this.metricsLayout.Controls.Add(this.lblVarName, 0, 1);
        this.metricsLayout.Controls.Add(this.lblVarVal, 1, 1);
        this.metricsLayout.Controls.Add(this.numVar, 2, 1);
        this.metricsLayout.Controls.Add(this.rbVar, 3, 1);
        this.metricsLayout.Controls.Add(this.lblDelName, 0, 2);
        this.metricsLayout.Controls.Add(this.lblDelVal, 1, 2);
        this.metricsLayout.Controls.Add(this.numDel, 2, 2);
        this.metricsLayout.Controls.Add(this.rbDel, 3, 2);
        this.metricsLayout.Controls.Add(this.lblPtpName, 0, 3);
        this.metricsLayout.Controls.Add(this.lblPtpVal, 1, 3);
        this.metricsLayout.Controls.Add(this.numPtp, 2, 3);
        this.metricsLayout.Controls.Add(this.rbPtp, 3, 3);
        this.metricsLayout.Controls.Add(this.lblRxv, 0, 4);
        this.metricsLayout.SetColumnSpan(this.lblRxv, 4);
        this.metricsLayout.Controls.Add(this.lblSensCaption, 0, 5);
        this.metricsLayout.Controls.Add(this.trkSensitivity, 1, 5);
        this.metricsLayout.SetColumnSpan(this.trkSensitivity, 2);
        this.metricsLayout.Controls.Add(this.lblSensVal, 3, 5);

        this.lblHdrMetric.AutoSize = true; this.lblHdrMetric.Text = ""; this.lblHdrMetric.Margin = new System.Windows.Forms.Padding(3);
        this.lblHdrValue.AutoSize = true; this.lblHdrValue.Text = "Deger"; this.lblHdrValue.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
        this.lblHdrThr.AutoSize = true; this.lblHdrThr.Text = "Esik"; this.lblHdrThr.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
        this.lblHdrSel.AutoSize = true; this.lblHdrSel.Text = "Sec"; this.lblHdrSel.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);

        this.lblVarName.AutoSize = true; this.lblVarName.Text = "VAR"; this.lblVarName.Anchor = System.Windows.Forms.AnchorStyles.Left; this.lblVarName.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Bold);
        this.lblVarVal.AutoSize = true; this.lblVarVal.Text = "0.0"; this.lblVarVal.Anchor = System.Windows.Forms.AnchorStyles.Left; this.lblVarVal.Font = new System.Drawing.Font("Consolas", 10F);
        this.numVar.DecimalPlaces = 2; this.numVar.Increment = 0.1M; this.numVar.Maximum = 500M; this.numVar.Minimum = 0.1M; this.numVar.Value = 10M; this.numVar.Name = "numVar"; this.numVar.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.rbVar.AutoSize = true; this.rbVar.Checked = true; this.rbVar.Name = "rbVar"; this.rbVar.Anchor = System.Windows.Forms.AnchorStyles.Left;

        this.lblDelName.AutoSize = true; this.lblDelName.Text = "DEL"; this.lblDelName.Anchor = System.Windows.Forms.AnchorStyles.Left; this.lblDelName.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Bold);
        this.lblDelVal.AutoSize = true; this.lblDelVal.Text = "0.0"; this.lblDelVal.Anchor = System.Windows.Forms.AnchorStyles.Left; this.lblDelVal.Font = new System.Drawing.Font("Consolas", 10F);
        this.numDel.DecimalPlaces = 2; this.numDel.Increment = 0.1M; this.numDel.Maximum = 100M; this.numDel.Minimum = 0.1M; this.numDel.Value = 3M; this.numDel.Name = "numDel"; this.numDel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.rbDel.AutoSize = true; this.rbDel.Name = "rbDel"; this.rbDel.Anchor = System.Windows.Forms.AnchorStyles.Left;

        this.lblPtpName.AutoSize = true; this.lblPtpName.Text = "PTP"; this.lblPtpName.Anchor = System.Windows.Forms.AnchorStyles.Left; this.lblPtpName.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Bold);
        this.lblPtpVal.AutoSize = true; this.lblPtpVal.Text = "0.0"; this.lblPtpVal.Anchor = System.Windows.Forms.AnchorStyles.Left; this.lblPtpVal.Font = new System.Drawing.Font("Consolas", 10F);
        this.numPtp.DecimalPlaces = 2; this.numPtp.Increment = 0.1M; this.numPtp.Maximum = 200M; this.numPtp.Minimum = 0.1M; this.numPtp.Value = 5M; this.numPtp.Name = "numPtp"; this.numPtp.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.rbPtp.AutoSize = true; this.rbPtp.Name = "rbPtp"; this.rbPtp.Anchor = System.Windows.Forms.AnchorStyles.Left;

        this.lblRxv.AutoSize = true; this.lblRxv.Text = "RXv: 0.0"; this.lblRxv.Font = new System.Drawing.Font("Consolas", 9F); this.lblRxv.ForeColor = System.Drawing.Color.DimGray; this.lblRxv.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);

        this.lblSensCaption.AutoSize = true; this.lblSensCaption.Text = "Hassasiyet"; this.lblSensCaption.Anchor = System.Windows.Forms.AnchorStyles.Left; this.lblSensCaption.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
        this.trkSensitivity.Minimum = 1; this.trkSensitivity.Maximum = 150; this.trkSensitivity.Value = 150; this.trkSensitivity.TickFrequency = 10; this.trkSensitivity.Name = "trkSensitivity"; this.trkSensitivity.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSensVal.AutoSize = true; this.lblSensVal.Text = "15.0"; this.lblSensVal.Anchor = System.Windows.Forms.AnchorStyles.Left; this.lblSensVal.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Bold); this.lblSensVal.Margin = new System.Windows.Forms.Padding(3, 10, 3, 3);

        // rightLayout
        this.rightLayout.ColumnCount = 1;
        this.rightLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.rightLayout.RowCount = 2;
        this.rightLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.rightLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.rightLayout.Controls.Add(this.gbAps, 0, 0);
        this.rightLayout.Controls.Add(this.gbTest, 0, 1);
        this.rightLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.rightLayout.Name = "rightLayout";
        this.rightLayout.Margin = new System.Windows.Forms.Padding(0);

        // gbAps
        this.gbAps.Controls.Add(this.lstAps);
        this.gbAps.Dock = System.Windows.Forms.DockStyle.Fill;
        this.gbAps.Name = "gbAps";
        this.gbAps.Padding = new System.Windows.Forms.Padding(6);
        this.gbAps.Text = "Gorunen AP'ler (isaretle = ac/kapat)";

        this.lstAps.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chSlot, this.chSsid, this.chDbm, this.chQuality, this.chTrend, this.chVar });
        this.chSlot.Text = "#"; this.chSlot.Width = 30;
        this.chSsid.Text = "SSID"; this.chSsid.Width = 150;
        this.chDbm.Text = "dBm"; this.chDbm.Width = 55; this.chDbm.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
        this.chQuality.Text = "Kalite"; this.chQuality.Width = 55; this.chQuality.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
        this.chTrend.Text = "Trend"; this.chTrend.Width = 50; this.chTrend.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
        this.chVar.Text = "Var"; this.chVar.Width = 60; this.chVar.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
        this.lstAps.CheckBoxes = true;
        this.lstAps.FullRowSelect = true;
        this.lstAps.GridLines = true;
        this.lstAps.UseCompatibleStateImageBehavior = false;
        this.lstAps.View = System.Windows.Forms.View.Details;
        this.lstAps.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lstAps.Name = "lstAps";
        this.lstAps.Font = new System.Drawing.Font("Consolas", 9F);

        // gbTest
        this.gbTest.Controls.Add(this.testLayout);
        this.gbTest.Dock = System.Windows.Forms.DockStyle.Fill;
        this.gbTest.Name = "gbTest";
        this.gbTest.Padding = new System.Windows.Forms.Padding(6);
        this.gbTest.Text = "Testler";
        this.gbTest.AutoSize = true;
        this.gbTest.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

        // testLayout
        this.testLayout.ColumnCount = 2;
        this.testLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this.testLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this.testLayout.RowCount = 4;
        for (int i = 0; i < 4; i++)
            this.testLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.testLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.testLayout.AutoSize = true;
        this.testLayout.Name = "testLayout";
        this.testLayout.Controls.Add(this.btnHandwave, 0, 0);
        this.testLayout.Controls.Add(this.btnDirection, 1, 0);
        this.testLayout.Controls.Add(this.btnCustom, 0, 1);
        this.testLayout.Controls.Add(this.btnAllTests, 1, 1);
        this.testLayout.Controls.Add(this.lblTestStatus, 0, 2);
        this.testLayout.SetColumnSpan(this.lblTestStatus, 2);
        this.testLayout.Controls.Add(this.prgTest, 0, 3);
        this.testLayout.Controls.Add(this.btnStopTest, 1, 3);

        this.btnHandwave.Text = "El Sallama (E)"; this.btnHandwave.Name = "btnHandwave"; this.btnHandwave.Dock = System.Windows.Forms.DockStyle.Fill; this.btnHandwave.AutoSize = true;
        this.btnDirection.Text = "Yon Testi (Y)"; this.btnDirection.Name = "btnDirection"; this.btnDirection.Dock = System.Windows.Forms.DockStyle.Fill; this.btnDirection.AutoSize = true;
        this.btnCustom.Text = "Custom (K)"; this.btnCustom.Name = "btnCustom"; this.btnCustom.Dock = System.Windows.Forms.DockStyle.Fill; this.btnCustom.AutoSize = true;
        this.btnAllTests.Text = "Tum Testler (M)"; this.btnAllTests.Name = "btnAllTests"; this.btnAllTests.Dock = System.Windows.Forms.DockStyle.Fill; this.btnAllTests.AutoSize = true;
        this.lblTestStatus.AutoSize = true; this.lblTestStatus.Text = "Test yok."; this.lblTestStatus.Name = "lblTestStatus"; this.lblTestStatus.Font = new System.Drawing.Font("Segoe UI", 8.5F); this.lblTestStatus.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3); this.lblTestStatus.MaximumSize = new System.Drawing.Size(360, 0);
        this.prgTest.Name = "prgTest"; this.prgTest.Dock = System.Windows.Forms.DockStyle.Fill; this.prgTest.Height = 18;
        this.btnStopTest.Text = "Testi Durdur"; this.btnStopTest.Name = "btnStopTest"; this.btnStopTest.Dock = System.Windows.Forms.DockStyle.Fill; this.btnStopTest.AutoSize = true; this.btnStopTest.Enabled = false;

        // (btnAnnotate, eklenen not butonu, durum cubugu uzerinden de erisilebilir)
        this.btnAnnotate.Text = "Not Ekle (Space)"; this.btnAnnotate.Name = "btnAnnotate"; this.btnAnnotate.Visible = false;

        // gbLog
        this.gbLog.Controls.Add(this.txtLog);
        this.gbLog.Dock = System.Windows.Forms.DockStyle.Fill;
        this.gbLog.Name = "gbLog";
        this.gbLog.Padding = new System.Windows.Forms.Padding(6);
        this.gbLog.Text = "Gunluk";

        this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtLog.Multiline = true;
        this.txtLog.ReadOnly = true;
        this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.txtLog.Name = "txtLog";
        this.txtLog.Font = new System.Drawing.Font("Consolas", 9F);
        this.txtLog.BackColor = System.Drawing.Color.FromArgb(24, 26, 30);
        this.txtLog.ForeColor = System.Drawing.Color.Gainsboro;

        // MainForm
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1014, 727);
        this.Controls.Add(this.layoutRoot);
        this.Controls.Add(this.statusStripMain);
        this.Controls.Add(this.toolStripMain);
        this.KeyPreview = true;
        this.MinimumSize = new System.Drawing.Size(900, 680);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "WiFi Motion";

        this.toolStripMain.ResumeLayout(false);
        this.toolStripMain.PerformLayout();
        this.statusStripMain.ResumeLayout(false);
        this.statusStripMain.PerformLayout();
        this.layoutRoot.ResumeLayout(false);
        this.layoutRoot.PerformLayout();
        this.gbStatus.ResumeLayout(false);
        this.gbStatus.PerformLayout();
        this.gbGraph.ResumeLayout(false);
        this.gbMetrics.ResumeLayout(false);
        this.gbMetrics.PerformLayout();
        this.metricsLayout.ResumeLayout(false);
        this.metricsLayout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.numVar)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.numDel)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.numPtp)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.trkSensitivity)).EndInit();
        this.rightLayout.ResumeLayout(false);
        this.gbAps.ResumeLayout(false);
        this.gbTest.ResumeLayout(false);
        this.gbTest.PerformLayout();
        this.testLayout.ResumeLayout(false);
        this.testLayout.PerformLayout();
        this.gbLog.ResumeLayout(false);
        this.gbLog.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
