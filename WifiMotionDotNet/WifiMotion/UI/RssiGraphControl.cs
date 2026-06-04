using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace WifiMotion.UI;

/// <summary>
/// Son RSSI okumalarini canli bir cizgi/alan grafigi olarak cizen ozel kontrol.
/// Python <c>_rssi_graph</c> (terminal sparkline) fonksiyonunun WinForms karsiligi.
/// </summary>
public sealed class RssiGraphControl : Control
{
    private double[] _data = Array.Empty<double>();
    private int _lastDbm;
    private int _baseline;
    private int _graphWidth = 58;

    public RssiGraphControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(18, 22, 28);
        ForeColor = Color.Gainsboro;
    }

    /// <summary>Grafik verisini gunceller ve yeniden cizdirir.</summary>
    public void UpdateData(IReadOnlyList<double> history, int lastDbm, int baseline, int graphWidth)
    {
        _data = history.ToArray();
        _lastDbm = lastDbm;
        _baseline = baseline;
        _graphWidth = Math.Max(8, graphWidth);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var rect = new Rectangle(46, 8, Math.Max(10, Width - 56), Math.Max(10, Height - 28));

        using var gridPen = new Pen(Color.FromArgb(45, 200, 200, 200));
        using var axisPen = new Pen(Color.FromArgb(90, 200, 200, 200));
        using var labelFont = new Font("Consolas", 8f);
        using var labelBrush = new SolidBrush(Color.FromArgb(160, 220, 220, 220));

        // Veri yoksa cerceve ciz ve cik
        var vals = _data.Where(v => v != 0 || true).ToArray();
        double lo, hi;
        if (_data.Length > 0)
        {
            lo = _data.Min() - 2;
            hi = _data.Max() + 2;
        }
        else
        {
            lo = -80; hi = -40;
        }
        double span = hi - lo;
        if (span < 8)
        {
            double mid = (hi + lo) / 2;
            lo = mid - 4; hi = mid + 4;
            span = hi - lo;
        }

        // Yatay izgara + dBm etiketleri
        for (int i = 0; i <= 4; i++)
        {
            float y = rect.Top + i * rect.Height / 4f;
            g.DrawLine(gridPen, rect.Left, y, rect.Right, y);
            double dbmAt = hi - span * i / 4.0;
            g.DrawString($"{Math.Round(dbmAt),4:0}", labelFont, labelBrush, 2, y - 7);
        }
        g.DrawRectangle(axisPen, rect);

        if (_data.Length < 2)
        {
            using var waitBrush = new SolidBrush(Color.FromArgb(120, 220, 220, 220));
            g.DrawString("Veri bekleniyor...", labelFont, waitBrush, rect.Left + 8, rect.Top + 8);
            return;
        }

        // Veriyi son graphWidth degerine kirp ve x ekseni boyunca yerlestir
        var data = _data.Length > _graphWidth
            ? _data.Skip(_data.Length - _graphWidth).ToArray()
            : _data;

        var pts = new PointF[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            float x = rect.Left + (data.Length == 1 ? 0 : (float)i / (data.Length - 1) * rect.Width);
            float y = rect.Bottom - (float)((data[i] - lo) / span * rect.Height);
            y = Math.Clamp(y, rect.Top, rect.Bottom);
            pts[i] = new PointF(x, y);
        }

        // Taban (baseline) cizgisi
        if (_baseline != 0 && _baseline >= lo && _baseline <= hi)
        {
            float by = rect.Bottom - (float)((_baseline - lo) / span * rect.Height);
            using var basePen = new Pen(Color.FromArgb(120, 255, 170, 60)) { DashStyle = DashStyle.Dash };
            g.DrawLine(basePen, rect.Left, by, rect.Right, by);
        }

        // Alan dolgusu
        var fillPts = new List<PointF> { new(pts[0].X, rect.Bottom) };
        fillPts.AddRange(pts);
        fillPts.Add(new PointF(pts[^1].X, rect.Bottom));
        using (var fill = new SolidBrush(Color.FromArgb(40, 0, 200, 255)))
            g.FillPolygon(fill, fillPts.ToArray());

        // Cizgi
        using (var linePen = new Pen(Color.FromArgb(0, 210, 255), 1.6f))
            g.DrawLines(linePen, pts);

        // Son nokta vurgusu
        var last = pts[^1];
        using (var dotBrush = new SolidBrush(Color.FromArgb(0, 230, 255)))
            g.FillEllipse(dotBrush, last.X - 3, last.Y - 3, 6, 6);

        // Son dBm etiketi
        using var valFont = new Font("Consolas", 8.5f, FontStyle.Bold);
        using var valBrush = new SolidBrush(Color.FromArgb(0, 230, 255));
        g.DrawString($"{_lastDbm} dBm", valFont, valBrush,
            Math.Min(last.X + 6, rect.Right - 60), Math.Clamp(last.Y - 8, rect.Top, rect.Bottom - 14));
    }
}
