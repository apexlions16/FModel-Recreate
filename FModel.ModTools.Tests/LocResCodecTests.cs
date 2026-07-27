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
        Assert.Equal("Merhaba dünya", parsed.Entries.Single(entry => entry.Key == "Greeting").LocalizedString);
        Assert.Equal("Çıkış", parsed.Entries.Single(entry => entry.Key == "Exit").LocalizedString);
        Assert.Equal(0x10203040u, parsed.Entries.Single(entry => entry.Key == "Greeting").SourceStringHash);
        if (version >= LocResVersion.OptimizedCrc32)
        {
            Assert.Equal(0x11111111u, parsed.Entries[0].NamespaceHash);
            Assert.Equal(0x22222222u, parsed.Entries[0].KeyHash);
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

        Assert.Equal("Merhaba dünya", translated.Entries.Single(entry => entry.Key == "Greeting").LocalizedString);
        Assert.Equal("Oyundan Çık", translated.Entries.Single(entry => entry.Key == "Exit").LocalizedString);
        Assert.Equal(source.Entries.Single(entry => entry.Key == "Exit").KeyHash,
            translated.Entries.Single(entry => entry.Key == "Exit").KeyHash);
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
