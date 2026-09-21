namespace RobocopyInterface.Services;

public interface IFilePickerService
{
    Task<string?> PickFolderAsync(string title);
    Task<IReadOnlyList<string>> PickFilesAsync(string title);
}
