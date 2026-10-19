using System;
using System.Collections.ObjectModel;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Antarmuka layanan pencatatan aktivitas (logging).
/// </summary>
public interface ILoggerService
{
    ObservableCollection<LogEntry> Logs { get; }
    event Action<LogEntry>? LogAdded;

    void Log(LogLevel level, string message, string? details = null);
    void LogInfo(string message, string? details = null);
    void LogSuccess(string message, string? details = null);
    void LogWarning(string message, string? details = null);
    void LogError(string message, string? details = null);

    void Clear();
    string ExportAsText();
}
