using System.Collections.Generic;

namespace Root.Core.Models;

/// <summary>
/// Opsi preset batas ukuran berkas untuk kontrol filter antarmuka pengguna.
/// </summary>
public class SizeFilterOption
{
    public string Label { get; set; } = string.Empty;
    public long Bytes { get; set; }

    public override string ToString() => Label;

    public static IReadOnlyList<SizeFilterOption> GetDefaultPresets()
    {
        return
        [
            new SizeFilterOption { Label = "Semua Ukuran", Bytes = 0 },
            new SizeFilterOption { Label = "> 1 MB", Bytes = 1L * 1024L * 1024L },
            new SizeFilterOption { Label = "> 10 MB", Bytes = 10L * 1024L * 1024L },
            new SizeFilterOption { Label = "> 50 MB", Bytes = 50L * 1024L * 1024L },
            new SizeFilterOption { Label = "> 100 MB", Bytes = 100L * 1024L * 1024L },
            new SizeFilterOption { Label = "> 500 MB", Bytes = 500L * 1024L * 1024L },
            new SizeFilterOption { Label = "> 1 GB", Bytes = 1024L * 1024L * 1024L },
            new SizeFilterOption { Label = "> 5 GB", Bytes = 5L * 1024L * 1024L * 1024L }
        ];
    }
}
