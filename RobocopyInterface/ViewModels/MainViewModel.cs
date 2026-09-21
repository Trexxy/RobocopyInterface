using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobocopyInterface.Models;
using RobocopyInterface.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RobocopyInterface.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RobocopyInterface", "settings.json");

    internal record SourceTargetRecord(string Source, string Target);
    internal record Settings(List<SourceTargetRecord> Entries);

    private readonly RobocopyRunner _runner;
    private readonly IFilePickerService _filePicker;
    private readonly StringBuilder _logBuilder = new();
    private CancellationTokenSource? _cts;
    private string _lastUsedTarget = string.Empty;

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

    public ObservableCollection<SourceTargetEntry> Sources { get; } = [];

    public MainViewModel(RobocopyRunner runner, IFilePickerService filePicker)
    {
        _runner = runner;
        _filePicker = filePicker;
        Sources.CollectionChanged += (_, _) => { StartSyncCommand.NotifyCanExecuteChanged(); SaveSettings(); };
        LoadSettings();
    }

    private void AddEntry(SourceTargetEntry entry)
    {
        entry.PropertyChanged += Entry_PropertyChanged;
        Sources.Add(entry);
    }

    private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SourceTargetEntry.Target)) return;
        if (sender is SourceTargetEntry entry)
            _lastUsedTarget = entry.Target;
        StartSyncCommand.NotifyCanExecuteChanged();
        SaveSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
            if (settings is null) return;
            foreach (var e in settings.Entries)
                AddEntry(new SourceTargetEntry(e.Source, e.Target));
            if (settings.Entries.Count > 0)
                _lastUsedTarget = settings.Entries[^1].Target;
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var entries = Sources.Select(s => new SourceTargetRecord(s.Source, s.Target)).ToList();
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Settings(entries)));
        }
        catch { }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _filePicker.PickFolderAsync("Select folder to sync");
        if (folder is not null)
            AddEntry(new SourceTargetEntry(folder, _lastUsedTarget));
    }

    [RelayCommand]
    private async Task AddFileAsync()
    {
        foreach (var file in await _filePicker.PickFilesAsync("Select files to sync"))
            AddEntry(new SourceTargetEntry(file, _lastUsedTarget));
    }

    [RelayCommand]
    private void RemoveSource(SourceTargetEntry entry)
    {
        entry.PropertyChanged -= Entry_PropertyChanged;
        Sources.Remove(entry);
    }

    [RelayCommand]
    private void ClearSources()
    {
        foreach (var entry in Sources)
            entry.PropertyChanged -= Entry_PropertyChanged;
        Sources.Clear();
    }

    [RelayCommand]
    private async Task BrowseTargetAsync(SourceTargetEntry entry)
    {
        var folder = await _filePicker.PickFolderAsync("Select target folder");
        if (folder is not null)
            entry.Target = folder;
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

        var sourcePaths = (IReadOnlyList<string>)[.. Sources.Select(s => s.Source)];
        var (totalFiles, totalBytes) = await Task.Run(() => CountTotalFilesAndBytes(sourcePaths));
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

        var pairs = (IReadOnlyList<(string Source, string Target)>)[.. Sources.Select(s => (s.Source, s.Target))];

        try
        {
            await _runner.RunAsync(
                pairs,
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
        !IsSyncing && Sources.Count > 0 && Sources.All(s => !string.IsNullOrWhiteSpace(s.Target));

    [RelayCommand]
    private void CancelSync() => _cts?.Cancel();

    private void AppendLog(string line)
    {
        _logBuilder.AppendLine(line);
        LogText = _logBuilder.ToString();
    }
}
