using System.Security.Cryptography;
using System.Text.Json;

namespace FModel.ModTools;

public sealed class ModProjectStore
{
    public const string ManifestFileName = "ModProject.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ProjectDirectory { get; }
    public string ManifestPath => Path.Combine(ProjectDirectory, ManifestFileName);
    public string SourcesDirectory => Path.Combine(ProjectDirectory, "Sources");
    public string StagingDirectory => Path.Combine(ProjectDirectory, "Staging");
    public string BuildDirectory => Path.Combine(ProjectDirectory, "Build");
    public string LogsDirectory => Path.Combine(ProjectDirectory, "Logs");
    public string InstallDirectory => Path.Combine(ProjectDirectory, "Install");
    public ModProjectManifest Manifest { get; private set; }

    private ModProjectStore(string projectDirectory, ModProjectManifest manifest)
    {
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        Manifest = manifest;
        EnsureDirectories();
    }

    public static ModProjectStore Create(string parentDirectory, string displayName, GameModProfile gameProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(gameProfile);

        var modId = UnrealPath.SanitizeIdentifier(displayName, "fmodel_recreate_mod");
        var projectDirectory = Path.Combine(Path.GetFullPath(parentDirectory), modId);
        Directory.CreateDirectory(projectDirectory);
        var store = new ModProjectStore(projectDirectory, new ModProjectManifest
        {
            ModId = modId,
            DisplayName = displayName.Trim(),
            Game = gameProfile
        });
        store.Save();
        return store;
    }

    public static ModProjectStore Load(string projectDirectoryOrManifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectoryOrManifest);
        var path = Path.GetFullPath(projectDirectoryOrManifest);
        var manifestPath = Directory.Exists(path) ? Path.Combine(path, ManifestFileName) : path;
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("The mod project manifest could not be found.", manifestPath);

        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<ModProjectManifest>(json, JsonOptions)
                       ?? throw new InvalidDataException("The mod project manifest is empty or invalid.");
        if (manifest.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported mod project schema version: {manifest.SchemaVersion}");
        return new ModProjectStore(Path.GetDirectoryName(manifestPath)!, manifest);
    }

    public void Save()
    {
        Manifest.UpdatedUtc = DateTimeOffset.UtcNow;
        EnsureDirectories();
        var tempPath = ManifestPath + ".tmp";
        var json = JsonSerializer.Serialize(Manifest, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ManifestPath, true);
    }

    public ModAssetGroup StagePackage(
        string logicalPath,
        IReadOnlyDictionary<string, byte[]> files,
        ModAssetSourceKind sourceKind = ModAssetSourceKind.ExtractedPackage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0) throw new ArgumentException("The package did not contain any files.", nameof(files));

