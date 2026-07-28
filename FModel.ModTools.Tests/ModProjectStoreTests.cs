namespace FModel.ModTools.Tests;

public sealed class ModProjectStoreTests
{
    [Fact]
    public void CreateStageSaveLoad_RoundTripsAndValidates()
    {
        using var root = new TestDirectory();
        var profile = CreateProfile(root.Path);
        var store = ModProjectStore.Create(root.Path, "Turkish Text Mod", profile);
        store.StagePackage("Example/Content/Localization/Game/tr/Game.locres", new Dictionary<string, byte[]>
        {
            ["Example/Content/Localization/Game/tr/Game.locres"] = [1, 2, 3, 4]
        });

        Assert.True(store.Validate().IsValid);
        var loaded = ModProjectStore.Load(store.ProjectDirectory);
        Assert.Equal(store.Manifest.ProjectId, loaded.Manifest.ProjectId);
        Assert.Single(loaded.Manifest.AssetGroups);
        Assert.True(loaded.Validate().IsValid);
    }

    [Fact]
    public void Validate_DetectsExternalModification()
    {
        using var root = new TestDirectory();
        var store = ModProjectStore.Create(root.Path, "Hash Test", CreateProfile(root.Path));
        var group = store.StagePackage("Example/Content/Test.bin", new Dictionary<string, byte[]>
        {
            ["Example/Content/Test.bin"] = [1, 2, 3]
        });
        var staged = store.GetProjectContainedPath(group.Files.Single().RelativeStagingPath);
        File.WriteAllBytes(staged, [9, 9, 9]);

        var validation = store.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Code == "HASH_CHANGED");
    }

    [Fact]
    public void Validate_DetectsOrphanSidecar()
    {
        using var root = new TestDirectory();
        var store = ModProjectStore.Create(root.Path, "Sidecar Test", CreateProfile(root.Path));
        store.StagePackage("Example/Content/Test.uasset", new Dictionary<string, byte[]>
        {
            ["Example/Content/Test.uasset"] = [1],
            ["Example/Content/Other.uexp"] = [2]
        });

        var validation = store.Validate();
        Assert.Contains(validation.Issues, issue => issue.Code == "ORPHAN_SIDECAR");
    }

    private static GameModProfile CreateProfile(string root) => new()
    {
        GameName = "Example",
        GameDirectory = root,
        PaksDirectory = root,
        EngineVersion = new UnrealEngineVersion(4, 27),
        ContainerKind = UnrealContainerKind.LegacyPak,
        SupportLevel = ModSupportLevel.Supported,
        MountPoint = "../../../",
        RepakVersion = "V11"
    };
}
