using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Implementasi layanan pencatatan aktivitas terpusat untuk aplikasi ROOT.
/// </summary>
public class LoggerService : ILoggerService
{
    private readonly SynchronizationContext? _syncContext;
    private readonly object _lock = new();

    public ObservableCollection<LogEntry> Logs { get; } = [];
    public event Action<LogEntry>? LogAdded;

    public LoggerService()
    {
        _syncContext = SynchronizationContext.Current;
    }

    public void Log(LogLevel level, string message, string? details = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Details = details
        };

        if (_syncContext != null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ => AddEntry(entry), null);
        }
        else
        {
            AddEntry(entry);
        }
    }

    private void AddEntry(LogEntry entry)
    {
        lock (_lock)
        {
            Logs.Add(entry);
        }

        try
        {
            LogAdded?.Invoke(entry);
        }
        catch
        {
            // Abaikan kegagalan pada listener log individu
        }
    }

    public void LogInfo(string message, string? details = null) => Log(LogLevel.Info, message, details);
    public void LogSuccess(string message, string? details = null) => Log(LogLevel.Sukses, message, details);
    public void LogWarning(string message, string? details = null) => Log(LogLevel.Peringatan, message, details);
    public void LogError(string message, string? details = null) => Log(LogLevel.Kesalahan, message, details);

    public void Clear()
    {
        if (_syncContext != null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ =>
            {
                lock (_lock) { Logs.Clear(); }
            }, null);
        }
        else
        {
            lock (_lock) { Logs.Clear(); }
        }
    }

    public string ExportAsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CATATAN AKTIVITAS ROOT ===");
        sb.AppendLine($"Waktu Ekspor: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        
        lock (_lock)
        {
            foreach (var log in Logs)
            {
                sb.AppendLine(log.ToString());
            }
        }
        
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"Total Entri: {Logs.Count}");
        return sb.ToString();
    }
}
