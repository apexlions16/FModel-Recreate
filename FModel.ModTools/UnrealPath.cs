namespace FModel.ModTools;

public static class UnrealPath
{
    public static string NormalizeVirtualPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The Unreal virtual path cannot be empty.", nameof(value));

        var normalized = value.Trim().Replace('\\', '/');
        while (normalized.StartsWith('/')) normalized = normalized[1..];

        if (normalized.Length == 0 || Path.IsPathRooted(normalized) || normalized.Contains(':'))
            throw new ArgumentException($"Unsafe Unreal virtual path: {value}", nameof(value));

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(static segment => segment is "." or ".."))
            throw new ArgumentException($"Unsafe Unreal virtual path: {value}", nameof(value));

        return string.Join('/', segments);
    }

    public static string NormalizeMountPoint(string? value)
    {
        var mount = string.IsNullOrWhiteSpace(value) ? "../../../" : value.Trim().Replace('\\', '/');
        if (!mount.EndsWith('/')) mount += '/';
        if (mount.Contains("..//", StringComparison.Ordinal))
            throw new ArgumentException($"Invalid mount point: {value}", nameof(value));
        return mount;
    }

    public static string ToContainedPath(string rootDirectory, string virtualPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        var normalized = NormalizeVirtualPath(virtualPath);
        var combined = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The virtual path escaped the staging directory: {virtualPath}");
        return combined;
    }

    public static string SanitizeIdentifier(string value, string fallback = "mod")
    {
        var chars = value.Trim().Select(static character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_').ToArray();
        var result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    public static string GetProjectRootName(string virtualAssetPath)
    {
        var normalized = NormalizeVirtualPath(virtualAssetPath);
        var marker = normalized.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
        if (marker <= 0)
            throw new ArgumentException("The target path must follow <Project>/Content/...", nameof(virtualAssetPath));
        return normalized[..marker];
    }

    public static string GetGameAssetPath(string virtualAssetPath)
    {
        var normalized = NormalizeVirtualPath(virtualAssetPath);
        var marker = normalized.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
        if (marker <= 0)
            throw new ArgumentException("The target path must follow <Project>/Content/...", nameof(virtualAssetPath));
        var afterContent = normalized[(marker + "/Content/".Length)..];
        var withoutExtension = afterContent[..afterContent.LastIndexOf('.')];
        return "/Game/" + withoutExtension;
    }
}
