using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RobocopyInterface.Services;

public class RobocopyRunner
{
    // Robocopy writes per-file percentage as "\r  47.3%\r  47.4%..." without newlines.
    // We split the raw stream on both \r and \n so we catch each update.
    private static readonly Regex PercentLine =
        new(@"^\s*(\d+(?:\.\d+)?)%\s*$", RegexOptions.Compiled);

    // Matches file announcement lines so we can extract the size for speed calculation.
    // Example line: "          New File               1,073,741,824	bigfile.bin"
    // The size field may include thousand-separators and an optional unit suffix (k/m/g/t).
    private static readonly Regex FileSizeLine =
        new(@"\b(?:New File|Newer|Older|Changed)\b\s+(\d[\d,]*(?:\.\d+)?)\s*([kKmMgGtT]?)\b",
            RegexOptions.Compiled);

    // Matches the Robocopy summary "Files :" line to extract the copied and skipped counts.
    // Example: "   Files :         7         3         4         0         0         0"
    // Groups: 1=Copied, 2=Skipped
    private static readonly Regex FilesSummaryLine =
        new(@"^\s*Files\s*:\s*\d[\d,]*\s+(\d[\d,]*)\s+(\d[\d,]*)", RegexOptions.Compiled);

    // Matches the Robocopy summary "Bytes :" line header.
    // Example: "   Bytes :   1.073 g  230.5 m  794.0 m         0         0         0"
    private static readonly Regex BytesSummaryLine =
        new(@"^\s*Bytes\s*:", RegexOptions.Compiled);

    // Tokenises individual numeric fields in a summary line.
    // Each field is a number (with optional thousand-separators/decimal point)
    // followed optionally by a space and a single unit letter (k/m/g/t).
    private static readonly Regex SummaryValueToken =
        new(@"(\d[\d,.]*)\s*([kKmMgGtT]?)", RegexOptions.Compiled);

    // How long to keep samples for the rolling average.
    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(3);
    // Minimum time between speed updates pushed to the UI.
    private static readonly TimeSpan SpeedReportInterval = TimeSpan.FromSeconds(2);

    // Per-file state for speed calculation. Reset on each new file announcement.
    private long _currentFileBytes;
    private double _lastPercent;
    private DateTime _lastPercentTime;
    private DateTime _lastSpeedReport = DateTime.MinValue;
    // Each entry is (timestamp, bytes transferred in that sample).
    private readonly Queue<(DateTime Time, double Bytes)> _speedSamples = new();

    // File-count and byte-size tracking for the overall progress indicator.
    private int _totalFiles;
    private long _totalBytes;
    private int _runningFilesDone;        // Cumulative across all completed sources
    private long _runningBytesTransferred;
    private int _sourceFilesDone;         // Files announced (being copied) in the current source
    private long _sourceBytesTransferred;
    private int _sourceSkippedFiles;      // From summary: files already up-to-date
    private long _sourceSkippedBytes;
    private IProgress<(int filesDone, int filesTotal, long bytesDone, long bytesTotal)>? _fileCountProgress;

    public async Task RunAsync(
        IReadOnlyList<string> sources,
        string destination,
        int totalFiles,
        long totalBytes,
        IProgress<string> logProgress,
        IProgress<(int filesDone, int filesTotal, long bytesDone, long bytesTotal)> fileCountProgress,
        IProgress<double> fileProgress,
        IProgress<string> speedProgress,
        CancellationToken ct)
    {
        _totalFiles = totalFiles;
        _totalBytes = totalBytes;
        _runningFilesDone = 0;
        _runningBytesTransferred = 0;
        _fileCountProgress = fileCountProgress;

        fileCountProgress.Report((0, totalFiles, 0, totalBytes));
        fileProgress.Report(0);
        speedProgress.Report(string.Empty);

        for (int i = 0; i < sources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var source = sources[i];

            if (!Path.Exists(source))
            {
                logProgress.Report($"[SKIP] Source not found: {source}");
                continue;
            }

            string srcDir, destDir;
            string? fileFilter = null;

            if (File.Exists(source))
            {
                // Single file: copy from its parent directory, filtered to just this file.
                srcDir = Path.GetDirectoryName(source)!;
                fileFilter = Path.GetFileName(source);
                destDir = destination;
            }
            else
            {
                // Folder: mirror into a same-named subfolder inside destination.
                srcDir = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                destDir = Path.Combine(destination, Path.GetFileName(srcDir));
            }

            var args = BuildArgs(srcDir, destDir, fileFilter);

            logProgress.Report($"--- Syncing: {source} ---");
            fileProgress.Report(0);
            speedProgress.Report(string.Empty);
            _currentFileBytes = 0;
            _sourceFilesDone = 0;
            _sourceBytesTransferred = 0;
            _sourceSkippedFiles = 0;
            _sourceSkippedBytes = 0;

            await RunProcessAsync(args, logProgress, fileProgress, speedProgress, ct);

            // After the process drains, fold in skipped (already-up-to-date) counts from the summary.
            _runningFilesDone += _sourceFilesDone + _sourceSkippedFiles;
            _runningBytesTransferred += _sourceBytesTransferred + _sourceSkippedBytes;
            fileCountProgress.Report((_runningFilesDone, totalFiles, _runningBytesTransferred, totalBytes));
            fileProgress.Report(0);
        }

        speedProgress.Report(string.Empty);
        logProgress.Report("--- All done ---");
    }

