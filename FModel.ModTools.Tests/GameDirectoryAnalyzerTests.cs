namespace FModel.ModTools.Tests;

public sealed class GameDirectoryAnalyzerTests
{
    [Fact]
    public void Analyze_LegacyUnsignedPak_IsSupported()
    {
        using var root = new TestDirectory();
        var paks = root.Combine("Example", "Content", "Paks");
        Directory.CreateDirectory(paks);
        File.WriteAllBytes(Path.Combine(paks, "pakchunk0-WindowsNoEditor.pak"), [1, 2, 3]);

        var report = new GameDirectoryAnalyzer().Analyze(new GameAnalysisRequest
        {
            GameDirectory = root.Path,
            EngineVersion = new UnrealEngineVersion(4, 27),
            PakObservations = [new("pakchunk0.pak", "../../../", 11, false, false, false, ["Zlib"])]
        });

        Assert.Equal(ModSupportLevel.Supported, report.SupportLevel);
        Assert.Equal(UnrealContainerKind.LegacyPak, report.ContainerKind);
        Assert.Equal("V11", report.RepakVersion);
        Assert.Equal(paks, report.PaksDirectory);
    }

    [Fact]
    public void Analyze_IoStoreOnly_IsUnsupported()
    {
        using var root = new TestDirectory();
        var paks = root.Combine("Example", "Content", "Paks");
        Directory.CreateDirectory(paks);
        File.WriteAllBytes(Path.Combine(paks, "pakchunk0-Windows.utoc"), [1]);
        File.WriteAllBytes(Path.Combine(paks, "pakchunk0-Windows.ucas"), [2]);

        var report = new GameDirectoryAnalyzer().Analyze(new GameAnalysisRequest
        {
            GameDirectory = root.Path,
            EngineVersion = new UnrealEngineVersion(4, 27)
        });

        Assert.Equal(ModSupportLevel.Unsupported, report.SupportLevel);
        Assert.Contains(report.BlockingReasons, reason => reason.Contains("IoStore", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_SignedPak_IsUnsupported()
    {
        using var root = new TestDirectory();
        var paks = root.Combine("Content", "Paks");
        Directory.CreateDirectory(paks);
        File.WriteAllBytes(Path.Combine(paks, "pakchunk0.pak"), [1]);
        File.WriteAllBytes(Path.Combine(paks, "pakchunk0.sig"), [1]);

        var report = new GameDirectoryAnalyzer().Analyze(new GameAnalysisRequest
        {
            GameDirectory = root.Path,
            EngineVersion = new UnrealEngineVersion(4, 25)
        });

        Assert.Equal(ModSupportLevel.Unsupported, report.SupportLevel);
        Assert.True(report.HasPakSignatures);
    }

    [Theory]
    [InlineData(8, true, "V8A")]
    [InlineData(8, false, "V8B")]
    [InlineData(11, false, "V11")]
    public void ToRepakVersion_PreservesVersion8Variant(int version, bool version8A, string expected) =>
        Assert.Equal(expected, GameDirectoryAnalyzer.ToRepakVersion(version, version8A));
}
