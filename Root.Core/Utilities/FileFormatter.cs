using System;
using System.Globalization;

namespace Root.Core.Utilities;

/// <summary>
/// Utilitas pemformatan ukuran berkas, waktu, dan angka dalam bahasa Indonesia.
/// </summary>
public static class FileFormatter
{
    private static readonly string[] SizeSuffixes = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
    private static readonly CultureInfo IndonesianCulture = new("id-ID");

    /// <summary>
    /// Memformat ukuran bita (bytes) ke format yang mudah dibaca (mis. 1,25 MB).
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "-" + FormatBytes(-bytes);
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        int mag = (int)Math.Max(0, Math.Min(Math.Floor(Math.Log(bytes, 1024)), SizeSuffixes.Length - 1));
        double adjustedSize = bytes / Math.Pow(1024, mag);

        // Jika bita murni (< 1024), jangan gunakan desimal
        if (mag == 0)
        {
            return $"{bytes} B";
        }

        return string.Format(IndonesianCulture, "{0:N2} {1}", adjustedSize, SizeSuffixes[mag]);
    }

    /// <summary>
    /// Memformat durasi TimeSpan ke dalam string ringkas (mis. 02 menit 14 detik atau 450 ms).
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2} mnt";
        }

        if (duration.TotalSeconds >= 1)
        {
            return string.Format(IndonesianCulture, "{0:N1} dtk", duration.TotalSeconds);
        }

        return $"{duration.Milliseconds} ms";
    }

    /// <summary>
    /// Memformat angka desimal/bulat dengan pemisah ribuan titik khas Indonesia.
    /// </summary>
    public static string FormatNumber(long number)
    {
        return number.ToString("N0", IndonesianCulture);
    }

    /// <summary>
    /// Memformat kecepatan pemindaian berkas per detik.
    /// </summary>
    public static string FormatRate(double filesPerSecond, double bytesPerSecond)
    {
        return $"{FormatNumber((long)filesPerSecond)} berkas/dtk ({FormatBytes((long)bytesPerSecond)}/dtk)";
    }
}
