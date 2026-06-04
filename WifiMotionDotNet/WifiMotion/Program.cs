using System;
using System.Windows.Forms;
using WifiMotion.UI;

namespace WifiMotion;

internal static class Program
{
    /// <summary>
    /// Uygulamanin giris noktasi. Python'daki <c>wifi_motion.main()</c> karsiligi.
    /// Tek-ornek (single-instance) kontrolu yapar ve ana formu acar.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // Ayni anda yalnizca bir ornek calissin (Python PID dosyasi mantiginin karsiligi).
        using var mutex = new System.Threading.Mutex(true, "WifiMotionCli_SingleInstance", out bool isNew);
        if (!isNew)
        {
            var r = MessageBox.Show(
                "WiFi Motion zaten calisiyor.\nYine de yeni bir ornek acmak istiyor musunuz?",
                "WiFi Motion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (r != DialogResult.Yes)
                return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
