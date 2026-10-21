using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Model.RunSavers;
using System.IO;
using System.Linq;
using System.Xml;
using Xunit;

namespace LiveSplit.Tests.Model.RunFactories;

public class NativeLibraryMust
{
    private static IRun ParseFile(string path, bool useFileStream)
    {
        LiveSplitCoreFactory.LoadLiveSplitCore();
        using Stream stream = useFileStream
            ? File.OpenRead(path)
            : new MemoryStream(File.ReadAllBytes(path));
        var factory = new StandardFormatsRunFactory(stream, path);
        return factory.Create(new StandardComparisonGeneratorsFactory());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseRealSplitsFile(bool useFileStream)
    {
        string path = RepositoryFiles.Find("lib", "livesplit-core", "tests", "run_files", "livesplit1.6.lss");

        IRun run = ParseFile(path, useFileStream);

        Assert.False(string.IsNullOrEmpty(run.GameName));
        Assert.True(run.Count > 0);
        Assert.True(run.AttemptCount > 0);
        Assert.NotNull(run[0].PersonalBestSplitTime[TimingMethod.RealTime]);
    }

    [Fact]
    public void ParseSplitsFileWithIcons()
    {
        string path = RepositoryFiles.Find("lib", "livesplit-core", "tests", "run_files", "Celeste - Any% (1.2.1.5).lss");

        IRun run = ParseFile(path, true);

        Assert.Equal("Celeste", run.GameName);
        Assert.NotNull(run.GameIcon);
        Assert.Contains(run, segment => segment.Icon != null);
    }

    [Fact]
    public void SaveAndReparseSplitsFile()
    {
        string path = RepositoryFiles.Find("lib", "livesplit-core", "tests", "run_files", "Celeste - Any% (1.2.1.5).lss");
        IRun original = ParseFile(path, true);

        using var stream = new MemoryStream();
        new XMLRunSaver().Save(original, stream);

        var document = new XmlDocument();
        stream.Position = 0;
        document.Load(stream);
        Assert.Equal("Run", document.DocumentElement.Name);

        stream.Position = 0;
        var factory = new StandardFormatsRunFactory(stream, "");
        IRun reparsed = factory.Create(new StandardComparisonGeneratorsFactory());

        Assert.Equal(original.GameName, reparsed.GameName);
        Assert.Equal(original.Count, reparsed.Count);
        Assert.Equal(
            original.Select(segment => segment.Name),
            reparsed.Select(segment => segment.Name));
        Assert.Equal(
            original.Select(segment => segment.PersonalBestSplitTime[TimingMethod.RealTime]),
            reparsed.Select(segment => segment.PersonalBestSplitTime[TimingMethod.RealTime]));
        Assert.NotNull(reparsed.GameIcon);
    }
}
