using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace WifiMotion.Core;

/// <summary>Tek bir RSSI okumasinin sonucu.</summary>
public readonly record struct RssiReading(int? Signal, int? Dbm, double? Rx, double? Tx);

/// <summary>
/// <c>netsh wlan show interfaces</c> ciktisini ayristirarak sinyal, RSSI ve veri
/// hizlarini okur. Python <c>lib/rssi_reader.py</c> modulunun portu.
/// Hem Turkce hem Ingilizce yerel ayar ciktisini destekler.
/// </summary>
public static class RssiReader
{
    private const int TimeoutMs = 5000;

    /// <summary>
    /// <c>netsh wlan show interfaces</c> calistirir; (sinyal%, RSSI dBm, alim Mbps,
    /// iletim Mbps) dondurur. Asla istisna firlatmaz; basarisizlikta hepsi null'dur.
    /// </summary>
    public static RssiReading GetRssi()
    {
        var (ok, stdout, _) = RunNetsh();
        if (!ok)
            return new RssiReading(null, null, null, null);

        int? sig = null, dbm = null;
        double? rx = null, tx = null;

        foreach (string line in stdout.Split('\n'))
        {
            string ll = line.ToLowerInvariant();
            int colon = line.IndexOf(':');
            string after = colon >= 0 && colon + 1 < line.Length ? line[(colon + 1)..] : "";

            if ((ll.Contains("signal") || ll.Contains("sinyal")) && line.Contains('%'))
            {
                if (TryExtractInt(after, out int v))
                    sig = v;
            }
            else if (ll.Contains("rssi") && !ll.Contains("bssid"))
            {
                if (TryExtractInt(after, out int v))
                    dbm = v;
            }
            else if (ll.Contains("receive rate") || ll.Contains("alım hızı") ||
                     (ll.Contains("al") && ll.Contains("mbps")))
            {
                if (TryExtractDouble(after, out double v))
                    rx = v;
            }
            else if (ll.Contains("transmit rate") || ll.Contains("iletim hızı") ||
                     (ll.Contains("ilet") && ll.Contains("mbps")))
            {
                if (TryExtractDouble(after, out double v))
                    tx = v;
            }
        }

        return new RssiReading(sig, dbm, rx, tx);
    }

    /// <summary>
    /// <c>netsh wlan</c> izin hatasi olmadan calistirilabilir mi kontrol eder.
    /// Python <c>can_read_rssi</c> karsiligi.
    /// </summary>
    public static bool CanReadRssi()
    {
        var (ok, stdout, stderr) = RunNetsh();
        if (!ok)
            return false;
        string outAll = (stdout + stderr).ToLowerInvariant();
        return !outAll.Contains("location permission") && !outAll.Contains("access is denied");
    }

    private static (bool Ok, string Stdout, string Stderr) RunNetsh()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return (false, "", "");

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(TimeoutMs))
            {
                try { proc.Kill(true); } catch { /* yoksay */ }
                return (false, "", "");
            }
            return (proc.ExitCode == 0, stdout, stderr);
        }
        catch (Exception)
        {
            return (false, "", "");
        }
    }

    /// <summary>Bir metin parcasindan ilk tam sayiyi (negatif olabilir) cikarir.</summary>
    private static bool TryExtractInt(string s, out int value)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (char.IsDigit(c) || (c == '-' && sb.Length == 0))
                sb.Append(c);
            else if (sb.Length > 0)
                break;
        }
        return int.TryParse(sb.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Bir metin parcasindan ilk ondalik sayiyi cikarir ('.' veya ',' destekli).</summary>
    private static bool TryExtractDouble(string s, out double value)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (char.IsDigit(c) || (c == '-' && sb.Length == 0))
                sb.Append(c);
            else if ((c == '.' || c == ',') && sb.Length > 0)
                sb.Append('.');
            else if (sb.Length > 0)
                break;
        }
        return double.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
