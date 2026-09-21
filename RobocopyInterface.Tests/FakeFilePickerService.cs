using RobocopyInterface.Services;

namespace RobocopyInterface.Tests;

internal sealed class FakeFilePickerService : IFilePickerService
{
    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);

    public Task<IReadOnlyList<string>> PickFilesAsync(string title) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
