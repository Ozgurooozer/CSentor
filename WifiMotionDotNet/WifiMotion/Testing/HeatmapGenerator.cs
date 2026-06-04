using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WifiMotion.Testing;

/// <summary>Test ciktilari icin yol yardimcilari.</summary>
public static class TestPaths
{
    public static string TestDir => Path.Combine(AppContext.BaseDirectory, "test_output");

    public static string EnsureTestDir()
    {
        Directory.CreateDirectory(TestDir);
        return TestDir;
    }
}

/// <summary>
/// Test kaydindan 4 panelli bir analiz grafigi (PNG) uretir. Python'daki
/// matplotlib tabanli <c>generate_heatmap</c> fonksiyonunun GDI+ (System.Drawing)
/// karsiligi — boylece harici bagimlilik gerekmez.
///
/// Paneller: (1) RSSI dBm, (2) VAR/DEL/PTP metrikleri (log), (3) AP RSSI
/// isi haritasi, (4) RX/TX hizlari + sinyal %.
/// </summary>
public static class HeatmapGenerator
{
    private static readonly Color ColRssi = ColorFromHex("#2196F3");
    private static readonly Color ColVar = ColorFromHex("#FF5722");
    private static readonly Color ColDelta = ColorFromHex("#4CAF50");
    private static readonly Color ColPtp = ColorFromHex("#9C27B0");
    private static readonly Color ColRx = ColorFromHex("#00BCD4");
    private static readonly Color ColTx = ColorFromHex("#FF9800");
    private static readonly Color ColGrid = Color.FromArgb(40, 0, 0, 0);
    private static readonly Color ColAxis = Color.FromArgb(120, 0, 0, 0);

    private const int Width = 1400;
    private const int Height = 1200;
    private const int MarginLeft = 90;
    private const int MarginRight = 110;
    private const int MarginTop = 60;
    private const int MarginBottom = 50;
    private const int PanelGap = 24;

