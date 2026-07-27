using System.Text.Json.Serialization;

namespace FModel.ModTools;

public enum UnrealContainerKind
{
    Unknown,
    LegacyPak,
    IoStore,
    Mixed
}

public enum ModSupportLevel
{
    Supported,
    Conditional,
    Unsupported
}

public enum ModBuildBackendKind
{
    Repak,
    UnrealPak
}

public enum ModAssetSourceKind
{
    ExtractedPackage,
    ExternalCookedFiles,
    LocResEditor,
    UnrealEditorConversion
}

public enum UnrealAssetConversionKind
{
    Texture,
    Audio
}

public sealed record UnrealEngineVersion(int Major, int Minor)
{
    public bool IsUnreal4 => Major == 4;
    public bool IsUnreal5 => Major == 5;
    public override string ToString() => $"{Major}.{Minor}";
}

public sealed record PakContainerObservation(
    string SourcePath,
    string MountPoint,
    int PakVersion,
    bool IsPakVersion8A,
    bool EncryptedIndex,
    bool UsesCustomFormat,
    IReadOnlyList<string> CompressionMethods);

public sealed class GameAnalysisRequest
{
    public required string GameDirectory { get; init; }
    public required UnrealEngineVersion EngineVersion { get; init; }
    public IReadOnlyList<PakContainerObservation> PakObservations { get; init; } = [];
}

public sealed class GameAnalysisReport
{
    public required string GameDirectory { get; init; }
    public string? PaksDirectory { get; init; }
    public required UnrealEngineVersion EngineVersion { get; init; }
    public UnrealContainerKind ContainerKind { get; init; }
    public ModSupportLevel SupportLevel { get; init; }
    public string MountPoint { get; init; } = "../../../";
    public string RepakVersion { get; init; } = "V8B";
    public bool HasEncryptedPakIndex { get; init; }
    public bool HasPakSignatures { get; init; }
    public bool UsesCustomPakFormat { get; init; }
    public IReadOnlyList<string> PakFiles { get; init; } = [];
    public IReadOnlyList<string> IoStoreFiles { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public bool CanBuildLegacyPak => SupportLevel != ModSupportLevel.Unsupported && ContainerKind is UnrealContainerKind.LegacyPak or UnrealContainerKind.Mixed;
}

public sealed class GameModProfile
{
    public string GameName { get; set; } = string.Empty;
    public string GameDirectory { get; set; } = string.Empty;
    public string PaksDirectory { get; set; } = string.Empty;
    public UnrealEngineVersion EngineVersion { get; set; } = new(4, 27);
    public UnrealContainerKind ContainerKind { get; set; } = UnrealContainerKind.Unknown;
    public ModSupportLevel SupportLevel { get; set; } = ModSupportLevel.Unsupported;
    public string MountPoint { get; set; } = "../../../";
    public string RepakVersion { get; set; } = "V8B";
    public string Compression { get; set; } = "Zlib";
    public bool HasEncryptedPakIndex { get; set; }
    public bool HasPakSignatures { get; set; }
    public bool UsesCustomPakFormat { get; set; }

    public static GameModProfile FromAnalysis(string gameName, GameAnalysisReport report) => new()
    {
        GameName = gameName,
        GameDirectory = report.GameDirectory,
        PaksDirectory = report.PaksDirectory ?? string.Empty,
        EngineVersion = report.EngineVersion,
        ContainerKind = report.ContainerKind,
        SupportLevel = report.SupportLevel,
        MountPoint = report.MountPoint,
        RepakVersion = report.RepakVersion,
        HasEncryptedPakIndex = report.HasEncryptedPakIndex,
        HasPakSignatures = report.HasPakSignatures,
        UsesCustomPakFormat = report.UsesCustomPakFormat
    };
}

public sealed class ModProjectManifest
{
    public int SchemaVersion { get; set; } = 1;
    public Guid ProjectId { get; set; } = Guid.NewGuid();
    public string ModId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public GameModProfile Game { get; set; } = new();
    public ModBuildBackendKind PreferredBackend { get; set; } = ModBuildBackendKind.Repak;
    public List<ModAssetGroup> AssetGroups { get; set; } = [];
    public string? LastBuildPath { get; set; }
    public string? LastBuildSha256 { get; set; }
}

public sealed class ModAssetGroup
{
    public Guid GroupId { get; set; } = Guid.NewGuid();
    public string LogicalPath { get; set; } = string.Empty;
    public ModAssetSourceKind SourceKind { get; set; }
    public DateTimeOffset AddedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<ModAssetFile> Files { get; set; } = [];
}

public sealed class ModAssetFile
{
    public string VirtualPath { get; set; } = string.Empty;
    public string RelativeStagingPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed record ModValidationIssue(string Code, string Message, bool IsError);

public sealed class ModValidationResult
{
    public List<ModValidationIssue> Issues { get; } = [];
    [JsonIgnore]
    public bool IsValid => Issues.All(static issue => !issue.IsError);
}

public sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
}

public sealed class PakBuildRequest
{
    public required string ToolPath { get; init; }
    public required string StagingDirectory { get; init; }
    public required string OutputPakPath { get; init; }
    public required string MountPoint { get; init; }
    public required string PakVersion { get; init; }
    public string? Compression { get; init; }
    public IReadOnlyList<string> ExpectedVirtualPaths { get; init; } = [];
}

public sealed class PakBuildResult
{
    public bool Success { get; init; }
    public string? OutputPakPath { get; init; }
    public string? Sha256 { get; init; }
    public IReadOnlyList<string> ListedFiles { get; init; } = [];
    public string Log { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class InstalledModManifest
{
    public int SchemaVersion { get; set; } = 1;
    public Guid ProjectId { get; set; }
    public string ModId { get; set; } = string.Empty;
    public string InstalledPakPath { get; set; } = string.Empty;
    public string InstalledSha256 { get; set; } = string.Empty;
    public DateTimeOffset InstalledUtc { get; set; }
}

public sealed record ModInstallResult(bool Success, string? InstalledPath, string Message);

public sealed class LocResDocument
{
    public LocResVersion Version { get; init; }
    public List<LocResEntry> Entries { get; } = [];
}

public enum LocResVersion : byte
{
    Legacy = 0,
    Compact = 1,
    OptimizedCrc32 = 2,
    OptimizedCityHash64Utf16 = 3
}

public sealed class LocResEntry
{
    public string Namespace { get; set; } = string.Empty;
    public uint NamespaceHash { get; set; }
    public string Key { get; set; } = string.Empty;
    public uint KeyHash { get; set; }
    public uint SourceStringHash { get; set; }
    public string LocalizedString { get; set; } = string.Empty;
}

public sealed class UnrealEditorConversionRequest
{
    public required string EngineRoot { get; init; }
    public required string SourceFile { get; init; }
    public required string TargetVirtualAssetPath { get; init; }
    public required UnrealAssetConversionKind Kind { get; init; }
    public required string WorkingDirectory { get; init; }
    public bool ReplaceExisting { get; init; } = true;
}

public sealed class UnrealEditorConversionResult
{
    public bool Success { get; init; }
    public string? CookedAssetDirectory { get; init; }
    public IReadOnlyList<string> CookedFiles { get; init; } = [];
    public string Log { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = [];
}
