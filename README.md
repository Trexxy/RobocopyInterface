# RobocopyInterface

A Windows desktop application that provides a graphical interface for [Robocopy](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy), making it easy to sync multiple files and folders to a destination with live progress feedback.

## Requirements

- Windows 10 or later
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows Desktop Runtime)
- Robocopy (included with Windows)

## Features

- Add any number of source **files** and/or **folders** to sync; click **Clear** to remove all sources at once
- Sources and destination are **remembered between restarts** (saved to `%AppData%\RobocopyInterface\settings.json`)
- Choose a destination folder via a folder picker dialog
- Live scrolling log output showing what Robocopy is doing
- **Two progress bars:**
  - *Overall* — shows `X / Y files` synced in the bar and `A / B` size transferred below the label. Y and B are pre-scanned from all sources before the sync starts. X and A increment live as each file is copied; already-up-to-date (skipped) files are added in a batch at the end of each source using the `Files :` and `Bytes :` counts from Robocopy's summary output.
  - *Current file* — shows 0–100% for the file currently being copied, with the percentage printed inside the bar
- **Copy speed indicator** — shows a rolling average (over 3 seconds) of the current transfer rate in B/s, KB/s, MB/s, or GB/s; updates every 2 seconds to prevent flickering; clears between files
- Cancel a running sync at any time

## Usage

1. Click **+ Add Folder** or **+ Add File** to add one or more sources. Click **Clear** to remove them all.
   - Folders are synced recursively into a same-named subfolder inside the destination.
   - Files are copied directly into the destination folder.
2. Set the **Destination** by typing a path or clicking **Browse**.
3. Click **Start Sync**. The log, progress bars, and speed indicator update in real time.
4. Click **Cancel** to stop the sync at any time.

Sources and destination are saved automatically and restored on next launch.

## How it works

Each source is synced using the following Robocopy command:

```
robocopy "<source>" "<destination>" /E /COPY:D /R:1 /W:1 /NDL
```

| Flag | Effect |
|---|---|
| `/E` | Recurse all subdirectories, including empty ones |
| `/COPY:D` | Copy file data only — no timestamps, permissions, or ownership |
| `/R:1` | Retry once on failure (avoids long hangs on locked files) |
| `/W:1` | Wait 1 second between retries |
| `/NDL` | Suppress directory listing lines in the log output |

Robocopy emits per-file percentage updates using carriage returns (`\r`) rather than newlines. The application reads the raw output stream and splits on both `\r` and `\n` to capture these updates and drive the current-file progress bar. Percentage lines are filtered out of the log so only meaningful file and summary lines are shown.

Copy speed is derived from the file size printed in Robocopy's file announcement lines (e.g. `New File  1,073,741,824  bigfile.bin`) combined with the percentage delta and elapsed time between samples.

Robocopy exit codes 0–7 indicate success or partial success (files skipped/extra); codes 8 and above indicate errors. The raw output is shown in the log so you can inspect what happened.

## Architecture

```
RobocopyInterface/
  App.xaml / App.xaml.cs          — Generic host setup and DI registration
  MainWindow.xaml / .xaml.cs      — View (declarative XAML bindings, auto-scroll helper)
  Converters/
    InverseBoolConverter.cs       — Flips a bool binding (used to disable UI while syncing)
  ViewModels/
    MainViewModel.cs              — All UI logic: commands, properties, cancellation
  Services/
    RobocopyRunner.cs             — Launches Robocopy, reads stdout/stderr, parses progress and speed
```

**Technology choices:**

| Concern | Choice |
|---|---|
| UI framework | WPF on .NET 10 |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — `[ObservableProperty]`, `[RelayCommand]` source generators |
| Dependency injection | `Microsoft.Extensions.Hosting` generic host |
| Progress reporting | `IProgress<T>` — thread-safe, no manual `Dispatcher` calls required |
| Cancellation | `CancellationToken` throughout; kills the Robocopy process tree on cancel |

## Building from source

```bash
dotnet build RobocopyInterface/RobocopyInterface.csproj
dotnet run --project RobocopyInterface/RobocopyInterface.csproj
```
