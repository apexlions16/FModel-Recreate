namespace FModel.ModTools.Tests;

public sealed class ConversionPipelineTests
{
    [Fact]
    public void CreateImportScript_UsesExactGameAssetPathAndSentinel()
    {
        using var root = new TestDirectory();
        var source = root.Combine("Title.png");
        File.WriteAllBytes(source, [1]);
        var request = new UnrealEditorConversionRequest
        {
            EngineRoot = root.Path,
            SourceFile = source,
            TargetVirtualAssetPath = "Example/Content/UI/Title.uasset",
            Kind = UnrealAssetConversionKind.Texture,
            WorkingDirectory = root.Path
        };

        var script = UnrealEditorConversionPipeline.CreateImportScript(request);
        Assert.Contains("/Game/UI", script, StringComparison.Ordinal);
        Assert.Contains("Title", script, StringComparison.Ordinal);
        Assert.Contains("FMODEL_RECREATE_IMPORT_OK", script, StringComparison.Ordinal);
        Assert.Contains("AssetImportTask", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateArguments_UseOfficialCommandlets()
    {
        using var root = new TestDirectory();
        var project = root.Combine("Test.uproject");
        var script = root.Combine("Import.py");
        var importArgs = UnrealEditorConversionPipeline.CreateImportArguments(project, script);
        var cookArgs = UnrealEditorConversionPipeline.CreateCookArguments(project);
        Assert.Contains("-run=pythonscript", importArgs);
        Assert.Contains("-run=cook", cookArgs);
        Assert.Contains("-targetplatform=WindowsNoEditor", cookArgs);
    }
}
