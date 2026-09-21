# RobocopyInterface

A Windows desktop application that provides a graphical interface for [Robocopy](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy), making it easy to sync multiple files and folders, each to its own target, with live progress feedback.

## Requirements

- Windows 10 or later
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows Desktop Runtime)
- Robocopy (included with Windows)

## Features

- Add any number of source **files** and/or **folders** to sync; click **Clear** to remove all sources at once
- Each source has its **own target folder**, editable per row via a textbox or that row's **Browse** button — sync different sources to different places in a single run
- Newly added sources pre-fill their target with the most recently used target, so adding several sources bound for the same place requires no retyping; each row stays independently editable afterward
- Sources and their targets are **remembered between restarts** (saved to `%AppData%\RobocopyInterface\settings.json`)
- Live scrolling log output showing what Robocopy is doing
- **Two progress bars:**
  - *Overall* — shows `X / Y files` synced in the bar and `A / B` size transferred below the label. Y and B are pre-scanned from all sources before the sync starts. X and A increment live as each file is copied; already-up-to-date (skipped) files are added in a batch at the end of each source using the `Files :` and `Bytes :` counts from Robocopy's summary output.
  - *Current file* — shows 0–100% for the file currently being copied, with the percentage printed inside the bar
- **Copy speed indicator** — shows a rolling average (over 3 seconds) of the current transfer rate in B/s, KB/s, MB/s, or GB/s; updates every 2 seconds to prevent flickering; clears between files
- Cancel a running sync at any time

## Usage

1. Click **+ Add Folder** or **+ Add File** to add one or more sources. Click **Clear** to remove them all.
   - Folders are synced recursively into a same-named subfolder inside their target.
   - Files are copied directly into their target folder.
2. For each source row, set its **Target** by typing a path or clicking that row's **Browse** button. New rows pre-fill with the most recently used target — edit as needed per row.
3. Click **Start Sync**. The log, progress bars, and speed indicator update in real time.
4. Click **Cancel** to stop the sync at any time.

Sources and their targets are saved automatically and restored on next launch.

## How it works

Each source is synced to its own target with the following Robocopy command, run once per source/target pair:

```
robocopy "<source>" "<target>" /E /COPY:D /R:1 /W:1 /NDL
```

The log marks the start of each pair with `--- Syncing: <source> -> <target> ---`.

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
  Models/
    SourceTargetEntry.cs          — A single source/target pair; observable Target for two-way binding
  ViewModels/
    MainViewModel.cs              — All UI logic: commands, properties, cancellation
  Services/
    RobocopyRunner.cs             — Launches Robocopy per source/target pair, reads stdout/stderr, parses progress and speed

RobocopyInterface.Tests/          — NUnit tests for SourceTargetEntry, RobocopyRunner's path resolution,
                                     settings JSON round-tripping, and MainViewModel's CanStartSync logic
```

**Technology choices:**

| Concern | Choice |
|---|---|
| UI framework | WPF on .NET 10 |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — `[ObservableProperty]`, `[RelayCommand]` source generators |
| Dependency injection | `Microsoft.Extensions.Hosting` generic host |
| Progress reporting | `IProgress<T>` — thread-safe, no manual `Dispatcher` calls required |
| Cancellation | `CancellationToken` throughout; kills the Robocopy process tree on cancel |
| Testing | NUnit (`RobocopyInterface.Tests`) |

## Building from source

```bash
dotnet build RobocopyInterface/RobocopyInterface.csproj
dotnet run --project RobocopyInterface/RobocopyInterface.csproj
```

## Running tests

```bash
dotnet test
```