        var normalizedFiles = files.Select(pair =>
            new KeyValuePair<string, byte[]>(UnrealPath.NormalizeVirtualPath(pair.Key), pair.Value)).ToArray();
        var duplicate = normalizedFiles.GroupBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"The package contains duplicate virtual paths: {duplicate.Key}");

        var group = new ModAssetGroup
        {
            LogicalPath = UnrealPath.NormalizeVirtualPath(logicalPath),
            SourceKind = sourceKind
        };

        foreach (var pair in normalizedFiles)
        {
            var destination = UnrealPath.ToContainedPath(StagingDirectory, pair.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, pair.Value);
            group.Files.Add(new ModAssetFile
            {
                VirtualPath = pair.Key,
                RelativeStagingPath = Path.GetRelativePath(ProjectDirectory, destination).Replace('\\', '/'),
                Size = pair.Value.LongLength,
                Sha256 = ComputeSha256(pair.Value)
            });
        }

        ReplaceLogicalGroup(group);
        Save();
        return group;
    }

    public ModAssetGroup StageExternalFiles(
        string logicalPath,
        IEnumerable<(string SourcePath, string VirtualPath)> files,
        ModAssetSourceKind sourceKind = ModAssetSourceKind.ExternalCookedFiles)
    {
        ArgumentNullException.ThrowIfNull(files);
        var fileArray = files.ToArray();
        if (fileArray.Length == 0) throw new ArgumentException("No files were selected.", nameof(files));

        var group = new ModAssetGroup
        {
            LogicalPath = UnrealPath.NormalizeVirtualPath(logicalPath),
            SourceKind = sourceKind
        };

        foreach (var (sourcePath, virtualPathValue) in fileArray)
        {
            var source = Path.GetFullPath(sourcePath);
            if (!File.Exists(source)) throw new FileNotFoundException("A selected cooked file does not exist.", source);
            var virtualPath = UnrealPath.NormalizeVirtualPath(virtualPathValue);
            var destination = UnrealPath.ToContainedPath(StagingDirectory, virtualPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
            var info = new FileInfo(destination);
            group.Files.Add(new ModAssetFile
            {
                VirtualPath = virtualPath,
                RelativeStagingPath = Path.GetRelativePath(ProjectDirectory, destination).Replace('\\', '/'),
                Size = info.Length,
                Sha256 = ComputeSha256(destination)
            });
        }

        ReplaceLogicalGroup(group);
        Save();
        return group;
    }

    public void RemoveGroup(Guid groupId)
    {
        var group = Manifest.AssetGroups.FirstOrDefault(group => group.GroupId == groupId);
        if (group is null) return;
        Manifest.AssetGroups.Remove(group);
        foreach (var file in group.Files)
        {
            var path = GetProjectContainedPath(file.RelativeStagingPath);
            if (File.Exists(path)) File.Delete(path);
        }
        Save();
    }

    public ModValidationResult Validate(bool verifyHashes = true)
    {
        var result = new ModValidationResult();
        if (string.IsNullOrWhiteSpace(Manifest.ModId))
            result.Issues.Add(new("PROJECT_MOD_ID", "The project has no mod identifier.", true));
        if (Manifest.AssetGroups.Count == 0)
            result.Issues.Add(new("PROJECT_EMPTY", "The mod project contains no staged files.", true));
        if (!Manifest.Game.EngineVersion.IsUnreal4)
            result.Issues.Add(new("ENGINE_UNSUPPORTED", "The current build phase supports Unreal Engine 4 only.", true));
        if (Manifest.Game.SupportLevel == ModSupportLevel.Unsupported)
            result.Issues.Add(new("GAME_UNSUPPORTED", "The selected game profile is marked unsupported for legacy PAK building.", true));

        var allFiles = Manifest.AssetGroups.SelectMany(static group => group.Files).ToArray();
        foreach (var duplicate in allFiles.GroupBy(static file => file.VirtualPath, StringComparer.OrdinalIgnoreCase)
                     .Where(static group => group.Count() > 1))
        {
            result.Issues.Add(new("DUPLICATE_PATH", $"The virtual path is included more than once: {duplicate.Key}", true));
        }

        foreach (var assetFile in allFiles)
        {
            string stagingPath;
            try
            {
                stagingPath = GetProjectContainedPath(assetFile.RelativeStagingPath);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                result.Issues.Add(new("UNSAFE_PATH", $"Unsafe staging path for {assetFile.VirtualPath}: {exception.Message}", true));
                continue;
            }

            if (!File.Exists(stagingPath))
            {
                result.Issues.Add(new("MISSING_FILE", $"A staged file is missing: {assetFile.VirtualPath}", true));
                continue;
            }

            var info = new FileInfo(stagingPath);
            if (info.Length != assetFile.Size)
                result.Issues.Add(new("SIZE_CHANGED", $"The staged file size changed outside the workspace: {assetFile.VirtualPath}", true));
            if (verifyHashes && !ComputeSha256(stagingPath).Equals(assetFile.Sha256, StringComparison.OrdinalIgnoreCase))
                result.Issues.Add(new("HASH_CHANGED", $"The staged file hash changed outside the workspace: {assetFile.VirtualPath}", true));
        }

        foreach (var group in Manifest.AssetGroups)
            ValidatePackageGroup(group, result);

        return result;
    }

    public IReadOnlyList<string> GetExpectedVirtualPaths() => Manifest.AssetGroups
        .SelectMany(static group => group.Files)
        .Select(static file => UnrealPath.NormalizeVirtualPath(file.VirtualPath))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public string GetBuildPakPath()
    {
        var name = UnrealPath.SanitizeIdentifier(Manifest.ModId, "FModelRecreateMod") + "_P.pak";
        return Path.Combine(BuildDirectory, name);
    }

    public string GetProjectContainedPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return UnrealPath.ToContainedPath(ProjectDirectory, normalized);
    }

    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string ComputeSha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private void ReplaceLogicalGroup(ModAssetGroup group)
    {
        var existing = Manifest.AssetGroups.FirstOrDefault(item =>
            item.LogicalPath.Equals(group.LogicalPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            group.GroupId = existing.GroupId;
            Manifest.AssetGroups.Remove(existing);
        }
        Manifest.AssetGroups.Add(group);
    }

    private static void ValidatePackageGroup(ModAssetGroup group, ModValidationResult result)
    {
        var extensions = group.Files.Select(static file => Path.GetExtension(file.VirtualPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasPackage = extensions.Contains(".uasset") || extensions.Contains(".umap");
        if (!hasPackage) return;

        var stems = group.Files
            .Where(static file => Path.GetExtension(file.VirtualPath) is ".uasset" or ".umap" ||
                                  Path.GetExtension(file.VirtualPath).Equals(".uasset", StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetExtension(file.VirtualPath).Equals(".umap", StringComparison.OrdinalIgnoreCase))
            .Select(static file => Path.ChangeExtension(file.VirtualPath, null))
            .ToArray();
        foreach (var sidecar in group.Files.Where(static file =>
                     Path.GetExtension(file.VirtualPath).Equals(".uexp", StringComparison.OrdinalIgnoreCase) ||
                     Path.GetExtension(file.VirtualPath).Equals(".ubulk", StringComparison.OrdinalIgnoreCase) ||
                     Path.GetExtension(file.VirtualPath).Equals(".uptnl", StringComparison.OrdinalIgnoreCase)))
        {
            var sidecarStem = Path.ChangeExtension(sidecar.VirtualPath, null);
            if (!stems.Contains(sidecarStem, StringComparer.OrdinalIgnoreCase))
                result.Issues.Add(new("ORPHAN_SIDECAR", $"A package sidecar has no matching .uasset/.umap: {sidecar.VirtualPath}", true));
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(ProjectDirectory);
        Directory.CreateDirectory(SourcesDirectory);
        Directory.CreateDirectory(StagingDirectory);
        Directory.CreateDirectory(BuildDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(InstallDirectory);
    }
}
