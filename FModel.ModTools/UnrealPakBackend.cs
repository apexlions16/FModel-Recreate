using System.Text;

namespace FModel.ModTools;

public sealed class UnrealPakBackend(IProcessRunner processRunner) : IPakBuildBackend
{
    public async Task<PakBuildResult> BuildAsync(PakBuildRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.ToolPath))
            return new PakBuildResult { Errors = ["UnrealPak.exe could not be found."] };
        if (!Directory.Exists(request.StagingDirectory))
            return new PakBuildResult { Errors = ["The staging directory does not exist."] };
        if (request.ExpectedVirtualPaths.Count == 0)
            return new PakBuildResult { Errors = ["The PAK build contains no files."] };

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputPakPath))!);
        var responsePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(request.OutputPakPath))!, "UnrealPakResponse.txt");
        File.WriteAllText(responsePath, CreateResponseFile(request), new UTF8Encoding(false));
        if (File.Exists(request.OutputPakPath)) File.Delete(request.OutputPakPath);

        var arguments = new List<string>
        {
            Path.GetFullPath(request.OutputPakPath),
            $"-Create={responsePath}",
            "-UTF8Output"
        };
        if (!string.IsNullOrWhiteSpace(request.Compression)) arguments.Add("-compress");

        var build = await processRunner.RunAsync(request.ToolPath, arguments,
            Path.GetDirectoryName(Path.GetFullPath(request.OutputPakPath)), cancellationToken).ConfigureAwait(false);
        var log = $"BUILD\n{build.StandardOutput}\n{build.StandardError}";
        if (!build.IsSuccess || !File.Exists(request.OutputPakPath))
            return new PakBuildResult
            {
                Errors = [$"UnrealPak build failed with exit code {build.ExitCode}.", build.StandardError.Trim()],
                Log = log
            };

        var list = await processRunner.RunAsync(request.ToolPath,
            [Path.GetFullPath(request.OutputPakPath), "-List", "-UTF8Output"], null, cancellationToken).ConfigureAwait(false);
        log += $"\nLIST\n{list.StandardOutput}\n{list.StandardError}";
        if (!list.IsSuccess)
            return new PakBuildResult { Errors = ["UnrealPak could not list the generated PAK."], Log = log };

        foreach (var expected in request.ExpectedVirtualPaths)
        {
            if (!list.StandardOutput.Contains(UnrealPath.NormalizeVirtualPath(expected), StringComparison.OrdinalIgnoreCase))
                return new PakBuildResult { Errors = [$"The generated PAK is missing {expected}."], Log = log };
        }

        return new PakBuildResult
        {
            Success = true,
            OutputPakPath = Path.GetFullPath(request.OutputPakPath),
            Sha256 = ModProjectStore.ComputeSha256(request.OutputPakPath),
            ListedFiles = request.ExpectedVirtualPaths,
            Log = log
        };
    }

    public static string CreateResponseFile(PakBuildRequest request)
    {
        var mount = UnrealPath.NormalizeMountPoint(request.MountPoint);
        var lines = request.ExpectedVirtualPaths.Select(virtualPath =>
        {
            var normalized = UnrealPath.NormalizeVirtualPath(virtualPath);
            var source = UnrealPath.ToContainedPath(request.StagingDirectory, normalized);
            if (!File.Exists(source)) throw new FileNotFoundException("A staged file is missing.", source);
            return $"\"{source}\" \"{mount}{normalized}\"";
        });
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
