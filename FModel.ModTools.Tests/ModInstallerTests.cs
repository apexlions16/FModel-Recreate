namespace FModel.ModTools.Tests;

public sealed class ModInstallerTests
{
    [Fact]
    public void InstallAndUninstall_OnlyManageVerifiedOwnFile()
    {
        using var root = new TestDirectory();
        var paks = root.Combine("Game", "Content", "Paks");
        Directory.CreateDirectory(paks);
        var profile = new GameModProfile
        {
            GameName = "Example",
            GameDirectory = root.Path,
            PaksDirectory = paks,
            EngineVersion = new UnrealEngineVersion(4, 27),
            ContainerKind = UnrealContainerKind.LegacyPak,
            SupportLevel = ModSupportLevel.Supported,
            RepakVersion = "V11"
        };
        var project = ModProjectStore.Create(root.Combine("Projects"), "Installer Test", profile);
        var builtPak = project.GetBuildPakPath();
        Directory.CreateDirectory(Path.GetDirectoryName(builtPak)!);
        File.WriteAllBytes(builtPak, [1, 2, 3, 4]);

        var installer = new ModInstaller();
        var installed = installer.Install(project, builtPak);
        Assert.True(installed.Success, installed.Message);
        Assert.EndsWith("_P.pak", installed.InstalledPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(installed.InstalledPath));

        var uninstalled = installer.Uninstall(project);
        Assert.True(uninstalled.Success, uninstalled.Message);
        Assert.False(File.Exists(installed.InstalledPath));
    }

    [Fact]
    public void Uninstall_BlocksExternallyModifiedPak()
    {
        using var root = new TestDirectory();
        var paks = root.Combine("Content", "Paks");
        Directory.CreateDirectory(paks);
        var project = ModProjectStore.Create(root.Combine("Projects"), "Tamper Test", new GameModProfile
        {
            GameName = "Example",
            GameDirectory = root.Path,
            PaksDirectory = paks,
            EngineVersion = new UnrealEngineVersion(4, 27),
            ContainerKind = UnrealContainerKind.LegacyPak,
            SupportLevel = ModSupportLevel.Supported,
            RepakVersion = "V11"
        });
        var pak = project.GetBuildPakPath();
        File.WriteAllBytes(pak, [1, 2, 3]);
        var installer = new ModInstaller();
        var installed = installer.Install(project, pak);
        Assert.True(installed.Success);
        File.WriteAllBytes(installed.InstalledPath!, [9, 9, 9]);

        var result = installer.Uninstall(project);
        Assert.False(result.Success);
        Assert.True(File.Exists(installed.InstalledPath));
    }
}
