namespace FModel.ModTools.Tests;

public sealed class LocResCodecTests
{
    [Theory]
    [InlineData(LocResVersion.Legacy)]
    [InlineData(LocResVersion.Compact)]
    [InlineData(LocResVersion.OptimizedCrc32)]
    [InlineData(LocResVersion.OptimizedCityHash64Utf16)]
    public void WriteRead_RoundTripsAllStandardVersions(LocResVersion version)
    {
        var document = CreateDocument(version);
        using var stream = new MemoryStream();
        var codec = new LocResCodec();
        codec.Write(stream, document);
        stream.Position = 0;
        var parsed = codec.Read(stream);

        Assert.Equal(version, parsed.Version);
        Assert.Equal(document.Entries.Count, parsed.Entries.Count);
        Assert.Equal("Merhaba dünya", GetEntry(parsed, "Menu", "Greeting").LocalizedString);
        Assert.Equal("Çıkış", GetEntry(parsed, "Menu", "Exit").LocalizedString);
        Assert.Equal("Çıkış", GetEntry(parsed, "HUD", "Exit").LocalizedString);
        Assert.Equal(0x10203040u, GetEntry(parsed, "Menu", "Greeting").SourceStringHash);
        if (version >= LocResVersion.OptimizedCrc32)
        {
            Assert.Equal(0x11111111u, GetEntry(parsed, "Menu", "Greeting").NamespaceHash);
            Assert.Equal(0x22222222u, GetEntry(parsed, "Menu", "Greeting").KeyHash);
        }
    }

    [Fact]
    public void CloneWithTranslations_ChangesOnlyRequestedValue()
    {
        var source = CreateDocument(LocResVersion.OptimizedCrc32);
        var codec = new LocResCodec();
        var translated = codec.CloneWithTranslations(source, new Dictionary<(string, string), string>
        {
            [("Menu", "Exit")] = "Oyundan Çık"
        });

        Assert.Equal("Merhaba dünya", GetEntry(translated, "Menu", "Greeting").LocalizedString);
        Assert.Equal("Oyundan Çık", GetEntry(translated, "Menu", "Exit").LocalizedString);
        Assert.Equal("Çıkış", GetEntry(translated, "HUD", "Exit").LocalizedString);
        Assert.Equal(GetEntry(source, "Menu", "Exit").KeyHash,
            GetEntry(translated, "Menu", "Exit").KeyHash);
    }

    [Fact]
    public void Write_RejectsDuplicateNamespaceAndKey()
    {
        var document = CreateDocument(LocResVersion.Compact);
        document.Entries.Add(new LocResEntry
        {
            Namespace = "Menu",
            Key = "Exit",
            SourceStringHash = 1,
            LocalizedString = "Duplicate"
        });
        using var stream = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => new LocResCodec().Write(stream, document));
    }

    private static LocResEntry GetEntry(LocResDocument document, string @namespace, string key) =>
        document.Entries.Single(entry =>
            entry.Namespace.Equals(@namespace, StringComparison.Ordinal) &&
            entry.Key.Equals(key, StringComparison.Ordinal));

    private static LocResDocument CreateDocument(LocResVersion version)
    {
        var document = new LocResDocument { Version = version };
        document.Entries.Add(new LocResEntry
        {
            Namespace = "Menu",
            NamespaceHash = 0x11111111,
            Key = "Greeting",
            KeyHash = 0x22222222,
            SourceStringHash = 0x10203040,
            LocalizedString = "Merhaba dünya"
        });
        document.Entries.Add(new LocResEntry
        {
            Namespace = "Menu",
            NamespaceHash = 0x11111111,
            Key = "Exit",
            KeyHash = 0x33333333,
            SourceStringHash = 0x50607080,
            LocalizedString = "Çıkış"
        });
        document.Entries.Add(new LocResEntry
        {
            Namespace = "HUD",
            NamespaceHash = 0x44444444,
            Key = "Exit",
            KeyHash = 0x33333333,
            SourceStringHash = 0x90A0B0C0,
            LocalizedString = "Çıkış"
        });
        return document;
    }
}
