using System.Text.Json;
using RobocopyInterface.ViewModels;

namespace RobocopyInterface.Tests;

public class SettingsJsonTests
{
    [Test]
    public void Settings_RoundTripsThroughJson()
    {
        var original = new MainViewModel.Settings(
        [
            new MainViewModel.SourceTargetRecord(@"C:\src1", @"C:\dst1"),
            new MainViewModel.SourceTargetRecord(@"C:\src2", @"C:\dst2"),
        ]);

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<MainViewModel.Settings>(json);

        Assert.That(roundTripped, Is.Not.Null);
        Assert.That(roundTripped!.Entries, Has.Count.EqualTo(2));
        Assert.That(roundTripped.Entries[0], Is.EqualTo(original.Entries[0]));
        Assert.That(roundTripped.Entries[1], Is.EqualTo(original.Entries[1]));
    }

    [Test]
    public void Settings_OldFormatJson_DeserializesWithNullEntries()
    {
        const string oldFormatJson = """{"Sources":["C:\\src"],"Destination":"C:\\dst"}""";

        var settings = JsonSerializer.Deserialize<MainViewModel.Settings>(oldFormatJson);

        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!.Entries, Is.Null);
    }
}
