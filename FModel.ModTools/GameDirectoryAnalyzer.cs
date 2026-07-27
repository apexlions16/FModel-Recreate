namespace FModel.ModTools;

public sealed class GameDirectoryAnalyzer
{
    private static readonly HashSet<string> SupportedRepakVersions =
        ["V0", "V1", "V2", "V3", "V4", "V5", "V6", "V7", "V8A", "V8B", "V9", "V10", "V11"];

    public GameAnalysisReport Analyze(GameAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var gameDirectory = Path.GetFullPath(request.GameDirectory);
        var warnings = new List<string>();
        var blockers = new List<string>();

        if (!Directory.Exists(gameDirectory))
        {
            blockers.Add("The selected game directory does not exist.");
            return CreateReport(gameDirectory, null, request.EngineVersion, UnrealContainerKind.Unknown,
                ModSupportLevel.Unsupported, "../../../", "V8B", false, false, false, [], [], warnings, blockers);
        }

        var paksDirectory = LocatePaksDirectory(gameDirectory);
        if (paksDirectory is null)
        {
            blockers.Add("No Unreal Content/Paks directory could be located.");
            return CreateReport(gameDirectory, null, request.EngineVersion, UnrealContainerKind.Unknown,
                ModSupportLevel.Unsupported, "../../../", "V8B", false, false, false, [], [], warnings, blockers);
        }

        var pakFiles = Directory.EnumerateFiles(paksDirectory, "*.pak", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        var ioStoreFiles = Directory.EnumerateFiles(paksDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(static path => Path.GetExtension(path).Equals(".utoc", StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetExtension(path).Equals(".ucas", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        var hasSignatures = Directory.EnumerateFiles(paksDirectory, "*.sig", SearchOption.TopDirectoryOnly).Any();

        var containerKind = (pakFiles.Length > 0, ioStoreFiles.Length > 0) switch
        {
            (true, false) => UnrealContainerKind.LegacyPak,
            (false, true) => UnrealContainerKind.IoStore,
            (true, true) => UnrealContainerKind.Mixed,
            _ => UnrealContainerKind.Unknown
        };

        var observations = request.PakObservations;
        var mountPoint = observations
            .Where(static observation => !string.IsNullOrWhiteSpace(observation.MountPoint))
            .GroupBy(static observation => UnrealPath.NormalizeMountPoint(observation.MountPoint), StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .Select(static group => group.Key)
            .FirstOrDefault() ?? "../../../";

        var versionObservation = observations
            .Where(static observation => observation.PakVersion is >= 0 and <= 11)
            .OrderByDescending(static observation => observation.PakVersion)
            .FirstOrDefault();
        var repakVersion = versionObservation is null
            ? GuessRepakVersion(request.EngineVersion)
            : ToRepakVersion(versionObservation.PakVersion, versionObservation.IsPakVersion8A);

        var encryptedIndex = observations.Any(static observation => observation.EncryptedIndex);
        var customFormat = observations.Any(static observation => observation.UsesCustomFormat);

        if (!request.EngineVersion.IsUnreal4)
            blockers.Add($"The first implementation supports Unreal Engine 4 only; detected {request.EngineVersion}.");
        if (containerKind == UnrealContainerKind.IoStore)
            blockers.Add("The game uses IoStore (.utoc/.ucas) without a legacy PAK container. IoStore writing is a later backend.");
        if (containerKind == UnrealContainerKind.Unknown)
            blockers.Add("No legacy .pak or IoStore container was found.");
        if (hasSignatures)
            blockers.Add("The game directory contains .sig files. FModel-Recreate will not bypass signed-PAK enforcement.");
        if (customFormat)
            blockers.Add("CUE4Parse reported a custom PAK implementation. A standard repacker cannot safely reproduce this format.");
        if (!SupportedRepakVersions.Contains(repakVersion))
            blockers.Add($"The detected PAK version {repakVersion} is not writable by the repak backend.");

        if (containerKind == UnrealContainerKind.Mixed)
            warnings.Add("Both legacy PAK and IoStore files are present. Only the legacy PAK path is handled by this phase.");
        if (encryptedIndex)
            warnings.Add("One or more source PAK indexes are encrypted. The mod PAK is intentionally not encrypted; whether the game accepts it is game-specific.");
        if (observations.Count == 0)
            warnings.Add("No mounted PAK metadata was available, so the PAK version and mount point were inferred.");
        if (!mountPoint.StartsWith("../", StringComparison.Ordinal) && mountPoint != "/")
            warnings.Add($"The detected mount point '{mountPoint}' is unusual and should be reviewed before installation.");

        var support = blockers.Count > 0
            ? ModSupportLevel.Unsupported
            : warnings.Count > 0 ? ModSupportLevel.Conditional : ModSupportLevel.Supported;

        return CreateReport(gameDirectory, paksDirectory, request.EngineVersion, containerKind, support,
            mountPoint, repakVersion, encryptedIndex, hasSignatures, customFormat,
            pakFiles, ioStoreFiles, warnings, blockers);
    }

    public static string? LocatePaksDirectory(string gameDirectory)
    {
        var root = Path.GetFullPath(gameDirectory);
        if (!Directory.Exists(root)) return null;

        if (Path.GetFileName(root).Equals("Paks", StringComparison.OrdinalIgnoreCase))
            return root;

        var directCandidates = new[]
        {
            Path.Combine(root, "Content", "Paks"),
            Path.Combine(root, "Game", "Content", "Paks")
        };
        var direct = directCandidates.FirstOrDefault(Directory.Exists);
        if (direct is not null) return direct;

        try
        {
            return Directory.EnumerateDirectories(root, "Paks", SearchOption.AllDirectories)
                .Where(static path => path.Contains($"{Path.DirectorySeparatorChar}Content{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path.Count(character => character == Path.DirectorySeparatorChar))
                .FirstOrDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static string ToRepakVersion(int version, bool isVersion8A = false) => version switch
    {
        0 => "V0",
        1 => "V1",
        2 => "V2",
        3 => "V3",
        4 => "V4",
        5 => "V5",
        6 => "V6",
        7 => "V7",
        8 => isVersion8A ? "V8A" : "V8B",
        9 => "V9",
        10 => "V10",
        11 => "V11",
        _ => $"V{version}"
    };

    private static string GuessRepakVersion(UnrealEngineVersion version)
    {
        if (!version.IsUnreal4) return "V11";
        return version.Minor switch
        {
            <= 2 => "V1",
            <= 4 => "V2",
            <= 10 => "V3",
            <= 16 => "V4",
            <= 19 => "V5",
            <= 21 => "V7",
            <= 24 => "V8B",
            <= 25 => "V9",
            26 => "V10",
            _ => "V11"
        };
    }

    private static GameAnalysisReport CreateReport(
        string gameDirectory,
        string? paksDirectory,
        UnrealEngineVersion engineVersion,
        UnrealContainerKind containerKind,
        ModSupportLevel supportLevel,
        string mountPoint,
        string repakVersion,
        bool encrypted,
        bool signed,
        bool custom,
        IReadOnlyList<string> pakFiles,
        IReadOnlyList<string> ioStoreFiles,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> blockers) => new()
    {
        GameDirectory = gameDirectory,
        PaksDirectory = paksDirectory,
        EngineVersion = engineVersion,
        ContainerKind = containerKind,
        SupportLevel = supportLevel,
        MountPoint = mountPoint,
        RepakVersion = repakVersion,
        HasEncryptedPakIndex = encrypted,
        HasPakSignatures = signed,
        UsesCustomPakFormat = custom,
        PakFiles = pakFiles,
        IoStoreFiles = ioStoreFiles,
        Warnings = warnings,
        BlockingReasons = blockers
    };
}
