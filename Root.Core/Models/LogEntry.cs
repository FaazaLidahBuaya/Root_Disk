using System;

namespace Root.Core.Models;

/// <summary>
/// Model entitas catatan aktivitas (activity log).
/// </summary>
public class LogEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }

    public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");
    public string LevelText => Level switch
    {
        LogLevel.Info => "INFO",
        LogLevel.Sukses => "SUKSES",
        LogLevel.Peringatan => "PERINGATAN",
        LogLevel.Kesalahan => "KESALAHAN",
        _ => Level.ToString().ToUpperInvariant()
    };

    public override string ToString()
    {
        return $"[{FormattedTime}] [{LevelText}] {Message}" + (string.IsNullOrWhiteSpace(Details) ? "" : $" -> {Details}");
    }
}
