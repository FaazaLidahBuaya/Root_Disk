using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using Root.Core.Models;
using Root.Core.Mvvm;
using Root.Core.Services;

namespace Root.App.ViewModels;

public class ActivityLogViewModel : ViewModelBase
{
    private readonly ILoggerService _logger;
    private readonly ICollectionView _logsView;
    private LogLevel _selectedFilter = LogLevel.Semua;
    private string _searchQuery = string.Empty;

    public ActivityLogViewModel(ILoggerService logger)
    {
        _logger = logger;
        _logsView = CollectionViewSource.GetDefaultView(_logger.Logs);
        _logsView.Filter = FilterLogEntry;

        _logger.LogAdded += _ =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() => _logsView.Refresh());
        };

        BersihkanLogCommand = new RelayCommand(BersihkanLog);
        EksporLogCommand = new RelayCommand(EksporLog);
        SalinSemuaCommand = new RelayCommand(SalinSemua);
    }

    public ObservableCollection<LogEntry> AllLogs => _logger.Logs;
    public ICollectionView FilteredLogs => _logsView;

    public LogLevel SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                _logsView.Refresh();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _logsView.Refresh();
            }
        }
    }

    public RelayCommand BersihkanLogCommand { get; }
    public RelayCommand EksporLogCommand { get; }
    public RelayCommand SalinSemuaCommand { get; }

    private bool FilterLogEntry(object item)
    {
        if (item is not LogEntry entry)
            return false;

        if (SelectedFilter != LogLevel.Semua && entry.Level != SelectedFilter)
            return false;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            return entry.Message.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                   (entry.Details?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return true;
    }

    private void BersihkanLog()
    {
        _logger.Clear();
        _logsView.Refresh();
        _logger.LogInfo("Catatan aktivitas telah dibersihkan.");
    }

    private void EksporLog()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Ekspor Catatan Aktivitas ROOT",
            Filter = "Berkas Teks (*.txt)|*.txt|Semua Berkas (*.*)|*.*",
            FileName = $"ROOT_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var content = _logger.ExportAsText();
                System.IO.File.WriteAllText(dialog.FileName, content);
                _logger.LogSuccess($"Catatan aktivitas berhasil diekspor ke: '{dialog.FileName}'");
                MessageBox.Show("Catatan aktivitas berhasil diekspor!", "ROOT - Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Gagal mengekspor catatan aktivitas: {ex.Message}");
                MessageBox.Show($"Gagal mengekspor berkas: {ex.Message}", "ROOT - Kesalahan", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SalinSemuaCommandExecute()
    {
        try
        {
            var content = _logger.ExportAsText();
            Clipboard.SetText(content);
            MessageBox.Show("Semua catatan aktivitas disalin ke clipboard!", "ROOT - Informasi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Gagal menyalin catatan ke clipboard: {ex.Message}");
        }
    }

    private void SalinSemua() => SalinSemuaCommandExecute();
}
