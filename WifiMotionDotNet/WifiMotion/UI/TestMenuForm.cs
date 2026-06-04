using System;
using System.Drawing;
using System.Windows.Forms;
using WifiMotion.Testing;

namespace WifiMotion.UI;

/// <summary>
/// Uzman testlerini listeleyen secim diyalogu. Python <c>show_test_menu</c> karsiligi.
/// </summary>
public sealed class TestMenuForm : Form
{
    private readonly ListBox _list;
    public TestDefinition? Selected { get; private set; }

    public TestMenuForm()
    {
        Text = "Test Menusu — Uzman Testleri";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 380);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = new Font("Segoe UI", 9.5F);

        var lbl = new Label
        {
            Text = "Bir uzman testi secin:",
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(8, 6, 0, 0),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        };

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5F),
            IntegralHeight = false,
        };
        foreach (var t in Tests.All)
            _list.Items.Add($"[{t.Key.ToUpperInvariant()}]  {t.Name,-26} {t.Duration,3}sn  {t.Desc}");
        _list.DoubleClick += (_, _) => Accept();

        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        var btnOk = new Button { Text = "Basla", Width = 90, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Iptal", Width = 90, DialogResult = DialogResult.Cancel };
        btnOk.Click += (_, _) => Accept();
        pnl.Controls.Add(btnOk);
        pnl.Controls.Add(btnCancel);

        Controls.Add(_list);
        Controls.Add(pnl);
        Controls.Add(lbl);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        _list.SelectedIndex = 0;
    }

    private void Accept()
    {
        int i = _list.SelectedIndex;
        if (i >= 0 && i < Tests.All.Count)
        {
            Selected = Tests.All[i];
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
