using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Implementasi layanan pemeriksaan hak akses administrator dan peluncuran ulang aplikasi melalui UAC Windows.
/// </summary>
public class AdminService : IAdminService
{
    private readonly ILoggerService? _logger;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;
    private readonly Func<bool>? _adminChecker;
    private readonly Func<string?>? _processPathResolver;

    public AdminService(
        ILoggerService? logger = null,
        Func<ProcessStartInfo, Process?>? processStarter = null,
        Func<bool>? adminChecker = null,
        Func<string?>? processPathResolver = null)
    {
        _logger = logger;
        _processStarter = processStarter ?? (psi => Process.Start(psi));
        _adminChecker = adminChecker;
        _processPathResolver = processPathResolver;
    }

    /// <summary>
    /// Memeriksa apakah proses aplikasi saat ini memiliki hak administrator.
    /// </summary>
    public bool IsRunningAsAdmin()
    {
        if (_adminChecker != null)
        {
            return _adminChecker();
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Gagal memeriksa token hak administrator Windows: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Meluncurkan proses baru aplikasi dengan kata kerja 'runas' untuk meminta elevasi hak Administrator via UAC.
    /// Menangani pembatalan pengguna (Win32 Error 1223) dan kegagalan elevasi tanpa crash.
    /// </summary>
    public AdminElevationResult RestartAsAdmin(string? arguments = null)
    {
        string? targetPath = _processPathResolver != null
            ? _processPathResolver()
            : (Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName);

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            var msg = "Tidak dapat menentukan lokasi berkas eksekusi aplikasi untuk peluncuran ulang.";
            _logger?.LogError(msg);
            return AdminElevationResult.Failed(msg);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = targetPath,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            _logger?.LogInfo($"Meminta elevasi UAC Windows untuk berkas: '{targetPath}'");
            var process = _processStarter(startInfo);

            if (process == null)
            {
                var msg = "Sistem Windows tidak mengembalikan instans proses baru setelah permintaan elevasi.";
                _logger?.LogWarning(msg);
                return AdminElevationResult.Failed(msg);
            }

            _logger?.LogSuccess($"Proses dengan hak Administrator berhasil diluncurkan (PID: {process.Id}).");
            return AdminElevationResult.Succeeded();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            _logger?.LogWarning("Permintaan elevasi hak Administrator dibatalkan oleh pengguna pada dialog UAC.");
            return AdminElevationResult.Cancelled("Pengguna membatalkan permintaan elevasi pada dialog UAC Windows.");
        }
        catch (Win32Exception ex)
        {
            _logger?.LogError($"Kegagalan peluncuran elevasi UAC (Win32 Error {ex.NativeErrorCode}): {ex.Message}");
            return AdminElevationResult.Failed($"Gagal meluncurkan aplikasi sebagai Administrator: {ex.Message}", ex.NativeErrorCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Kesalahan tak terduga saat mencoba elevasi UAC: {ex.Message}");
            return AdminElevationResult.Failed($"Terjadi kesalahan sistem: {ex.Message}");
        }
    }
}
