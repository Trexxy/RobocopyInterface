using System.IO;
using RobocopyInterface.Models;
using RobocopyInterface.Services;
using RobocopyInterface.ViewModels;

namespace RobocopyInterface.Tests;

public class MainViewModelCanStartSyncTests
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RobocopyInterface", "settings.json");

    private string? _backupJson;

    [SetUp]
    public void SetUp()
    {
        _backupJson = File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null;
    }

    [TearDown]
    public void TearDown()
    {
        if (_backupJson is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, _backupJson);
        }
        else if (File.Exists(SettingsPath))
        {
            File.Delete(SettingsPath);
        }
    }

    [Test]
    public void CanStartSync_NoSources_IsFalse()
    {
        var vm = new MainViewModel(new RobocopyRunner());
        vm.Sources.Clear();

        Assert.That(vm.StartSyncCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void CanStartSync_SourceWithBlankTarget_IsFalse()
    {
        var vm = new MainViewModel(new RobocopyRunner());
        vm.Sources.Clear();
        vm.Sources.Add(new SourceTargetEntry(@"C:\src"));

        Assert.That(vm.StartSyncCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void CanStartSync_SourceWithTarget_IsTrue()
    {
        var vm = new MainViewModel(new RobocopyRunner());
        vm.Sources.Clear();
        vm.Sources.Add(new SourceTargetEntry(@"C:\src", @"C:\dst"));

        Assert.That(vm.StartSyncCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void CanStartSync_OneOfTwoSourcesHasBlankTarget_IsFalse()
    {
        var vm = new MainViewModel(new RobocopyRunner());
        vm.Sources.Clear();
        vm.Sources.Add(new SourceTargetEntry(@"C:\src1", @"C:\dst1"));
        vm.Sources.Add(new SourceTargetEntry(@"C:\src2"));

        Assert.That(vm.StartSyncCommand.CanExecute(null), Is.False);
    }
}
