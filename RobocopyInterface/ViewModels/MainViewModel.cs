using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RobocopyInterface.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace RobocopyInterface.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly RobocopyRunner _runner;
    private readonly StringBuilder _logBuilder = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSyncCommand))]
    private string _destination = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSyncCommand))]
    private bool _isSyncing;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _overallProgressText = string.Empty;

    [ObservableProperty]
    private string _overallSizeText = string.Empty;

    [ObservableProperty]
    private double _currentFileProgress;

    [ObservableProperty]
    private string _copySpeed = string.Empty;

    [ObservableProperty]
    private string _logText = string.Empty;

    public ObservableCollection<string> Sources { get; } = [];

    public MainViewModel(RobocopyRunner runner)
    {
        _runner = runner;
        Sources.CollectionChanged += (_, _) => StartSyncCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select folder to sync" };
        if (dialog.ShowDialog() == true)
            Sources.Add(dialog.FolderName);
    }

    [RelayCommand]
    private void AddFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select files to sync",
            Multiselect = true,
        };
        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
                Sources.Add(file);
        }
    }

    [RelayCommand]
    private void RemoveSource(string path) => Sources.Remove(path);

    [RelayCommand]
    private void BrowseDestination()
    {
        var dialog = new OpenFolderDialog { Title = "Select destination folder" };
        if (dialog.ShowDialog() == true)
            Destination = dialog.FolderName;
    }

    [RelayCommand(CanExecute = nameof(CanStartSync))]
    private async Task StartSyncAsync()
    {
        IsSyncing = true;
        OverallProgress = 0;
        OverallProgressText = string.Empty;
        OverallSizeText = string.Empty;
        CurrentFileProgress = 0;
        CopySpeed = string.Empty;
        _logBuilder.Clear();
        LogText = string.Empty;
        _cts = new CancellationTokenSource();

        var sources = (IReadOnlyList<string>)[.. Sources];
        var (totalFiles, totalBytes) = await Task.Run(() => CountTotalFilesAndBytes(sources));
        OverallProgressText = $"0 / {totalFiles} files";
        OverallSizeText = $"0 B / {FormatSize(totalBytes)}";

        var logProgress       = new Progress<string>(AppendLog);
        var fileCountProgress = new Progress<(int filesDone, int filesTotal, long bytesDone, long bytesTotal)>(p =>
        {
            OverallProgress     = p.filesTotal > 0 ? p.filesDone / (double)p.filesTotal * 100 : 0;
            OverallProgressText = $"{p.filesDone} / {p.filesTotal} files";
            OverallSizeText     = $"{FormatSize(p.bytesDone)} / {FormatSize(p.bytesTotal)}";
        });
        var fileProgress  = new Progress<double>(value => CurrentFileProgress = value);
        var speedProgress = new Progress<string>(value => CopySpeed = value);

        try
        {
            await _runner.RunAsync(
                sources,
                Destination,
                totalFiles,
                totalBytes,
                logProgress,
                fileCountProgress,
                fileProgress,
                speedProgress,
                _cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog("--- Sync cancelled ---");
        }
        finally
        {
            IsSyncing = false;
            CurrentFileProgress = 0;
            CopySpeed = string.Empty;
            _cts.Dispose();
            _cts = null;
        }
    }

    private static (int files, long bytes) CountTotalFilesAndBytes(IReadOnlyList<string> sources)
    {
        int files = 0;
        long bytes = 0;
        foreach (var source in sources)
        {
            if (File.Exists(source))
            {
                files++;
                bytes += new FileInfo(source).Length;
            }
            else if (Directory.Exists(source))
            {
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    files++;
                    try { bytes += new FileInfo(file).Length; } catch { }
                }
            }
        }
        return (files, bytes);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024 * 1024):F1} TB",
        >= 1024L * 1024 * 1024        => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
        >= 1024L * 1024               => $"{bytes / (1024.0 * 1024):F1} MB",
        >= 1024L                      => $"{bytes / 1024.0:F1} KB",
        _                             => $"{bytes} B",
    };

    private bool CanStartSync() =>
        !IsSyncing && Sources.Count > 0 && !string.IsNullOrWhiteSpace(Destination);

    [RelayCommand]
    private void CancelSync() => _cts?.Cancel();

    private void AppendLog(string line)
    {
        _logBuilder.AppendLine(line);
        LogText = _logBuilder.ToString();
    }
}