    private static string BuildArgs(string sourceDir, string destDir, string? fileFilter)
    {
        // /E      — recurse subdirectories including empty ones
        // /COPY:D — copy data only (no timestamps, security, owner, auditing)
        // /R:1    — retry once on failure
        // /W:1    — wait 1 second between retries
        // /NDL    — suppress directory listing lines to reduce noise
        // Note: /NP is intentionally omitted so robocopy emits per-file percentages,
        //       which we parse by splitting the raw stream on \r as well as \n.
        var args = $"\"{sourceDir}\" \"{destDir}\"";
        if (fileFilter is not null)
            args += $" \"{fileFilter}\"";
        args += " /E /COPY:D /R:1 /W:1 /NDL";
        return args;
    }

    private async Task RunProcessAsync(
        string args,
        IProgress<string> logProgress,
        IProgress<double> fileProgress,
        IProgress<string> speedProgress,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo("robocopy", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Read both streams concurrently to prevent either buffer from filling
        // and deadlocking the process while we wait on the other.
        var readOutput = ReadStreamAsync(process.StandardOutput, logProgress, fileProgress, speedProgress);
        var readError  = ReadStreamAsync(process.StandardError,  logProgress, fileProgress: null, speedProgress: null);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        finally
        {
            // Drain any buffered output that arrived before the process exited.
            await Task.WhenAll(readOutput, readError);
        }
    }

    private async Task ReadStreamAsync(
        StreamReader reader,
        IProgress<string> logProgress,
        IProgress<double>? fileProgress,
        IProgress<string>? speedProgress)
    {
        var buffer = new char[4096];
        var lineBuffer = new StringBuilder();

        int charsRead;
        while ((charsRead = await reader.ReadAsync(buffer)) > 0)
        {
            for (int i = 0; i < charsRead; i++)
            {
                char c = buffer[i];
                if (c is '\r' or '\n')
                    FlushLine(lineBuffer, logProgress, fileProgress, speedProgress);
                else
                    lineBuffer.Append(c);
            }
        }

        // Flush anything left in the buffer that wasn't terminated by a newline.
        FlushLine(lineBuffer, logProgress, fileProgress, speedProgress);
    }

    private void FlushLine(
        StringBuilder lineBuffer,
        IProgress<string> logProgress,
        IProgress<double>? fileProgress,
        IProgress<string>? speedProgress)
    {
        if (lineBuffer.Length == 0) return;

        var line = lineBuffer.ToString();
        lineBuffer.Clear();

        // Per-file percentage update — route to the progress bar, not the log.
        if (fileProgress is not null)
        {
            var percentMatch = PercentLine.Match(line);
            if (percentMatch.Success)
            {
                var pct = double.Parse(percentMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                fileProgress.Report(pct);
                var speed = ComputeSpeedText(pct);
                if (speed is not null)
                    speedProgress?.Report(speed);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(line)) return;

        // File announcement line — reset all speed state and parse the new file's size.
        var sizeMatch = FileSizeLine.Match(line);
        if (sizeMatch.Success)
        {
            _currentFileBytes = ParseFileBytes(sizeMatch.Groups[1].Value, sizeMatch.Groups[2].Value);
            _lastPercent = 0;
            _lastPercentTime = DateTime.UtcNow;
            _lastSpeedReport = DateTime.MinValue;
            _speedSamples.Clear();
            // Clear the displayed speed immediately when a new file starts.
            speedProgress?.Report(string.Empty);

            // Count this file, accumulate its size, and report live progress.
            _sourceBytesTransferred += _currentFileBytes;
            _sourceFilesDone++;
            if (_totalFiles > 0)
                _fileCountProgress?.Report((
                    _runningFilesDone + _sourceFilesDone,
                    _totalFiles,
                    _runningBytesTransferred + _sourceBytesTransferred,
                    _totalBytes));
        }
        else
        {
            // Robocopy summary "Files :" — capture skipped file count.
            var filesSummary = FilesSummaryLine.Match(line);
            if (filesSummary.Success)
            {
                _sourceSkippedFiles = int.Parse(filesSummary.Groups[2].Value.Replace(",", ""), CultureInfo.InvariantCulture);
            }
            // Robocopy summary "Bytes :" — capture skipped byte count.
            // Fields: Total, Copied, Skipped, Mismatch, FAILED, Extras (indices 0–5).
            else if (BytesSummaryLine.IsMatch(line))
            {
                var colonIdx = line.IndexOf(':');
                var tokens = SummaryValueToken.Matches(line, colonIdx + 1);
                if (tokens.Count >= 3)
                    _sourceSkippedBytes = ParseFileBytes(tokens[2].Groups[1].Value, tokens[2].Groups[2].Value);
            }
        }

        logProgress.Report(line);
    }

    /// <summary>
    /// Called on each percentage update. Accumulates byte-delta samples into a rolling
    /// window and returns a formatted speed string every <see cref="SpeedReportInterval"/>,
    /// or null when it is not yet time to update the UI.
    /// </summary>
    private string? ComputeSpeedText(double newPercent)
    {
        if (_currentFileBytes <= 0) return null;

        var now = DateTime.UtcNow;
        var elapsedSinceLast = (now - _lastPercentTime).TotalSeconds;

        // Add a new sample if enough time has passed and progress has moved forward.
        if (elapsedSinceLast >= 0.05 && newPercent > _lastPercent)
        {
            var deltaBytes = (newPercent - _lastPercent) / 100.0 * _currentFileBytes;
            _speedSamples.Enqueue((now, deltaBytes));
        }

        _lastPercent = newPercent;
        _lastPercentTime = now;

        // Throttle: only push a value to the UI every SpeedReportInterval.
        if (now - _lastSpeedReport < SpeedReportInterval)
            return null;

        // Drop samples that have aged out of the rolling window.
        while (_speedSamples.Count > 1 && now - _speedSamples.Peek().Time > SpeedWindow)
            _speedSamples.Dequeue();

        if (_speedSamples.Count == 0) return null;

        var totalBytes   = _speedSamples.Sum(s => s.Bytes);
        var windowLength = (now - _speedSamples.Peek().Time).TotalSeconds;

        if (windowLength < 0.1) return null;

        _lastSpeedReport = now;
        return FormatSpeed(totalBytes / windowLength);
    }

    private static long ParseFileBytes(string digits, string unit)
    {
        var value = double.Parse(digits.Replace(",", ""), CultureInfo.InvariantCulture);
        return unit.ToLowerInvariant() switch
        {
            "k" => (long)(value * 1024),
            "m" => (long)(value * 1024 * 1024),
            "g" => (long)(value * 1024L * 1024 * 1024),
            "t" => (long)(value * 1024L * 1024 * 1024 * 1024),
            _   => (long)value,
        };
    }

    private static string FormatSpeed(double bytesPerSec) => bytesPerSec switch
    {
        >= 1024d * 1024 * 1024 => $"{bytesPerSec / (1024d * 1024 * 1024):F1} GB/s",
        >= 1024d * 1024        => $"{bytesPerSec / (1024d * 1024):F1} MB/s",
        >= 1024d               => $"{bytesPerSec / 1024d:F1} KB/s",
        _                      => $"{bytesPerSec:F0} B/s",
    };
}
