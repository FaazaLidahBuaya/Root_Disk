using System;
using System.ComponentModel;
using System.Diagnostics;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class AdminServiceTests
{
    [Fact]
    public void IsRunningAsAdmin_WithMockChecker_ReturnsConfiguredValue()
    {
        var adminServiceTrue = new AdminService(adminChecker: () => true);
        Assert.True(adminServiceTrue.IsRunningAsAdmin());

        var adminServiceFalse = new AdminService(adminChecker: () => false);
        Assert.False(adminServiceFalse.IsRunningAsAdmin());
    }

    [Fact]
    public void IsRunningAsAdmin_DefaultWindowsCheck_DoesNotThrow()
    {
        var adminService = new AdminService();
        // Memastikan pengecekan token Windows bawaan tidak memicu unhandled exception
        var exception = Record.Exception(() => adminService.IsRunningAsAdmin());
        Assert.Null(exception);
    }

    [Fact]
    public void RestartAsAdmin_WhenElevationSucceeds_ReturnsSuccessResult()
    {
        ProcessStartInfo? capturedPsi = null;
        var dummyProcess = Process.GetCurrentProcess();

        var adminService = new AdminService(
            processStarter: psi =>
            {
                capturedPsi = psi;
                return dummyProcess;
            },
            processPathResolver: () => @"C:\Program Files\Root\Root.App.exe");

        var result = adminService.RestartAsAdmin("--test-arg");

        Assert.True(result.Success);
        Assert.False(result.CancelledByUser);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(capturedPsi);
        Assert.Equal(@"C:\Program Files\Root\Root.App.exe", capturedPsi.FileName);
        Assert.Equal("--test-arg", capturedPsi.Arguments);
        Assert.True(capturedPsi.UseShellExecute);
        Assert.Equal("runas", capturedPsi.Verb);
    }

    [Fact]
    public void RestartAsAdmin_WhenUserCancelsUacDialog_CatchesError1223_ReturnsCancelled()
    {
        // Kode Win32 1223 = ERROR_CANCELLED (The operation was canceled by the user)
        var win32CancelEx = new Win32Exception(1223);

        var adminService = new AdminService(
            processStarter: psi => throw win32CancelEx,
            processPathResolver: () => @"C:\Program Files\Root\Root.App.exe");

        var result = adminService.RestartAsAdmin();

        Assert.False(result.Success);
        Assert.True(result.CancelledByUser);
        Assert.Equal(1223, result.NativeErrorCode);
        Assert.Contains("UAC", result.ErrorMessage);
    }

    [Fact]
    public void RestartAsAdmin_WhenElevationFailsWithOtherWin32Code_ReturnsFailedWithCode()
    {
        // Kode Win32 5 = ERROR_ACCESS_DENIED
        var win32AccessDeniedEx = new Win32Exception(5);

        var adminService = new AdminService(
            processStarter: psi => throw win32AccessDeniedEx,
            processPathResolver: () => @"C:\Program Files\Root\Root.App.exe");

        var result = adminService.RestartAsAdmin();

        Assert.False(result.Success);
        Assert.False(result.CancelledByUser);
        Assert.Equal(5, result.NativeErrorCode);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void RestartAsAdmin_WhenGeneralExceptionOccurs_ReturnsFailedWithoutCrash()
    {
        var adminService = new AdminService(
            processStarter: psi => throw new InvalidOperationException("Gagal inisialisasi antrean sistem"),
            processPathResolver: () => @"C:\Program Files\Root\Root.App.exe");

        var result = adminService.RestartAsAdmin();

        Assert.False(result.Success);
        Assert.False(result.CancelledByUser);
        Assert.Contains("Gagal inisialisasi", result.ErrorMessage);
    }

    [Fact]
    public void RestartAsAdmin_WhenPathCannotBeResolved_ReturnsFailed()
    {
        var adminService = new AdminService(
            processPathResolver: () => null);

        var result = adminService.RestartAsAdmin();

        Assert.False(result.Success);
        Assert.False(result.CancelledByUser);
        Assert.Contains("lokasi berkas eksekusi", result.ErrorMessage);
    }

    [Fact]
    public void RestartAsAdmin_WhenStarterReturnsNullProcess_ReturnsFailed()
    {
        var adminService = new AdminService(
            processStarter: psi => null,
            processPathResolver: () => @"C:\Program Files\Root\Root.App.exe");

        var result = adminService.RestartAsAdmin();

        Assert.False(result.Success);
        Assert.False(result.CancelledByUser);
        Assert.Contains("tidak mengembalikan instans proses", result.ErrorMessage);
    }
}
