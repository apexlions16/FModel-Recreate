namespace FModel.ModTools.Tests;

public sealed class UnrealPathTests
{
    [Theory]
    [InlineData("Game\\Content\\UI\\Title.uasset", "Game/Content/UI/Title.uasset")]
    [InlineData("/Game/Content/Test.locres", "Game/Content/Test.locres")]
    public void NormalizeVirtualPath_NormalizesSafePaths(string input, string expected) =>
        Assert.Equal(expected, UnrealPath.NormalizeVirtualPath(input));

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("C:/game/file.uasset")]
    [InlineData("Game/Content/../../escape")]
    [InlineData("")]
    public void NormalizeVirtualPath_RejectsUnsafePaths(string input) =>
        Assert.ThrowsAny<ArgumentException>(() => UnrealPath.NormalizeVirtualPath(input));

    [Fact]
    public void ToContainedPath_CannotEscapeRoot()
    {
        using var root = new TestDirectory();
        Assert.ThrowsAny<ArgumentException>(() => UnrealPath.ToContainedPath(root.Path, "../outside.txt"));
    }

    [Fact]
    public void GetGameAssetPath_MapsProjectContentPath() =>
        Assert.Equal("/Game/UI/Title", UnrealPath.GetGameAssetPath("Example/Content/UI/Title.uasset"));
}
