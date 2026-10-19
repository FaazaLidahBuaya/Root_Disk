using System;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class LoggerServiceTests
{
    [Fact]
    public void LogInfo_AddsEntryWithInfoLevel()
    {
        var logger = new LoggerService();
        logger.LogInfo("Pesan informasi uji coba");

        Assert.Single(logger.Logs);
        Assert.Equal(LogLevel.Info, logger.Logs[0].Level);
        Assert.Equal("Pesan informasi uji coba", logger.Logs[0].Message);
    }

    [Fact]
    public void LogSuccess_AddsEntryWithSuksesLevel()
    {
        var logger = new LoggerService();
        logger.LogSuccess("Operasi berhasil diselesaikan");

        Assert.Single(logger.Logs);
        Assert.Equal(LogLevel.Sukses, logger.Logs[0].Level);
        Assert.Equal("SUKSES", logger.Logs[0].LevelText);
    }

    [Fact]
    public void LogWarning_AddsEntryWithPeringatanLevel()
    {
        var logger = new LoggerService();
        logger.LogWarning("Peringatan kapasitas hampir penuh");

        Assert.Single(logger.Logs);
        Assert.Equal(LogLevel.Peringatan, logger.Logs[0].Level);
        Assert.Equal("PERINGATAN", logger.Logs[0].LevelText);
    }

    [Fact]
    public void LogError_AddsEntryWithKesalahanLevel()
    {
        var logger = new LoggerService();
        logger.LogError("Terjadi kegagalan I/O", "Akses drive terputus");

        Assert.Single(logger.Logs);
        Assert.Equal(LogLevel.Kesalahan, logger.Logs[0].Level);
        Assert.Equal("KESALAHAN", logger.Logs[0].LevelText);
        Assert.Equal("Akses drive terputus", logger.Logs[0].Details);
    }

    [Fact]
    public void Clear_RemovesAllLogEntries()
    {
        var logger = new LoggerService();
        logger.LogInfo("Log 1");
        logger.LogWarning("Log 2");
        Assert.Equal(2, logger.Logs.Count);

        logger.Clear();
        Assert.Empty(logger.Logs);
    }

    [Fact]
    public void ExportAsText_ContainsExportHeaderAndMessages()
    {
        var logger = new LoggerService();
        logger.LogInfo("Pemindaian dimulai");
        logger.LogSuccess("Pemindaian selesai");

        var text = logger.ExportAsText();

        Assert.Contains("CATATAN AKTIVITAS ROOT", text);
        Assert.Contains("Pemindaian dimulai", text);
        Assert.Contains("Pemindaian selesai", text);
        Assert.Contains("Total Entri: 2", text);
    }

    [Fact]
    public void LogAdded_EventFiresWhenEntryIsAdded()
    {
        var logger = new LoggerService();
        LogEntry? captured = null;
        logger.LogAdded += entry => captured = entry;

        logger.LogInfo("Tes event");

        Assert.NotNull(captured);
        Assert.Equal("Tes event", captured?.Message);
    }
}
