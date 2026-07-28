using System.Text.Json;

namespace FModel.ModTools;

public sealed class ModInstaller
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ModInstallResult Install(ModProjectStore project, string pakPath, bool replaceExistingManagedInstall = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        var sourcePak = Path.GetFullPath(pakPath);
        if (!File.Exists(sourcePak)) return new(false, null, "The built PAK file does not exist.");
        if (project.Manifest.Game.SupportLevel == ModSupportLevel.Unsupported)
            return new(false, null, "The game profile is marked unsupported; installation was blocked.");

        var paksDirectory = project.Manifest.Game.PaksDirectory;
        if (string.IsNullOrWhiteSpace(paksDirectory) || !Directory.Exists(paksDirectory))
            return new(false, null, "The target Content/Paks directory does not exist.");

        var targetName = UnrealPath.SanitizeIdentifier(project.Manifest.ModId, "FModelRecreateMod") + "_P.pak";
        var targetPath = Path.Combine(Path.GetFullPath(paksDirectory), targetName);
        var manifestPath = GetInstallManifestPath(project);
        var previous = LoadInstallManifest(manifestPath);

        if (File.Exists(targetPath))
        {
            if (previous is null || !Path.GetFullPath(previous.InstalledPakPath).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                return new(false, null, "A file with the target mod name already exists and is not managed by this project.");
            if (!replaceExistingManagedInstall)
                return new(false, null, "This mod is already installed.");

            var currentHash = ModProjectStore.ComputeSha256(targetPath);
            if (!currentHash.Equals(previous.InstalledSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, null, "The installed PAK was modified outside FModel-Recreate. It will not be overwritten automatically.");

            var backupDirectory = Path.Combine(project.InstallDirectory, "Backups");
            Directory.CreateDirectory(backupDirectory);
            var backupPath = Path.Combine(backupDirectory, $"{Path.GetFileNameWithoutExtension(targetName)}-{DateTime.UtcNow:yyyyMMddHHmmss}.pak");
            File.Copy(targetPath, backupPath, false);
        }

        var tempPath = targetPath + ".fmr.tmp";
        try
        {
            File.Copy(sourcePak, tempPath, true);
            var sourceHash = ModProjectStore.ComputeSha256(sourcePak);
            var copiedHash = ModProjectStore.ComputeSha256(tempPath);
            if (!sourceHash.Equals(copiedHash, StringComparison.OrdinalIgnoreCase))
                return new(false, null, "The PAK changed while it was being copied; installation was aborted.");

            File.Move(tempPath, targetPath, true);
            var installed = new InstalledModManifest
            {
                ProjectId = project.Manifest.ProjectId,
                ModId = project.Manifest.ModId,
                InstalledPakPath = targetPath,
                InstalledSha256 = sourceHash,
                InstalledUtc = DateTimeOffset.UtcNow
            };
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            var manifestTemp = manifestPath + ".tmp";
            File.WriteAllText(manifestTemp, JsonSerializer.Serialize(installed, JsonOptions));
            File.Move(manifestTemp, manifestPath, true);
            return new(true, targetPath, $"Installed {targetName}.");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new(false, null, $"Windows denied access to the game directory: {exception.Message}");
        }
        catch (IOException exception)
        {
            return new(false, null, $"The PAK could not be installed: {exception.Message}");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public ModInstallResult Uninstall(ModProjectStore project, bool forceWhenModified = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        var manifestPath = GetInstallManifestPath(project);
        var installed = LoadInstallManifest(manifestPath);
        if (installed is null) return new(false, null, "This project has no managed installation record.");
        if (installed.ProjectId != project.Manifest.ProjectId)
            return new(false, null, "The installation record belongs to another project.");

        var targetPath = Path.GetFullPath(installed.InstalledPakPath);
        if (!File.Exists(targetPath))
        {
            File.Delete(manifestPath);
            return new(true, targetPath, "The PAK was already absent; the stale installation record was removed.");
        }

        var currentHash = ModProjectStore.ComputeSha256(targetPath);
        if (!forceWhenModified && !currentHash.Equals(installed.InstalledSha256, StringComparison.OrdinalIgnoreCase))
            return new(false, targetPath, "The installed PAK was modified outside FModel-Recreate. Uninstall was blocked to avoid deleting another file.");

        try
        {
            File.Delete(targetPath);
            File.Delete(manifestPath);
            return new(true, targetPath, "The managed mod PAK was removed.");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new(false, targetPath, $"Windows denied access while uninstalling: {exception.Message}");
        }
        catch (IOException exception)
        {
            return new(false, targetPath, $"The PAK could not be removed: {exception.Message}");
        }
    }

    public InstalledModManifest? GetInstalledState(ModProjectStore project) =>
        LoadInstallManifest(GetInstallManifestPath(project));

    private static string GetInstallManifestPath(ModProjectStore project) =>
        Path.Combine(project.InstallDirectory, "installed.json");

    private static InstalledModManifest? LoadInstallManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return null;
        try
        {
            return JsonSerializer.Deserialize<InstalledModManifest>(File.ReadAllText(manifestPath));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
