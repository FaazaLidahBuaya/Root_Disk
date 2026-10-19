using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Root.Core.Models;
using Root.Core.Mvvm;
using Root.Core.Services;
using Root.Core.Utilities;

namespace Root.App.ViewModels;

public class CleanupViewModel : ViewModelBase
{
    private readonly ICleanupAnalyzerService _analyzerService;
    private readonly ICleanupExecutionService _executionService;
    private readonly ILoggerService _logger;
    private readonly Func<string, string, bool>? _confirmAction;
    private readonly Action<string, string, bool>? _alertAction;
    private readonly Action<string>? _fileOpener;
    private readonly Action<string>? _clipboardAction;

    private FileSystemItemModel? _lastScannedRootItem;
    private CancellationTokenSource? _operationCts;

    private string _targetDirectoryPath = string.Empty;
    private bool _useCurrentScanData = true;
    private bool _isBusy;
    private bool _hasResults;
    private string _statusText = "Siap menganalisis kandidat pembersihan konservatif.";
    private string _currentProcessingFile = "-";
    private double _progressPercentage;
    private string _elapsedTime = "00:00";
    private CleanupPreviewModel? _previewModel;
    private CleanupExecutionResult? _executionResult;
    private CleanupCandidateModel? _selectedCandidate;

    public CleanupViewModel(
        ICleanupAnalyzerService analyzerService,
        ICleanupExecutionService executionService,
        ILoggerService logger,
        Func<string, string, bool>? confirmAction = null,
        Action<string, string, bool>? alertAction = null,
        Action<string>? fileOpener = null,
        Action<string>? clipboardAction = null)
    {
        _analyzerService = analyzerService;
        _executionService = executionService;
        _logger = logger;
        _confirmAction = confirmAction;
        _alertAction = alertAction;
        _fileOpener = fileOpener;
        _clipboardAction = clipboardAction;

        Candidates = [];

        MulaiAnalisisCommand = new AsyncRelayCommand(MulaiAnalisisAsync, () => !IsBusy && CanStartAnalysis);
        BatalOperasiCommand = new RelayCommand(BatalOperasi, () => IsBusy);
        PilihFolderCommand = new RelayCommand(PilihFolder, () => !IsBusy);
        PilihSemuaAmanCommand = new RelayCommand(PilihSemuaAman, () => HasResults && !IsBusy);
        BatalkanSemuaPilihanCommand = new RelayCommand(BatalkanSemuaPilihan, () => HasResults && !IsBusy);
        BersihkanTerpilihCommand = new AsyncRelayCommand(BersihkanTerpilihAsync, () => HasResults && !IsBusy && SelectedCount > 0);
        BukaLokasiBerkasCommand = new RelayCommand(param => BukaLokasiBerkas(param as string));
        SalinLaporanAuditCommand = new RelayCommand(SalinLaporanAudit, () => ExecutionResult != null);
    }

    public ObservableCollection<CleanupCandidateModel> Candidates { get; }

    public CleanupPreviewModel? PreviewModel
    {
        get => _previewModel;
        set
        {
            if (SetProperty(ref _previewModel, value))
            {
                HasResults = value != null && value.TotalCandidates > 0;
                UpdateSummaryProperties();
            }
        }
    }

