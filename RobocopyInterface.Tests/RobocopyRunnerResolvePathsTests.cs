using System.IO;
using RobocopyInterface.Services;

namespace RobocopyInterface.Tests;

public class RobocopyRunnerResolvePathsTests
{
    private string _tempRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RobocopyInterfaceTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void ResolvePaths_SourceIsFile_TargetsFileDirectlyIntoTarget()
    {
        var sourceFile = Path.Combine(_tempRoot, "file.txt");
        File.WriteAllText(sourceFile, "data");
        var target = Path.Combine(_tempRoot, "target");

        var (srcDir, destDir, fileFilter) = RobocopyRunner.ResolvePaths(sourceFile, target);

        Assert.That(srcDir, Is.EqualTo(_tempRoot));
        Assert.That(destDir, Is.EqualTo(target));
        Assert.That(fileFilter, Is.EqualTo("file.txt"));
    }

    [Test]
    public void ResolvePaths_SourceIsFolder_MirrorsIntoSameNamedSubfolderOfTarget()
    {
        var sourceFolder = Path.Combine(_tempRoot, "source");
        Directory.CreateDirectory(sourceFolder);
        var target = Path.Combine(_tempRoot, "target");

        var (srcDir, destDir, fileFilter) = RobocopyRunner.ResolvePaths(sourceFolder, target);

        Assert.That(srcDir, Is.EqualTo(sourceFolder));
        Assert.That(destDir, Is.EqualTo(Path.Combine(target, "source")));
        Assert.That(fileFilter, Is.Null);
    }
}
