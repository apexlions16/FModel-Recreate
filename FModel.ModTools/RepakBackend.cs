namespace FModel.ModTools;

public interface IPakBuildBackend
{
    Task<PakBuildResult> BuildAsync(PakBuildRequest request, CancellationToken cancellationToken);
}

public sealed class RepakBackend(IProcessRunner processRunner) : IPakBuildBackend
{
    private static readonly HashSet<string> Versions =
        ["V0", "V1", "V2", "V3", "V4", "V5", "V6", "V7", "V8A", "V8B", "V9", "V10", "V11"];
    private static readonly HashSet<string> Compressions =
        ["Zlib", "Gzip", "Oodle", "Zstd", "LZ4"];

    public async Task<PakBuildResult> BuildAsync(PakBuildRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = ValidateRequest(request);
        if (errors.Count > 0) return new PakBuildResult { Errors = errors };

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputPakPath))!);
        if (File.Exists(request.OutputPakPath)) File.Delete(request.OutputPakPath);

        var arguments = new List<string>
        {
            "pack",
            "--mount-point", UnrealPath.NormalizeMountPoint(request.MountPoint),
            "--version", request.PakVersion
        };
        if (!string.IsNullOrWhiteSpace(request.Compression))
        {
            arguments.Add("--compression");
            arguments.Add(request.Compression);
        }
        arguments.Add(Path.GetFullPath(request.StagingDirectory));
        arguments.Add(Path.GetFullPath(request.OutputPakPath));

        var pack = await processRunner.RunAsync(request.ToolPath, arguments,
            Path.GetDirectoryName(Path.GetFullPath(request.OutputPakPath)), cancellationToken).ConfigureAwait(false);
        var log = $"PACK\n{pack.StandardOutput}\n{pack.StandardError}";
        if (!pack.IsSuccess || !File.Exists(request.OutputPakPath))
        {
            return new PakBuildResult
            {
                Errors = [$"repak pack failed with exit code {pack.ExitCode}.", pack.StandardError.Trim()],
                Log = log
            };
        }

        var info = await processRunner.RunAsync(request.ToolPath,
            ["info", Path.GetFullPath(request.OutputPakPath)], null, cancellationToken).ConfigureAwait(false);
        var list = await processRunner.RunAsync(request.ToolPath,
            ["list", Path.GetFullPath(request.OutputPakPath)], null, cancellationToken).ConfigureAwait(false);
        log += $"\nINFO\n{info.StandardOutput}\n{info.StandardError}\nLIST\n{list.StandardOutput}\n{list.StandardError}";
        if (!info.IsSuccess || !list.IsSuccess)
        {
            return new PakBuildResult
            {
                Errors = ["The generated PAK could not be inspected by repak."],
                Log = log
            };
        }

        var listedFiles = list.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(UnrealPath.NormalizeVirtualPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expectedFiles = request.ExpectedVirtualPaths.Select(UnrealPath.NormalizeVirtualPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var missing = expectedFiles.Except(listedFiles, StringComparer.OrdinalIgnoreCase).ToArray();
        var unexpected = listedFiles.Except(expectedFiles, StringComparer.OrdinalIgnoreCase).ToArray();
        if (missing.Length > 0 || unexpected.Length > 0)
        {
            var mismatchErrors = new List<string>();
            if (missing.Length > 0) mismatchErrors.Add("Missing PAK entries: " + string.Join(", ", missing));
            if (unexpected.Length > 0) mismatchErrors.Add("Unexpected PAK entries: " + string.Join(", ", unexpected));
            return new PakBuildResult { Errors = mismatchErrors, ListedFiles = listedFiles, Log = log };
        }

        return new PakBuildResult
        {
            Success = true,
            OutputPakPath = Path.GetFullPath(request.OutputPakPath),
            Sha256 = ModProjectStore.ComputeSha256(request.OutputPakPath),
            ListedFiles = listedFiles,
            Log = log
        };
    }

    public static IReadOnlyList<string> CreatePackArguments(PakBuildRequest request)
    {
        var arguments = new List<string>
        {
            "pack", "--mount-point", UnrealPath.NormalizeMountPoint(request.MountPoint),
            "--version", request.PakVersion
        };
        if (!string.IsNullOrWhiteSpace(request.Compression))
        {
            arguments.Add("--compression");
            arguments.Add(request.Compression);
        }
        arguments.Add(Path.GetFullPath(request.StagingDirectory));
        arguments.Add(Path.GetFullPath(request.OutputPakPath));
        return arguments;
    }

    private static List<string> ValidateRequest(PakBuildRequest request)
    {
        var errors = new List<string>();
        if (!File.Exists(request.ToolPath)) errors.Add("repak.exe could not be found.");
        if (!Directory.Exists(request.StagingDirectory)) errors.Add("The staging directory does not exist.");
        if (!Versions.Contains(request.PakVersion)) errors.Add($"Unsupported repak version: {request.PakVersion}");
        if (!string.IsNullOrWhiteSpace(request.Compression) && !Compressions.Contains(request.Compression))
            errors.Add($"Unsupported repak compression: {request.Compression}");
        if (request.ExpectedVirtualPaths.Count == 0) errors.Add("The PAK build contains no files.");
        return errors;
    }
}
