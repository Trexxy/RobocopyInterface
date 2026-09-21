using System.ComponentModel;
using RobocopyInterface.Models;

namespace RobocopyInterface.Tests;

public class SourceTargetEntryTests
{
    [Test]
    public void Constructor_SetsSourceAndTarget()
    {
        var entry = new SourceTargetEntry(@"C:\src", @"C:\dst");

        Assert.That(entry.Source, Is.EqualTo(@"C:\src"));
        Assert.That(entry.Target, Is.EqualTo(@"C:\dst"));
    }

    [Test]
    public void Constructor_DefaultsTargetToEmpty()
    {
        var entry = new SourceTargetEntry(@"C:\src");

        Assert.That(entry.Target, Is.EqualTo(string.Empty));
    }

    [Test]
    public void SettingTarget_RaisesPropertyChangedForTarget()
    {
        var entry = new SourceTargetEntry(@"C:\src");
        var raised = new List<string?>();
        entry.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        entry.Target = @"C:\dst";

        Assert.That(raised, Contains.Item(nameof(SourceTargetEntry.Target)));
    }
}
