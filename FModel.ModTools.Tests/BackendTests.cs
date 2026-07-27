namespace FModel.ModTools.Tests;

public sealed class BackendTests
{
    [Fact]
    public void RepakArguments_UseVerifiedCliContract()
    {
        using var root = new TestDirectory();
        var staging = root.Combine("Staging");
        Directory.CreateDirectory(staging);
        var request = new PakBuildRequest
        {
            ToolPath = root.Combine("repak.exe"),
            StagingDirectory = staging,
            OutputPakPath = root.Combine("Build", "Test_P.pak"),
            MountPoint = "../../../",
            PakVersion = "V11",
            Compression = "Zlib",
            ExpectedVirtualPaths = ["Game/Content/Test.bin"]
        };

        var arguments = RepakBackend.CreatePackArguments(request);
        Assert.Equal("pack", arguments[0]);
        Assert.Contains("--mount-point", arguments);
        Assert.Contains("--version", arguments);
        Assert.Contains("V11", arguments);
        Assert.Contains("--compression", arguments);
        Assert.Contains("Zlib", arguments);
        Assert.Equal(Path.GetFullPath(request.OutputPakPath), arguments[^1]);
    }

    [Fact]
    public void UnrealPakResponse_MapsStagingFilesToMountPoint()
    {
        using var root = new TestDirectory();
        var staging = root.Combine("Staging");
        var file = UnrealPath.ToContainedPath(staging, "Game/Content/Test.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, [1]);
        var request = new PakBuildRequest
        {
            ToolPath = root.Combine("UnrealPak.exe"),
            StagingDirectory = staging,
            OutputPakPath = root.Combine("Test_P.pak"),
            MountPoint = "../../../",
            PakVersion = "V11",
            ExpectedVirtualPaths = ["Game/Content/Test.bin"]
        };

        var response = UnrealPakBackend.CreateResponseFile(request);
        Assert.Contains(Path.GetFullPath(file), response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("../../../Game/Content/Test.bin", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepakBackend_RealRoundTrip_WhenIntegrationToolIsProvided()
    {
        var repak = Environment.GetEnvironmentVariable("FMR_REPAK_PATH");
        if (string.IsNullOrWhiteSpace(repak) || !File.Exists(repak)) return;

        using var root = new TestDirectory();
        var staging = root.Combine("Staging");
        var virtualPath = "Example/Content/Test/Smoke.txt";
        var file = UnrealPath.ToContainedPath(staging, virtualPath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "FModel-Recreate integration test");

        var result = await new RepakBackend(new SystemProcessRunner()).BuildAsync(new PakBuildRequest
        {
            ToolPath = repak,
            StagingDirectory = staging,
            OutputPakPath = root.Combine("Build", "Smoke_P.pak"),
            MountPoint = "../../../",
            PakVersion = "V11",
            Compression = "Zlib",
            ExpectedVirtualPaths = [virtualPath]
        }, CancellationToken.None);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors) + Environment.NewLine + result.Log);
        Assert.Contains(virtualPath, result.ListedFiles, StringComparer.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.OutputPakPath));
    }
}
