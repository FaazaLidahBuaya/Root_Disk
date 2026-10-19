using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Root.Core.Services;

/// <summary>
/// Implementasi perlindungan direktori dan berkas sistem operasi Windows.
/// </summary>
public class SystemProtectionService : ISystemProtectionService
{
    private readonly HashSet<string> _protectedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _protectedRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin",
        "$RECYCLE.BIN",
        "System Volume Information",
        "Recovery",
        "Boot",
        "Config.Msi",
        "bootmgr",
        "BOOTNXT",
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys",
        "dumpstack.log"
    };

    private readonly HashSet<string> _protectedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sys", ".dll", ".exe", ".msi", ".drv", ".ocx", ".cpl", ".com",
        ".bat", ".cmd", ".ps1", ".vbs", ".reg", ".msc", ".inf", ".cat"
    };

    private readonly HashSet<string> _protectedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NTUSER.DAT",
        "UsrClass.dat",
        "bootmgr",
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys"
    };

    public SystemProtectionService(IEnumerable<string>? additionalProtectedPaths = null)
    {
        // Daftarkan folder sistem Windows standar
        RegisterProtectedFolder(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        RegisterProtectedFolder(Environment.GetFolderPath(Environment.SpecialFolder.System));
        RegisterProtectedFolder(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
        RegisterProtectedFolder(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        RegisterProtectedFolder(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        RegisterProtectedFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles));
        RegisterProtectedFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86));

        // Daftarkan folder sistem tambahan yang umum di Windows
        string windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        RegisterProtectedFolder(windir);
        RegisterProtectedFolder(Path.Combine(windir, "System32"));
        RegisterProtectedFolder(Path.Combine(windir, "SysWOW64"));
        RegisterProtectedFolder(Path.Combine(windir, "WinSxS"));
        RegisterProtectedFolder(Path.Combine(windir, "Boot"));

        string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        RegisterProtectedFolder(Path.Combine(systemDrive, @"Program Files"));
        RegisterProtectedFolder(Path.Combine(systemDrive, @"Program Files (x86)"));
        RegisterProtectedFolder(Path.Combine(systemDrive, @"ProgramData\Microsoft\Windows"));

        if (additionalProtectedPaths != null)
        {
            foreach (var path in additionalProtectedPaths)
            {
                RegisterProtectedFolder(path);
            }
        }
    }

    private void RegisterProtectedFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string normalized = NormalizePath(path);
            _protectedDirectories.Add(normalized);
        }
        catch
        {
            // Abaikan kegagalan normalisasi jalur yang tidak valid
        }
    }

    private static string NormalizePath(string path)
    {
        string full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public bool IsProtectedPath(string path)
    {
        return GetProtectionReason(path) != null;
    }

    public bool IsProtectedExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string ext = Path.GetExtension(path);
        return _protectedExtensions.Contains(ext);
    }

    public string? GetProtectionReason(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Jalur kosong atau tidak valid.";
        }

        try
        {
            string normalized = NormalizePath(path);
            string fileName = Path.GetFileName(normalized);

            // Cek nama berkas sistem sensitif
            if (!string.IsNullOrEmpty(fileName))
            {
                if (_protectedFileNames.Contains(fileName) || fileName.StartsWith("NTUSER.DAT", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("UsrClass.dat", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Berkas sistem atau hive registri sensitif ({fileName}).";
                }

                if (_protectedRootNames.Contains(fileName))
                {
                    return $"Komponen root sistem operasi Windows ({fileName}).";
                }
            }

            // Cek apakah ekstensi berkas adalah komponen eksekutabel/driver sistem
            if (IsProtectedExtension(normalized))
            {
                return $"Ekstensi berkas ({Path.GetExtension(normalized)}) merupakan komponen eksekutabel atau biner sistem.";
            }

            // Cek apakah berkas atau folder berada di dalam direktori sistem yang terproteksi
            foreach (var protectedDir in _protectedDirectories)
            {
                if (normalized.Equals(protectedDir, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Jalur sama persis dengan direktori sistem terlindungi ({protectedDir}).";
                }

                string prefix = protectedDir + Path.DirectorySeparatorChar;
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Jalur berada di dalam direktori sistem terlindungi ({protectedDir}).";
                }
            }

            // Cek apakah berada di root folder sistem khusus seperti System Volume Information atau $Recycle.Bin
            string[] parts = normalized.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                // Bagian kedua setelah drive letter (misal C:\System Volume Information)
                string rootFolder = parts[1];
                if (_protectedRootNames.Contains(rootFolder))
                {
                    return $"Jalur berada di dalam folder sistem root ({rootFolder}).";
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Jalur tidak dapat divalidasi ({ex.Message}) sehingga dilindungi demi keamanan.";
        }
    }
}
