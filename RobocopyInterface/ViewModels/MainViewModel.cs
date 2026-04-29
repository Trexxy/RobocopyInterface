using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RobocopyHelper.Services;
using System.Collections.ObjectModel;
using System.Text;

namespace RobocopyHelper.ViewModels;

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
        CurrentFileProgress = 0;
        CopySpeed = string.Empty;
        _logBuilder.Clear();
        LogText = string.Empty;
        _cts = new CancellationTokenSource();

        var logProgress     = new Progress<string>(AppendLog);
        var overallProgress = new Progress<double>(value => OverallProgress = value);
        var fileProgress    = new Progress<double>(value => CurrentFileProgress = value);
        var speedProgress   = new Progress<string>(value => CopySpeed = value);

        try
        {
            await _runner.RunAsync(
                [.. Sources],
                Destination,
                logProgress,
                overallProgress,
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
