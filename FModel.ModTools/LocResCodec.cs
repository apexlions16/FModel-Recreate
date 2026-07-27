using System.Text;

namespace FModel.ModTools;

public sealed class LocResCodec
{
    private const uint MagicA = 0x7574140E;
    private const uint MagicB = 0xFC034A67;
    private const uint MagicC = 0x9D90154A;
    private const uint MagicD = 0x1B7F37C3;

    public LocResDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public LocResDocument Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek) throw new ArgumentException("The LocRes stream must be readable and seekable.", nameof(stream));
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        var version = LocResVersion.Legacy;
        if (stream.Length >= 17)
        {
            var start = stream.Position;
            var magic = (reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32());
            if (magic == (MagicA, MagicB, MagicC, MagicD))
            {
                version = (LocResVersion)reader.ReadByte();
                if (version > LocResVersion.OptimizedCityHash64Utf16)
                    throw new InvalidDataException($"Unsupported LocRes version: {(byte)version}");
            }
            else
            {
                stream.Position = start;
            }
        }

        var localizedStrings = Array.Empty<string>();
        if (version >= LocResVersion.Compact)
        {
            var stringArrayOffset = reader.ReadInt64();
            if (stringArrayOffset >= 0)
            {
                var returnPosition = stream.Position;
                if (stringArrayOffset > stream.Length - sizeof(int))
                    throw new InvalidDataException("The LocRes localized string array offset is outside the file.");
                stream.Position = stringArrayOffset;
                var stringCount = ReadCount(reader, "localized string");
                localizedStrings = new string[stringCount];
                for (var index = 0; index < stringCount; index++)
                {
                    localizedStrings[index] = ReadFString(reader);
                    if (version >= LocResVersion.OptimizedCrc32) _ = reader.ReadInt32();
                }
                stream.Position = returnPosition;
            }
        }

        if (version >= LocResVersion.OptimizedCrc32)
            _ = ReadCount(reader, "entry");

        var document = new LocResDocument { Version = version };
        var namespaceCount = ReadUnsignedCount(reader, "namespace");
        for (var namespaceIndex = 0; namespaceIndex < namespaceCount; namespaceIndex++)
        {
            var (namespaceValue, namespaceHash) = ReadTextKey(reader, version);
            var keyCount = ReadUnsignedCount(reader, "key");
            for (var keyIndex = 0; keyIndex < keyCount; keyIndex++)
            {
                var (keyValue, keyHash) = ReadTextKey(reader, version);
                var sourceHash = reader.ReadUInt32();
                string localized;
                if (version >= LocResVersion.Compact)
                {
                    var localizedIndex = reader.ReadInt32();
                    if (localizedIndex < 0 || localizedIndex >= localizedStrings.Length)
                        throw new InvalidDataException($"LocRes entry '{namespaceValue}:{keyValue}' references invalid string index {localizedIndex}.");
                    localized = localizedStrings[localizedIndex];
                }
                else
                {
                    localized = ReadFString(reader);
                }

                document.Entries.Add(new LocResEntry
                {
                    Namespace = namespaceValue,
                    NamespaceHash = namespaceHash,
                    Key = keyValue,
                    KeyHash = keyHash,
                    SourceStringHash = sourceHash,
                    LocalizedString = localized
                });
            }
        }

        return document;
    }

    public void Write(string path, LocResDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tempPath = Path.GetFullPath(path) + ".tmp";
        using (var stream = File.Create(tempPath)) Write(stream, document);
        File.Move(tempPath, path, true);
    }

    public void Write(Stream stream, LocResDocument document)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        if (!stream.CanWrite || !stream.CanSeek) throw new ArgumentException("The LocRes stream must be writable and seekable.", nameof(stream));
        if (document.Version > LocResVersion.OptimizedCityHash64Utf16)
            throw new InvalidDataException($"Unsupported LocRes version: {(byte)document.Version}");

        ValidateEntries(document);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        long stringArrayOffsetPosition = -1;
        if (document.Version != LocResVersion.Legacy)
        {
            writer.Write(MagicA);
            writer.Write(MagicB);
            writer.Write(MagicC);
            writer.Write(MagicD);
            writer.Write((byte)document.Version);
        }

        var stringTable = new List<string>();
        var stringIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        if (document.Version >= LocResVersion.Compact)
        {
            stringArrayOffsetPosition = stream.Position;
            writer.Write((long)-1);
            foreach (var entry in document.Entries)
            {
                if (stringIndices.ContainsKey(entry.LocalizedString)) continue;
                stringIndices.Add(entry.LocalizedString, stringTable.Count);
                stringTable.Add(entry.LocalizedString);
            }
        }

        if (document.Version >= LocResVersion.OptimizedCrc32)
            writer.Write(document.Entries.Count);

        var namespaceGroups = document.Entries
            .GroupBy(static entry => new TextKeyIdentity(entry.Namespace, entry.NamespaceHash))
            .ToArray();
        writer.Write(checked((uint)namespaceGroups.Length));
        foreach (var namespaceGroup in namespaceGroups)
        {
            WriteTextKey(writer, document.Version, namespaceGroup.Key.Value, namespaceGroup.Key.Hash);
            var entries = namespaceGroup.ToArray();
            writer.Write(checked((uint)entries.Length));
            foreach (var entry in entries)
            {
                WriteTextKey(writer, document.Version, entry.Key, entry.KeyHash);
                writer.Write(entry.SourceStringHash);
                if (document.Version >= LocResVersion.Compact)
                    writer.Write(stringIndices[entry.LocalizedString]);
                else
                    WriteFString(writer, entry.LocalizedString);
            }
        }

        if (document.Version >= LocResVersion.Compact)
        {
            var stringArrayOffset = stream.Position;
            writer.Write(stringTable.Count);
            foreach (var localizedString in stringTable)
            {
                WriteFString(writer, localizedString);
                if (document.Version >= LocResVersion.OptimizedCrc32)
                    writer.Write(document.Entries.Count(entry => entry.LocalizedString.Equals(localizedString, StringComparison.Ordinal)));
            }

            var endPosition = stream.Position;
            stream.Position = stringArrayOffsetPosition;
            writer.Write(stringArrayOffset);
            stream.Position = endPosition;
        }
    }

    public LocResDocument CloneWithTranslations(LocResDocument source, IReadOnlyDictionary<(string Namespace, string Key), string> translations)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(translations);
        var clone = new LocResDocument { Version = source.Version };
        foreach (var entry in source.Entries)
        {
            clone.Entries.Add(new LocResEntry
            {
                Namespace = entry.Namespace,
                NamespaceHash = entry.NamespaceHash,
                Key = entry.Key,
                KeyHash = entry.KeyHash,
                SourceStringHash = entry.SourceStringHash,
                LocalizedString = translations.TryGetValue((entry.Namespace, entry.Key), out var translation)
                    ? translation
                    : entry.LocalizedString
            });
        }
        return clone;
    }

    private static void ValidateEntries(LocResDocument document)
    {
        var duplicate = document.Entries.GroupBy(static entry => (entry.Namespace, entry.Key))
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"The LocRes contains duplicate namespace/key entries: {duplicate.Key.Namespace}:{duplicate.Key.Key}");

        if (document.Version >= LocResVersion.OptimizedCrc32)
        {
            foreach (var namespaceGroup in document.Entries.GroupBy(static entry => entry.Namespace))
            {
                if (namespaceGroup.Select(static entry => entry.NamespaceHash).Distinct().Count() != 1)
                    throw new InvalidDataException($"The optimized LocRes contains inconsistent namespace hashes for '{namespaceGroup.Key}'.");
            }
        }
    }

    private static (string Value, uint Hash) ReadTextKey(BinaryReader reader, LocResVersion version)
    {
        var hash = version >= LocResVersion.OptimizedCrc32 ? reader.ReadUInt32() : 0;
        return (ReadFString(reader), hash);
    }

    private static void WriteTextKey(BinaryWriter writer, LocResVersion version, string value, uint hash)
    {
        if (version >= LocResVersion.OptimizedCrc32) writer.Write(hash);
        WriteFString(writer, value);
    }

    private static int ReadCount(BinaryReader reader, string label)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > 20_000_000)
            throw new InvalidDataException($"Invalid {label} count: {count}");
        return count;
    }

    private static int ReadUnsignedCount(BinaryReader reader, string label)
    {
        var count = reader.ReadUInt32();
        if (count > 20_000_000)
            throw new InvalidDataException($"Invalid {label} count: {count}");
        return checked((int)count);
    }

    private static string ReadFString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length == 0) return string.Empty;
        if (length == int.MinValue) throw new InvalidDataException("Invalid FString length.");

        if (length < 0)
        {
            var characterCount = -length;
            if (characterCount > 100_000_000) throw new InvalidDataException("FString is too large.");
            var bytes = reader.ReadBytes(checked(characterCount * 2));
            if (bytes.Length != characterCount * 2) throw new EndOfStreamException();
            return Encoding.Unicode.GetString(bytes, 0, Math.Max(0, bytes.Length - 2));
        }

        if (length > 100_000_000) throw new InvalidDataException("FString is too large.");
        var ansiBytes = reader.ReadBytes(length);
        if (ansiBytes.Length != length) throw new EndOfStreamException();
        var contentLength = Math.Max(0, ansiBytes.Length - 1);
        try
        {
            return new UTF8Encoding(false, true).GetString(ansiBytes, 0, contentLength);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(ansiBytes, 0, contentLength);
        }
    }

    private static void WriteFString(BinaryWriter writer, string? value)
    {
        value ??= string.Empty;
        if (value.Length == 0)
        {
            writer.Write(0);
            return;
        }

        var useAnsi = value.All(static character => character is > '\0' and <= '\x7F');
        if (useAnsi)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length + 1);
            writer.Write(bytes);
            writer.Write((byte)0);
        }
        else
        {
            writer.Write(-(value.Length + 1));
            writer.Write(Encoding.Unicode.GetBytes(value));
            writer.Write((ushort)0);
        }
    }

    private readonly record struct TextKeyIdentity(string Value, uint Hash);
}