    public CleanupExecutionResult? ExecutionResult
    {
        get => _executionResult;
        set
        {
            if (SetProperty(ref _executionResult, value))
            {
                SalinLaporanAuditCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public CleanupCandidateModel? SelectedCandidate
    {
        get => _selectedCandidate;
        set => SetProperty(ref _selectedCandidate, value);
    }

    public string TargetDirectoryPath
    {
        get => _targetDirectoryPath;
        set
        {
            if (SetProperty(ref _targetDirectoryPath, value))
            {
                OnPropertyChanged(nameof(CanStartAnalysis));
                MulaiAnalisisCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool UseCurrentScanData
    {
        get => _useCurrentScanData;
        set
        {
            if (SetProperty(ref _useCurrentScanData, value))
            {
                OnPropertyChanged(nameof(CanStartAnalysis));
                MulaiAnalisisCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasScanData => _lastScannedRootItem != null;

    public bool CanStartAnalysis => (UseCurrentScanData && HasScanData) ||
                                  (!UseCurrentScanData && !string.IsNullOrWhiteSpace(TargetDirectoryPath) && Directory.Exists(TargetDirectoryPath));

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                MulaiAnalisisCommand.RaiseCanExecuteChanged();
                BatalOperasiCommand.RaiseCanExecuteChanged();
                PilihFolderCommand.RaiseCanExecuteChanged();
                PilihSemuaAmanCommand.RaiseCanExecuteChanged();
                BatalkanSemuaPilihanCommand.RaiseCanExecuteChanged();
                BersihkanTerpilihCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasResults
    {
        get => _hasResults;
        set
        {
            if (SetProperty(ref _hasResults, value))
            {
                PilihSemuaAmanCommand.RaiseCanExecuteChanged();
                BatalkanSemuaPilihanCommand.RaiseCanExecuteChanged();
                BersihkanTerpilihCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CurrentProcessingFile
    {
        get => _currentProcessingFile;
        set => SetProperty(ref _currentProcessingFile, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public string ElapsedTime
    {
        get => _elapsedTime;
        set => SetProperty(ref _elapsedTime, value);
    }

    // Metrik Ringkasan untuk UI
    public int TotalCandidatesCount => Candidates.Count;
    public string FormattedTotalPotentialSize => FileFormatter.FormatBytes(Candidates.Sum(c => c.FileSizeBytes));
    public int SelectedCount => Candidates.Count(c => c.IsSelected && !c.IsProtected);
    public string FormattedSelectedFreedSpace => FileFormatter.FormatBytes(Candidates.Where(c => c.IsSelected && !c.IsProtected).Sum(c => c.FileSizeBytes));
    public int SafeCount => Candidates.Count(c => c.SafetyLevel == CleanupSafetyLevel.AmanKonservatif);
    public int NeedsCautionCount => Candidates.Count(c => c.SafetyLevel == CleanupSafetyLevel.PerluPerhatian);

    public AsyncRelayCommand MulaiAnalisisCommand { get; }
    public RelayCommand BatalOperasiCommand { get; }
    public RelayCommand PilihFolderCommand { get; }
    public RelayCommand PilihSemuaAmanCommand { get; }
    public RelayCommand BatalkanSemuaPilihanCommand { get; }
    public AsyncRelayCommand BersihkanTerpilihCommand { get; }
    public RelayCommand BukaLokasiBerkasCommand { get; }
    public RelayCommand SalinLaporanAuditCommand { get; }

    public void LoadScanData(FileSystemItemModel rootItem)
    {
        _lastScannedRootItem = rootItem;
        OnPropertyChanged(nameof(HasScanData));
        OnPropertyChanged(nameof(CanStartAnalysis));
        MulaiAnalisisCommand.RaiseCanExecuteChanged();

        if (UseCurrentScanData)
        {
            StatusText = $"Data pemindaian aktif dimuat ({rootItem.Name}). Siap menganalisis kandidat pembersihan.";
        }
    }

    private void UpdateSummaryProperties()
    {
        OnPropertyChanged(nameof(TotalCandidatesCount));
        OnPropertyChanged(nameof(FormattedTotalPotentialSize));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(FormattedSelectedFreedSpace));
        OnPropertyChanged(nameof(SafeCount));
        OnPropertyChanged(nameof(NeedsCautionCount));
        BersihkanTerpilihCommand.RaiseCanExecuteChanged();
    }

    public void NotifyCandidateSelectionChanged()
    {
        UpdateSummaryProperties();
    }

    public void PilihSemuaAman()
    {
        foreach (var item in Candidates)
        {
            if (item.SafetyLevel == CleanupSafetyLevel.AmanKonservatif && !item.IsProtected)
            {
                item.IsSelected = true;
            }
            else
            {
                item.IsSelected = false;
            }
        }
        UpdateSummaryProperties();
    }

    public void BatalkanSemuaPilihan()
    {
        foreach (var item in Candidates)
        {
            item.IsSelected = false;
        }
        UpdateSummaryProperties();
    }

    public async Task MulaiAnalisisAsync()
    {
        if (IsBusy) return;

        Candidates.Clear();
        ExecutionResult = null;
        PreviewModel = null;
        IsBusy = true;
        StatusText = "Menganalisis kandidat pembersihan...";
        ProgressPercentage = 0;
        CurrentProcessingFile = "-";

        _operationCts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        using var timer = new System.Timers.Timer(500);
        timer.Elapsed += (_, _) =>
        {
            ElapsedTime = $"{sw.Elapsed.Minutes:D2}:{sw.Elapsed.Seconds:D2}";
        };
        timer.Start();

        var progress = new Progress<CleanupProgressReport>(report =>
        {
            StatusText = report.Stage;
            CurrentProcessingFile = Path.GetFileName(report.CurrentFilePath);
            ProgressPercentage = report.ProgressPercentage;
        });

        try
        {
            CleanupPreviewModel preview;
            var options = new CleanupOptions();

            if (UseCurrentScanData && _lastScannedRootItem != null)
            {
                preview = await _analyzerService.AnalyzeFromScanTreeAsync(_lastScannedRootItem, options, progress, _operationCts.Token);
            }
            else
            {
                preview = await _analyzerService.AnalyzeDirectoryAsync(TargetDirectoryPath, options, progress, _operationCts.Token);
            }

            foreach (var candidate in preview.Candidates)
            {
                candidate.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CleanupCandidateModel.IsSelected))
                    {
                        NotifyCandidateSelectionChanged();
                    }
                };
                Candidates.Add(candidate);
            }

            PreviewModel = preview;
            StatusText = $"Analisis selesai. Ditemukan {Candidates.Count:N0} kandidat pembersihan konservatif.";
            _logger.LogSuccess($"Analisis kandidat pembersihan selesai dalam {sw.ElapsedMilliseconds} ms: {Candidates.Count:N0} berkas teridentifikasi.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analisis dibatalkan oleh pengguna.";
            _logger.LogWarning("Analisis kandidat pembersihan dibatalkan oleh pengguna.");
        }
        catch (Exception ex)
        {
            StatusText = $"Gagal melakukan analisis: {ex.Message}";
            _logger.LogError($"Kesalahan saat analisis pembersihan: {ex.Message}");
            ShowAlert($"Terjadi kesalahan saat menganalisis direktori:\n\n{ex.Message}", "Kesalahan Analisis", true);
        }
        finally
        {
            timer.Stop();
            sw.Stop();
            ElapsedTime = $"{sw.Elapsed.Minutes:D2}:{sw.Elapsed.Seconds:D2}";
            IsBusy = false;
            _operationCts?.Dispose();
            _operationCts = null;
        }
    }

    public async Task BersihkanTerpilihAsync()
    {
        if (IsBusy || SelectedCount == 0) return;

        var selectedItems = Candidates.Where(c => c.IsSelected && !c.IsProtected).ToList();
        if (selectedItems.Count == 0) return;

        // 1. Tampilkan Pratinjau & Minta Konfirmasi Eksplisit
        string confirmationMessage = 
            $"PRATINJAU TINDAKAN PEMBERSIHAN:\n\n" +
            $"• Jumlah Berkas Terpilih : {selectedItems.Count:N0} berkas\n" +
            $"• Total Ruang Dibebaskan : {FileFormatter.FormatBytes(selectedItems.Sum(c => c.FileSizeBytes))}\n\n" +
            $"PERINGATAN KEAMANAN & INTEGRITAS DATA:\n" +
            $"• Berkas yang terpilih akan dihapus secara permanen.\n" +
            $"• Sebelum penghapusan, setiap target akan diverifikasi ulang (re-validated).\n" +
            $"• Berkas yang berubah sejak analisis atau berada di folder sistem akan otomatis dilewati.\n\n" +
            $"Apakah Anda yakin ingin mengeksekusi pembersihan sekarang?";

        bool confirmed = _confirmAction != null
            ? _confirmAction(confirmationMessage, "Konfirmasi Pembersihan Berkas - ROOT")
            : (MessageBox.Show(
                confirmationMessage,
                "Konfirmasi Pembersihan Berkas - ROOT",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes);

        if (!confirmed)
        {
            _logger.LogInfo("Pembersihan dibatalkan oleh pengguna pada dialog konfirmasi eksplisit. Tidak ada berkas yang dihapus.");
            StatusText = "Pembersihan dibatalkan oleh pengguna. Tidak ada berkas yang dihapus.";
            return;
        }

        // 2. Eksekusi Pembersihan dengan Pre-Validation
        IsBusy = true;
        StatusText = "Mengeksekusi pembersihan dengan pemeriksaan ulang target...";
        ProgressPercentage = 0;

        _operationCts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        var progress = new Progress<CleanupProgressReport>(report =>
        {
            StatusText = report.Stage;
            CurrentProcessingFile = Path.GetFileName(report.CurrentFilePath);
            ProgressPercentage = report.ProgressPercentage;
        });

        try
        {
            var execResult = await _executionService.ExecuteCleanupAsync(selectedItems, progress, _operationCts.Token);
            ExecutionResult = execResult;

            // Perbarui daftar kandidat di antarmuka (hapus item yang berhasil dibersihkan)
            var deletedPaths = new HashSet<string>(
                execResult.ItemResults
                    .Where(r => r.Status == CleanupItemStatus.BerhasilDihapus)
                    .Select(r => r.FullPath),
                StringComparer.OrdinalIgnoreCase);

            for (int i = Candidates.Count - 1; i >= 0; i--)
            {
                if (deletedPaths.Contains(Candidates[i].FullPath))
                {
                    Candidates.RemoveAt(i);
                }
            }

            UpdateSummaryProperties();

            StatusText = $"Pembersihan selesai: {execResult.TotalDeletedCount:N0} berkas dihapus ({execResult.FormattedTotalFreedSpace} dibebaskan).";
            
            string summaryAlert = 
                $"HASIL AUDIT PEMBERSIHAN ROOT:\n\n" +
                $"• Berhasil Dihapus : {execResult.TotalDeletedCount:N0} berkas ({execResult.FormattedTotalFreedSpace})\n" +
                $"• Dilewati (Berkas Berubah) : {execResult.SkippedModifiedCount:N0} berkas\n" +
                $"• Dilewati (Sudah Hilang) : {execResult.SkippedMissingCount:N0} berkas\n" +
                $"• Ditolak (Proteksi Sistem) : {execResult.BlockedProtectedCount:N0} berkas\n" +
                $"• Gagal (Izin / Terkunci) : {execResult.FailedPermissionCount + execResult.FailedLockedCount + execResult.FailedOtherCount:N0} berkas\n\n" +
                $"Laporan audit lengkap telah dicatat di log sistem.";

            ShowAlert(summaryAlert, "Pembersihan Selesai - ROOT", false);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Pembersihan dihentikan oleh pembatalan pengguna.";
            _logger.LogWarning("Eksekusi pembersihan dibatalkan oleh pengguna.");
        }
        catch (Exception ex)
        {
            StatusText = $"Terjadi kesalahan saat pembersihan: {ex.Message}";
            _logger.LogError($"Kesalahan eksekusi pembersihan: {ex.Message}");
            ShowAlert($"Kesalahan saat pembersihan:\n\n{ex.Message}", "Kegagalan Pembersihan", true);
        }
        finally
        {
            sw.Stop();
            IsBusy = false;
            _operationCts?.Dispose();
            _operationCts = null;
        }
    }

    public void BatalOperasi()
    {
        if (_operationCts != null && !_operationCts.IsCancellationRequested)
        {
            _operationCts.Cancel();
            StatusText = "Membatalkan proses...";
        }
    }

    public void PilihFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder untuk Analisis Kandidat Pembersihan - ROOT"
        };

        if (dialog.ShowDialog() == true)
        {
            TargetDirectoryPath = dialog.FolderName;
            UseCurrentScanData = false;
            StatusText = $"Folder kustom dipilih: {TargetDirectoryPath}";
        }
    }

    public void BukaLokasiBerkas(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (_fileOpener != null)
        {
            _fileOpener(path);
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                ShowAlert($"Berkas sudah tidak ditemukan di lokasi:\n{path}", "Berkas Tidak Ditemukan", false);
            }
        }
        catch (Exception ex)
        {
            ShowAlert($"Gagal membuka penjelajah berkas: {ex.Message}", "Kesalahan Explorer", true);
        }
    }

    public void SalinLaporanAudit()
    {
        if (ExecutionResult == null) return;

        string report = ExecutionResult.GenerateAuditReportMarkdown();
        if (_clipboardAction != null)
        {
            _clipboardAction(report);
        }
        else
        {
            try
            {
                Clipboard.SetText(report);
            }
            catch (Exception ex)
            {
                ShowAlert($"Gagal menyalin laporan ke clipboard: {ex.Message}", "Kesalahan Clipboard", true);
                return;
            }
        }

        ShowAlert("Laporan audit pembersihan berhasil disalin ke Clipboard!", "Laporan Disalin", false);
    }

    private void ShowAlert(string message, string title, bool isError)
    {
        if (_alertAction != null)
        {
            _alertAction(message, title, isError);
        }
        else
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                isError ? MessageBoxImage.Error : MessageBoxImage.Information);
        }
    }
}
