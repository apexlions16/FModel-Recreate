using System.Text.Json;

namespace FModel.ModTools;

public sealed class UnrealEditorConversionPipeline(IProcessRunner processRunner)
{
    public async Task<UnrealEditorConversionResult> ConvertAndCookAsync(
        UnrealEditorConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = Validate(request);
        if (errors.Count > 0) return new UnrealEditorConversionResult { Errors = errors };

        var editorCommand = LocateEditorCommand(request.EngineRoot);
        if (editorCommand is null)
            return new UnrealEditorConversionResult { Errors = ["UE4Editor-Cmd.exe could not be found in the selected engine directory."] };

        var projectRootName = UnrealPath.SanitizeIdentifier(UnrealPath.GetProjectRootName(request.TargetVirtualAssetPath), "FMRModProject");
        var workRoot = Path.Combine(Path.GetFullPath(request.WorkingDirectory), projectRootName + "_FMR_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workRoot);
        var uprojectPath = Path.Combine(workRoot, projectRootName + ".uproject");
        var scriptPath = Path.Combine(workRoot, "ImportAsset.py");
        File.WriteAllText(uprojectPath, CreateProjectDescriptor(request.EngineRoot));
        File.WriteAllText(scriptPath, CreateImportScript(request));

        var importArguments = CreateImportArguments(uprojectPath, scriptPath);
        var import = await processRunner.RunAsync(editorCommand, importArguments, workRoot, cancellationToken).ConfigureAwait(false);
        var log = $"IMPORT\n{import.StandardOutput}\n{import.StandardError}";
        if (!import.IsSuccess || !import.StandardOutput.Contains("FMODEL_RECREATE_IMPORT_OK", StringComparison.Ordinal))
        {
            return new UnrealEditorConversionResult
            {
                Errors = [$"Unreal Editor import failed with exit code {import.ExitCode}."],
                Log = log
            };
        }

        var cookArguments = CreateCookArguments(uprojectPath);
        var cook = await processRunner.RunAsync(editorCommand, cookArguments, workRoot, cancellationToken).ConfigureAwait(false);
        log += $"\nCOOK\n{cook.StandardOutput}\n{cook.StandardError}";
        if (!cook.IsSuccess)
        {
            return new UnrealEditorConversionResult
            {
                Errors = [$"Unreal Editor cook failed with exit code {cook.ExitCode}."],
                Log = log
            };
        }

        var normalized = UnrealPath.NormalizeVirtualPath(request.TargetVirtualAssetPath);
        var contentMarker = normalized.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
        var relativeAfterContent = normalized[(contentMarker + "/Content/".Length)..];
        var relativeWithoutExtension = Path.ChangeExtension(relativeAfterContent, null)!.Replace('/', Path.DirectorySeparatorChar);
        var cookedContentRoot = Path.Combine(workRoot, "Saved", "Cooked", "WindowsNoEditor", projectRootName, "Content");
        var cookedBase = Path.Combine(cookedContentRoot, relativeWithoutExtension);
        var cookedFiles = new[] { ".uasset", ".uexp", ".ubulk", ".uptnl" }
            .Select(extension => cookedBase + extension)
            .Where(File.Exists)
            .ToArray();
        if (!cookedFiles.Any(static path => Path.GetExtension(path).Equals(".uasset", StringComparison.OrdinalIgnoreCase)))
        {
            return new UnrealEditorConversionResult
            {
                Errors = ["Cooking completed but the expected .uasset was not produced."],
                Log = log
            };
        }

        return new UnrealEditorConversionResult
        {
            Success = true,
            CookedAssetDirectory = cookedContentRoot,
            CookedFiles = cookedFiles,
            Log = log
        };
    }

    public static string? LocateEditorCommand(string engineRoot)
    {
        if (string.IsNullOrWhiteSpace(engineRoot)) return null;
        var root = Path.GetFullPath(engineRoot);
        var candidates = new[]
        {
            root,
            Path.Combine(root, "UE4Editor-Cmd.exe"),
            Path.Combine(root, "Engine", "Binaries", "Win64", "UE4Editor-Cmd.exe"),
            Path.Combine(root, "Binaries", "Win64", "UE4Editor-Cmd.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public static IReadOnlyList<string> CreateImportArguments(string uprojectPath, string scriptPath) =>
    [
        Path.GetFullPath(uprojectPath),
        "-run=pythonscript",
        $"-script={Path.GetFullPath(scriptPath)}",
        "-unattended",
        "-nop4",
        "-nosplash",
        "-stdout",
        "-FullStdOutLogOutput"
    ];

    public static IReadOnlyList<string> CreateCookArguments(string uprojectPath) =>
    [
        Path.GetFullPath(uprojectPath),
        "-run=cook",
        "-targetplatform=WindowsNoEditor",
        "-unversioned",
        "-unattended",
        "-nop4",
        "-stdout",
        "-FullStdOutLogOutput"
    ];

    public static string CreateImportScript(UnrealEditorConversionRequest request)
    {
        var assetPath = UnrealPath.GetGameAssetPath(request.TargetVirtualAssetPath);
        var separator = assetPath.LastIndexOf('/');
        var destinationPath = assetPath[..separator];
        var destinationName = assetPath[(separator + 1)..];
        var sourceLiteral = JsonSerializer.Serialize(Path.GetFullPath(request.SourceFile).Replace('\\', '/'));
        var destinationLiteral = JsonSerializer.Serialize(destinationPath);
        var nameLiteral = JsonSerializer.Serialize(destinationName);
        var replace = request.ReplaceExisting ? "True" : "False";
        var postImport = request.Kind switch
        {
            UnrealAssetConversionKind.Texture => "\nasset = task.get_objects()[0] if task.get_objects() else None\nif asset:\n    unreal.EditorAssetLibrary.save_loaded_asset(asset, only_if_is_dirty=False)\n",
            UnrealAssetConversionKind.Audio => "\nasset = task.get_objects()[0] if task.get_objects() else None\nif asset:\n    unreal.EditorAssetLibrary.save_loaded_asset(asset, only_if_is_dirty=False)\n",
            _ => string.Empty
        };

        return $"""
import unreal

task = unreal.AssetImportTask()
task.filename = {sourceLiteral}
task.destination_path = {destinationLiteral}
task.destination_name = {nameLiteral}
task.automated = True
task.replace_existing = {replace}
task.replace_existing_settings = {replace}
task.save = True

unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
if not task.imported_object_paths:
    raise RuntimeError('Unreal did not import the requested asset.')
{postImport}
print('FMODEL_RECREATE_IMPORT_OK')
""";
    }

    public static string CreateProjectDescriptor(string engineRoot)
    {
        var version = DetectEngineAssociation(engineRoot);
        return JsonSerializer.Serialize(new
        {
            FileVersion = 3,
            EngineAssociation = version,
            Category = "FModel-Recreate",
            Description = "Temporary isolated project generated for cooked replacement conversion.",
            Plugins = new[]
            {
                new { Name = "PythonScriptPlugin", Enabled = true },
                new { Name = "EditorScriptingUtilities", Enabled = true }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string DetectEngineAssociation(string engineRoot)
    {
        var folder = new DirectoryInfo(Path.GetFullPath(engineRoot)).Name;
        var marker = folder.IndexOf("4.", StringComparison.Ordinal);
        if (marker >= 0)
        {
            var candidate = new string(folder[marker..].TakeWhile(static character => char.IsDigit(character) || character == '.').ToArray()).TrimEnd('.');
            if (candidate.Count(static character => character == '.') == 1) return candidate;
        }
        return "4.27";
    }

    private static List<string> Validate(UnrealEditorConversionRequest request)
    {
        var errors = new List<string>();
        if (!File.Exists(request.SourceFile)) errors.Add("The source file does not exist.");
        try
        {
            var virtualPath = UnrealPath.NormalizeVirtualPath(request.TargetVirtualAssetPath);
            if (!Path.GetExtension(virtualPath).Equals(".uasset", StringComparison.OrdinalIgnoreCase))
                errors.Add("The conversion target must be a .uasset virtual path.");
            _ = UnrealPath.GetGameAssetPath(virtualPath);
        }
        catch (ArgumentException exception)
        {
            errors.Add(exception.Message);
        }

        var extension = Path.GetExtension(request.SourceFile);
        if (request.Kind == UnrealAssetConversionKind.Texture &&
            extension is not (".png" or ".tga" or ".dds") &&
            !extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".tga", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".dds", StringComparison.OrdinalIgnoreCase))
            errors.Add("Texture conversion supports PNG, TGA and DDS sources.");
        if (request.Kind == UnrealAssetConversionKind.Audio && !extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            errors.Add("Audio conversion supports WAV sources.");
        return errors;
    }
}