    /// <summary>
    /// Kayittan grafik uretir. Basarisizlikta (veya yetersiz veri) null doner.
    /// </summary>
    public static string? Generate(TestRecording rec, string? outputPath = null)
    {
        if (rec is null || rec.Timestamps.Count == 0)
            return null;

        try
        {
            outputPath ??= Path.Combine(
                TestPaths.EnsureTestDir(),
                $"{rec.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            double t0 = rec.StartTime != 0 ? rec.StartTime : rec.Timestamps[0];
            int n = new[] { rec.Timestamps.Count, rec.RssiDbm.Count, rec.Var.Count, rec.Delta.Count, rec.Ptp.Count }.Min();
            if (n < 2)
                return null;

            var t = new double[n];
            for (int i = 0; i < n; i++)
                t[i] = rec.Timestamps[i] - t0;
            double duration = t[^1];
            if (duration <= 0)
                duration = 1;

            using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);

            using var titleFont = new Font("Segoe UI", 13, FontStyle.Bold);
            using var labelFont = new Font("Segoe UI", 8.5f);
            using var smallFont = new Font("Segoe UI", 7.5f);
            using var blackBrush = new SolidBrush(Color.Black);

            string title = $"WiFi Motion Test: {rec.Name}  |  {n} ornek  |  {duration:F1}s";
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                g.DrawString(title, titleFont, blackBrush, new RectangleF(0, 12, Width, 28), sf);

            int panelAreaTop = MarginTop;
            int panelAreaBottom = Height - MarginBottom;
            int totalH = panelAreaBottom - panelAreaTop;
            int panelH = (totalH - 3 * PanelGap) / 4;

            var panels = new Rectangle[4];
            for (int i = 0; i < 4; i++)
            {
                int top = panelAreaTop + i * (panelH + PanelGap);
                panels[i] = new Rectangle(MarginLeft, top, Width - MarginLeft - MarginRight, panelH);
            }

            DrawRssiPanel(g, panels[0], t, rec, n, duration, labelFont, smallFont, blackBrush);
            DrawMetricsPanel(g, panels[1], t, rec, n, duration, labelFont, blackBrush);
            DrawApPanel(g, panels[2], rec, n, duration, labelFont, smallFont, blackBrush);
            DrawRatesPanel(g, panels[3], t, rec, n, duration, labelFont, blackBrush);

            bmp.Save(outputPath, ImageFormat.Png);
            return outputPath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // -----------------------------------------------------------------
    // Panel 1: RSSI (dBm)
    // -----------------------------------------------------------------
    private static void DrawRssiPanel(Graphics g, Rectangle r, double[] t, TestRecording rec, int n,
        double duration, Font labelFont, Font smallFont, Brush textBrush)
    {
        double lo = rec.RssiDbm.Take(n).Min() - 5;
        double hi = rec.RssiDbm.Take(n).Max() + 5;
        DrawAxes(g, r, lo, hi, duration, labelFont, textBrush, "RSSI (dBm)");

        // Asama (phase) cizgileri
        using (var phasePen = new Pen(Color.FromArgb(70, 128, 128, 128)) { DashStyle = DashStyle.Dot })
            foreach (var p in rec.Phases)
                if (p.AtSec <= duration)
                {
                    float x = MapX(r, p.AtSec, duration);
                    g.DrawLine(phasePen, x, r.Top, x, r.Bottom);
                }

        // Not (annotation) cizgileri + etiketler
        double t0Rel = rec.StartTime != 0 ? rec.StartTime : (rec.Timestamps.Count > 0 ? rec.Timestamps[0] : 0);
        using (var annPen = new Pen(Color.FromArgb(110, 220, 0, 0)) { DashStyle = DashStyle.Dash })
        using (var annBrush = new SolidBrush(Color.FromArgb(180, 255, 245, 120)))
        {
            foreach (var (ats, label) in rec.Annotations)
            {
                double rel = ats - t0Rel;
                if (rel < 0 || rel > duration)
                    continue;
                float x = MapX(r, rel, duration);
                g.DrawLine(annPen, x, r.Top, x, r.Bottom);
                var sz = g.MeasureString(label, smallFont);
                var lblRect = new RectangleF(x + 2, r.Top + 2, sz.Width + 4, sz.Height + 2);
                g.FillRectangle(annBrush, lblRect);
                g.DrawString(label, smallFont, textBrush, x + 4, r.Top + 3);
            }
        }

        DrawSeries(g, r, t, rec.RssiDbm.Take(n).Select(v => (double)v).ToArray(), lo, hi, ColRssi, 1.8f);
        DrawLegend(g, r, smallFont, new[] { ("RSSI (dBm)", ColRssi) });
    }

    // -----------------------------------------------------------------
    // Panel 2: Metrikler (log olcek)
    // -----------------------------------------------------------------
    private static void DrawMetricsPanel(Graphics g, Rectangle r, double[] t, TestRecording rec, int n,
        double duration, Font labelFont, Brush textBrush)
    {
        var all = rec.Var.Take(n).Concat(rec.Delta.Take(n)).Concat(rec.Ptp.Take(n)).ToList();
        bool useLog = all.Count > 0 && all.Max() > 0;

        double minPos = all.Where(v => v > 0).DefaultIfEmpty(0.01).Min();
        double maxV = all.DefaultIfEmpty(1).Max();
        if (maxV <= 0) maxV = 1;
        double loLog = Math.Log10(Math.Max(1e-6, minPos));
        double hiLog = Math.Log10(Math.Max(minPos * 10, maxV));
        if (hiLog - loLog < 1) hiLog = loLog + 1;

        DrawAxes(g, r, useLog ? loLog : 0, useLog ? hiLog : maxV, duration, labelFont, textBrush,
            useLog ? "Metrikler (log)" : "Metrikler");

        double Map(double v) => useLog ? Math.Log10(Math.Max(1e-6, v)) : v;

        DrawSeries(g, r, t, rec.Var.Take(n).Select(Map).ToArray(), useLog ? loLog : 0, useLog ? hiLog : maxV, ColVar, 1.4f);
        DrawSeries(g, r, t, rec.Delta.Take(n).Select(Map).ToArray(), useLog ? loLog : 0, useLog ? hiLog : maxV, ColDelta, 1.4f);
        DrawSeries(g, r, t, rec.Ptp.Take(n).Select(Map).ToArray(), useLog ? loLog : 0, useLog ? hiLog : maxV, ColPtp, 1.4f);

        DrawLegend(g, r, labelFont, new[] { ("Variance", ColVar), ("Delta", ColDelta), ("Peak-to-Peak", ColPtp) });
    }

    // -----------------------------------------------------------------
    // Panel 3: AP RSSI isi haritasi
    // -----------------------------------------------------------------
    private static void DrawApPanel(Graphics g, Rectangle r, TestRecording rec, int n,
        double duration, Font labelFont, Font smallFont, Brush textBrush)
    {
        g.DrawRectangle(new Pen(ColAxis), r);

        if (rec.ApHistory.Count == 0)
        {
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("AP verisi yok\n(WLAN API kapali olabilir)", labelFont, textBrush, r, sf);
            DrawXLabel(g, r, duration, labelFont, textBrush, "AP (BSSID)");
            return;
        }

        var bssids = rec.ApHistory.Keys.Take(8).ToList();
        int rows = bssids.Count;

        // Tum RSSI degerlerinden global min/max
        var allVals = rec.ApHistory.Take(8).SelectMany(kv => kv.Value).ToList();
        double vMin = allVals.DefaultIfEmpty(-90).Min();
        double vMax = allVals.DefaultIfEmpty(-30).Max();
        if (Math.Abs(vMax - vMin) < 1) { vMin -= 1; vMax += 1; }

        float cellH = (float)r.Height / rows;
        for (int row = 0; row < rows; row++)
        {
            var vals = rec.ApHistory[bssids[row]];
            int cols = Math.Min(vals.Count, n);
            if (cols == 0) continue;
            float cellW = (float)r.Width / cols;
            for (int c = 0; c < cols; c++)
            {
                double norm = (vals[c] - vMin) / (vMax - vMin);
                using var b = new SolidBrush(RdYlGnReversed(norm));
                g.FillRectangle(b, r.Left + c * cellW, r.Top + row * cellH, cellW + 1, cellH + 1);
            }
            // Satir etiketi (kisaltilmis BSSID)
            string shortB = ShortBssid(bssids[row]);
            g.DrawString(shortB, smallFont, textBrush, r.Left - MarginLeft + 4, r.Top + row * cellH + cellH / 2 - 6);
        }

        g.DrawRectangle(new Pen(ColAxis), r);
        // Y ekseni basligi
        DrawYTitle(g, r, labelFont, textBrush, "AP (BSSID)");
        DrawXLabel(g, r, duration, labelFont, textBrush, null);
    }

    // -----------------------------------------------------------------
    // Panel 4: RX/TX hizlari + sinyal %
    // -----------------------------------------------------------------
    private static void DrawRatesPanel(Graphics g, Rectangle r, double[] t, TestRecording rec, int n,
        double duration, Font labelFont, Brush textBrush)
    {
        var rx = rec.RxRate.Count >= n ? rec.RxRate.Take(n).ToArray() : Pad(rec.RxRate, n);
        var tx = rec.TxRate.Count >= n ? rec.TxRate.Take(n).ToArray() : Pad(rec.TxRate, n);
        double hi = rx.Concat(tx).DefaultIfEmpty(1).Max();
        if (hi <= 0) hi = 1;

        DrawAxes(g, r, 0, hi, duration, labelFont, textBrush, "Hiz (Mbps)");

        // Sinyal % dolgu (ikincil eksen, 0..100 olarak r yuksekligine olcekli)
        var pct = rec.RssiPct.Take(n).Select(v => (double)v).ToArray();
        if (pct.Length >= 2)
        {
            var pts = new List<PointF> { new(MapX(r, t[0], duration), r.Bottom) };
            for (int i = 0; i < pct.Length; i++)
                pts.Add(new PointF(MapX(r, t[i], duration), r.Bottom - (float)(Math.Clamp(pct[i], 0, 100) / 100.0 * r.Height)));
            pts.Add(new PointF(MapX(r, t[^1], duration), r.Bottom));
            using var fill = new SolidBrush(Color.FromArgb(30, 128, 128, 128));
            g.FillPolygon(fill, pts.ToArray());
        }

        DrawSeries(g, r, t, rx, 0, hi, ColRx, 1.4f);
        DrawSeries(g, r, t, tx, 0, hi, ColTx, 1.4f);
        DrawXLabel(g, r, duration, labelFont, textBrush, "Zaman (saniye)");
        DrawLegend(g, r, labelFont, new[] { ("RX (Mbps)", ColRx), ("TX (Mbps)", ColTx), ("Sinyal %", Color.Gray) });
    }

    // -----------------------------------------------------------------
    // Ortak cizim yardimcilari
    // -----------------------------------------------------------------
    private static void DrawAxes(Graphics g, Rectangle r, double lo, double hi, double duration,
        Font font, Brush textBrush, string yTitle)
    {
        using var axisPen = new Pen(ColAxis);
        using var gridPen = new Pen(ColGrid);
        g.DrawRectangle(axisPen, r);

        for (int i = 0; i <= 4; i++)
        {
            float y = r.Top + i * r.Height / 4f;
            g.DrawLine(gridPen, r.Left, y, r.Right, y);
            double val = hi - (hi - lo) * i / 4.0;
            g.DrawString(val.ToString("F1", CultureInfo.InvariantCulture), font, textBrush, 4, y - 7);
        }
        DrawYTitle(g, r, font, textBrush, yTitle);
    }

    private static void DrawXLabel(Graphics g, Rectangle r, double duration, Font font, Brush textBrush, string? caption)
    {
        for (int i = 0; i <= 5; i++)
        {
            double tv = duration * i / 5.0;
            float x = MapX(r, tv, duration);
            g.DrawString(tv.ToString("F0", CultureInfo.InvariantCulture), font, textBrush, x - 8, r.Bottom + 4);
        }
        if (caption is not null)
        {
            using var sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(caption, font, textBrush, new RectangleF(r.Left, r.Bottom + 20, r.Width, 16), sf);
        }
    }

    private static void DrawYTitle(Graphics g, Rectangle r, Font font, Brush textBrush, string title)
    {
        var state = g.Save();
        g.TranslateTransform(18, r.Top + r.Height / 2f);
        g.RotateTransform(-90);
        using var sf = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString(title, font, textBrush, 0, 0, sf);
        g.Restore(state);
    }

    private static void DrawSeries(Graphics g, Rectangle r, double[] t, double[] values,
        double lo, double hi, Color color, float width)
    {
        if (values.Length < 2)
            return;
        double span = hi - lo;
        if (span <= 0) span = 1;
        double duration = t[^1] <= 0 ? 1 : t[^1];

        var pts = new PointF[Math.Min(t.Length, values.Length)];
        for (int i = 0; i < pts.Length; i++)
        {
            float x = MapX(r, t[i], duration);
            float y = r.Bottom - (float)((values[i] - lo) / span * r.Height);
            y = Math.Clamp(y, r.Top, r.Bottom);
            pts[i] = new PointF(x, y);
        }
        using var pen = new Pen(color, width);
        g.DrawLines(pen, pts);
    }

    private static void DrawLegend(Graphics g, Rectangle r, Font font, (string Label, Color Color)[] items)
    {
        float x = r.Right - 150;
        float y = r.Top + 4;
        using var bg = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
        float h = items.Length * 16 + 6;
        g.FillRectangle(bg, x - 4, y - 2, 150, h);
        using var black = new SolidBrush(Color.Black);
        foreach (var (label, color) in items)
        {
            using var b = new SolidBrush(color);
            g.FillRectangle(b, x, y + 3, 14, 6);
            g.DrawString(label, font, black, x + 18, y - 1);
            y += 16;
        }
    }

    private static float MapX(Rectangle r, double tv, double duration) =>
        r.Left + (float)(tv / duration * r.Width);

    private static double[] Pad(List<double> src, int n)
    {
        var result = new double[n];
        for (int i = 0; i < n; i++)
            result[i] = i < src.Count ? src[i] : 0;
        return result;
    }

    /// <summary>RdYlGn_r benzeri renk: norm=0 → yesil, 0.5 → sari, 1 → kirmizi.</summary>
    private static Color RdYlGnReversed(double norm)
    {
        norm = Math.Clamp(norm, 0, 1);
        // 0 -> yesil (0,160,0), 0.5 -> sari (255,210,0), 1 -> kirmizi (200,0,0)
        if (norm < 0.5)
        {
            double k = norm / 0.5;
            return Color.FromArgb(
                (int)(0 + k * (255 - 0)),
                (int)(160 + k * (210 - 160)),
                0);
        }
        else
        {
            double k = (norm - 0.5) / 0.5;
            return Color.FromArgb(
                (int)(255 + k * (200 - 255)),
                (int)(210 + k * (0 - 210)),
                0);
        }
    }

    private static string ShortBssid(string bssid)
    {
        var parts = bssid.Split(':');
        string first = parts.Length > 0 ? parts[0] : bssid;
        string last = bssid.Length >= 2 ? bssid[^2..] : bssid;
        return first + ".." + last;
    }

    private static Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        int r = Convert.ToInt32(hex.Substring(0, 2), 16);
        int g = Convert.ToInt32(hex.Substring(2, 2), 16);
        int b = Convert.ToInt32(hex.Substring(4, 2), 16);
        return Color.FromArgb(r, g, b);
    }
}
