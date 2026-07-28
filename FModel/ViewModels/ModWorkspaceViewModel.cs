using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CUE4Parse.UE4.Pak;
using FModel.ModTools;
using FModel.Services;

namespace FModel.ViewModels;

public sealed class ModWorkspaceViewModel : ViewModel, IDisposable
{
    private readonly GameDirectoryAnalyzer _analyzer = new();
    private readonly SystemProcessRunner _processRunner = new();
    private readonly ModInstaller _installer = new();
    private readonly HttpClient _httpClient = new();
    private CancellationTokenSource? _operationCancellation;
    private GameAnalysisReport? _lastAnalysis;

    public ObservableCollection<ModAssetGroup> AssetGroups { get; } = [];
    public ObservableCollection<string> AnalysisMessages { get; } = [];
    public ObservableCollection<string> ValidationMessages { get; } = [];
    public IReadOnlyList<int> EngineMinorVersions { get; } = Enumerable.Range(0, 28).ToArray();
    public IReadOnlyList<ModBuildBackendKind> Backends { get; } = Enum.GetValues<ModBuildBackendKind>();
    public IReadOnlyList<string> Compressions { get; } = ["Zlib", "Gzip", "Zstd", "LZ4", "Oodle"];
    public IReadOnlyList<UnrealAssetConversionKind> ConversionKinds { get; } = Enum.GetValues<UnrealAssetConversionKind>();

    private string _gameDirectory = string.Empty;
    public string GameDirectory { get => _gameDirectory; set => SetProperty(ref _gameDirectory, value); }

    private int _engineMinor = 27;
    public int EngineMinor { get => _engineMinor; set => SetProperty(ref _engineMinor, value); }

    private string _projectDisplayName = "My Unreal Mod";
    public string ProjectDisplayName { get => _projectDisplayName; set => SetProperty(ref _projectDisplayName, value); }

    private string _projectDirectory = string.Empty;
    public string ProjectDirectory { get => _projectDirectory; private set => SetProperty(ref _projectDirectory, value); }

    private string _projectTitle = "No project open";
    public string ProjectTitle { get => _projectTitle; private set => SetProperty(ref _projectTitle, value); }

    private string _supportSummary = "Select a UE4 game directory and analyze it.";
    public string SupportSummary { get => _supportSummary; private set => SetProperty(ref _supportSummary, value); }

    private string _mountPoint = "../../../";
    public string MountPoint { get => _mountPoint; set => SetProperty(ref _mountPoint, value); }

    private string _paksDirectory = string.Empty;
    public string PaksDirectory { get => _paksDirectory; private set => SetProperty(ref _paksDirectory, value); }

    private string _repakVersion = "V11";
    public string RepakVersion { get => _repakVersion; set => SetProperty(ref _repakVersion, value); }

    private string _compression = "Zlib";
    public string Compression { get => _compression; set => SetProperty(ref _compression, value); }

    private ModBuildBackendKind _selectedBackend = ModBuildBackendKind.Repak;
    public ModBuildBackendKind SelectedBackend { get => _selectedBackend; set => SetProperty(ref _selectedBackend, value); }

    private string _unrealPakPath = string.Empty;
    public string UnrealPakPath { get => _unrealPakPath; set => SetProperty(ref _unrealPakPath, value); }

    private string _engineRoot = string.Empty;
    public string EngineRoot { get => _engineRoot; set => SetProperty(ref _engineRoot, value); }

    private string _conversionSource = string.Empty;
    public string ConversionSource { get => _conversionSource; set => SetProperty(ref _conversionSource, value); }

    private string _conversionTarget = string.Empty;
    public string ConversionTarget { get => _conversionTarget; set => SetProperty(ref _conversionTarget, value); }

    private UnrealAssetConversionKind _conversionKind = UnrealAssetConversionKind.Texture;
    public UnrealAssetConversionKind ConversionKind { get => _conversionKind; set => SetProperty(ref _conversionKind, value); }

    private string _statusText = "Ready";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private string _logText = string.Empty;
    public string LogText { get => _logText; private set => SetProperty(ref _logText, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaisePropertyChanged(nameof(IsNotBusy)); } }
    public bool IsNotBusy => !IsBusy;
    public bool HasProject => ApplicationService.ModWorkspace.CurrentProject is not null;

    public ModWorkspaceViewModel()
    {
        ApplicationService.ModWorkspace.ProjectChanged += OnProjectChanged;
        LoadCurrentProject();
    }

    public GameAnalysisReport AnalyzeCurrentGame()
    {
        var observations = ApplicationService.ApplicationView.CUE4Parse.Provider.MountedVfs
            .OfType<PakFileReader>()
            .Select(reader => new PakContainerObservation(
                reader.Name,
                reader.MountPoint,
                (int) reader.Info.Version,
                reader.Info.IsSubVersion,
                reader.Info.EncryptedIndex,
                reader.Info.Magic != CUE4Parse.UE4.Pak.Objects.FPakInfo.PAK_FILE_MAGIC,
                reader.CompressionMethods.Select(static method => method.ToString()).ToArray()))
            .ToArray();
        var report = _analyzer.Analyze(new GameAnalysisRequest
        {
            GameDirectory = GameDirectory,
            EngineVersion = new UnrealEngineVersion(4, EngineMinor),
            PakObservations = observations
        });
        _lastAnalysis = report;
        ApplyAnalysis(report);
        return report;
    }

    public void CreateProject(string parentDirectory)
    {
        var report = AnalyzeCurrentGame();
        var profile = GameModProfile.FromAnalysis(Path.GetFileName(Path.TrimEndingDirectorySeparator(GameDirectory)), report);
        profile.MountPoint = MountPoint;
        profile.RepakVersion = RepakVersion;
        profile.Compression = Compression;
        var project = ModProjectStore.Create(parentDirectory, ProjectDisplayName, profile);
        ApplicationService.ModWorkspace.SetProject(project);
        AppendLog($"Created project: {project.ProjectDirectory}");
    }

    public void OpenProject(string path)
    {
        var project = ModProjectStore.Load(path);
        ApplicationService.ModWorkspace.SetProject(project);
        AppendLog($"Opened project: {project.ProjectDirectory}");
    }

    public void SaveProfile()
    {
        var project = RequireProject();
        project.Manifest.Game.GameDirectory = GameDirectory;
        project.Manifest.Game.PaksDirectory = PaksDirectory;
        project.Manifest.Game.EngineVersion = new UnrealEngineVersion(4, EngineMinor);
        project.Manifest.Game.MountPoint = MountPoint;
        project.Manifest.Game.RepakVersion = RepakVersion;
        project.Manifest.Game.Compression = Compression;
        if (_lastAnalysis is not null)
        {
            project.Manifest.Game.ContainerKind = _lastAnalysis.ContainerKind;
            project.Manifest.Game.SupportLevel = _lastAnalysis.SupportLevel;
            project.Manifest.Game.HasEncryptedPakIndex = _lastAnalysis.HasEncryptedPakIndex;
            project.Manifest.Game.HasPakSignatures = _lastAnalysis.HasPakSignatures;
            project.Manifest.Game.UsesCustomPakFormat = _lastAnalysis.UsesCustomPakFormat;
        }
        project.Manifest.PreferredBackend = SelectedBackend;
        project.Save();
        AppendLog("Project profile saved.");
    }

    public ModValidationResult ValidateProject()
    {
        SaveProfile();
        var result = RequireProject().Validate();
        ValidationMessages.Clear();
        foreach (var issue in result.Issues)
            ValidationMessages.Add($"{(issue.IsError ? "ERROR" : "WARNING")} [{issue.Code}] {issue.Message}");
        if (result.Issues.Count == 0) ValidationMessages.Add("No validation issues were found.");
        StatusText = result.IsValid ? "Project validation passed" : "Project validation failed";
        return result;
    }

    public async Task<PakBuildResult> BuildAsync()
    {
        var validation = ValidateProject();
        if (!validation.IsValid)
            return new PakBuildResult { Errors = ["Project validation failed."] };

        return await RunBusyAsync("Building _P.pak", async cancellationToken =>
        {
            var project = RequireProject();
            string toolPath;
            IPakBuildBackend backend;
            if (SelectedBackend == ModBuildBackendKind.Repak)
            {
                toolPath = await new RepakToolManager(_httpClient)
                    .EnsureAvailableAsync(AppPaths.ToolsDirectory, cancellationToken).ConfigureAwait(false);
                backend = new RepakBackend(_processRunner);
            }
            else
            {
                toolPath = UnrealPakPath;
                backend = new UnrealPakBackend(_processRunner);
            }

            var request = new PakBuildRequest
            {
                ToolPath = toolPath,
                StagingDirectory = project.StagingDirectory,
                OutputPakPath = project.GetBuildPakPath(),
                MountPoint = MountPoint,
                PakVersion = RepakVersion,
                Compression = Compression,
                ExpectedVirtualPaths = project.GetExpectedVirtualPaths()
            };
            var result = await backend.BuildAsync(request, cancellationToken).ConfigureAwait(false);
            AppendLog(result.Log);
            if (result.Success)
            {
                project.Manifest.LastBuildPath = result.OutputPakPath;
                project.Manifest.LastBuildSha256 = result.Sha256;
                project.Save();
                AppendLog($"Build completed: {result.OutputPakPath}\nSHA-256: {result.Sha256}");
            }
            else
            {
                AppendLog("Build failed:\n" + string.Join("\n", result.Errors));
            }
            return result;
        }).ConfigureAwait(true);
    }

    public ModInstallResult Install()
    {
        var project = RequireProject();
        if (string.IsNullOrWhiteSpace(project.Manifest.LastBuildPath))
            return new(false, null, "Build the project before installation.");
        var result = _installer.Install(project, project.Manifest.LastBuildPath);
        AppendLog(result.Message);
        return result;
    }

    public ModInstallResult Uninstall(bool forceWhenModified = false)
    {
        var result = _installer.Uninstall(RequireProject(), forceWhenModified);
        AppendLog(result.Message);
        return result;
    }

    public void AddExternalFiles(string logicalPath, string[] filePaths, string virtualDirectory)
    {
        var project = RequireProject();
        var normalizedDirectory = UnrealPath.NormalizeVirtualPath(virtualDirectory).TrimEnd('/');
        var files = filePaths.Select(path => (path, $"{normalizedDirectory}/{Path.GetFileName(path)}"));
        project.StageExternalFiles(logicalPath, files);
        ApplicationService.ModWorkspace.SetProject(project);
        AppendLog($"Added {filePaths.Length} external cooked file(s).");
    }

    public void RemoveGroup(ModAssetGroup? group)
    {
        if (group is null) return;
        var project = RequireProject();
        project.RemoveGroup(group.GroupId);
        ApplicationService.ModWorkspace.SetProject(project);
        AppendLog($"Removed asset group: {group.LogicalPath}");
    }

    public async Task<UnrealEditorConversionResult> ConvertAndStageAsync()
    {
        return await RunBusyAsync("Converting source asset", async cancellationToken =>
        {
            var project = RequireProject();
            var pipeline = new UnrealEditorConversionPipeline(_processRunner);
            var result = await pipeline.ConvertAndCookAsync(new UnrealEditorConversionRequest
            {
                EngineRoot = EngineRoot,
                SourceFile = ConversionSource,
                TargetVirtualAssetPath = ConversionTarget,
                Kind = ConversionKind,
                WorkingDirectory = Path.Combine(project.ProjectDirectory, "Conversion")
            }, cancellationToken).ConfigureAwait(false);
            AppendLog(result.Log);
            if (!result.Success)
            {
                AppendLog("Conversion failed:\n" + string.Join("\n", result.Errors));
                return result;
            }

            var target = UnrealPath.NormalizeVirtualPath(ConversionTarget);
            var targetStem = Path.ChangeExtension(target, null)!;
            var staged = result.CookedFiles.Select(file =>
            {
                var extension = Path.GetExtension(file);
                return (file, targetStem + extension);
            });
            project.StageExternalFiles(target, staged, ModAssetSourceKind.UnrealEditorConversion);
            ApplicationService.ModWorkspace.SetProject(project);
            AppendLog($"Converted and staged {result.CookedFiles.Count} cooked file(s).");
            return result;
        }).ConfigureAwait(true);
    }

    public void AppendLog(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var line = $"[{DateTime.Now:HH:mm:ss}] {message.Trim()}";
        Application.Current?.Dispatcher.Invoke(() => LogText = string.IsNullOrEmpty(LogText) ? line : LogText + Environment.NewLine + line);
    }

    public void CancelOperation() => _operationCancellation?.Cancel();

    private async Task<T> RunBusyAsync<T>(string status, Func<CancellationToken, Task<T>> action)
    {
        if (IsBusy) throw new InvalidOperationException("Another Mod Workspace operation is already running.");
        IsBusy = true;
        StatusText = status;
        _operationCancellation = new CancellationTokenSource();
        try
        {
            var result = await action(_operationCancellation.Token).ConfigureAwait(true);
            StatusText = "Ready";
            return result;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation cancelled";
            AppendLog("Operation cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            StatusText = "Operation failed";
            AppendLog(exception.ToString());
            throw;
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            IsBusy = false;
        }
    }

    private void ApplyAnalysis(GameAnalysisReport report)
    {
        AnalysisMessages.Clear();
        foreach (var warning in report.Warnings) AnalysisMessages.Add("WARNING: " + warning);
        foreach (var blocker in report.BlockingReasons) AnalysisMessages.Add("BLOCKED: " + blocker);
        if (AnalysisMessages.Count == 0) AnalysisMessages.Add("No compatibility warnings were detected.");
        PaksDirectory = report.PaksDirectory ?? string.Empty;
        MountPoint = report.MountPoint;
        RepakVersion = report.RepakVersion;
        SupportSummary = $"{report.SupportLevel} · {report.ContainerKind} · UE {report.EngineVersion} · {report.PakFiles.Count} PAK";
    }

    private void LoadCurrentProject()
    {
        var project = ApplicationService.ModWorkspace.CurrentProject;
        AssetGroups.Clear();
        if (project is null)
        {
            ProjectTitle = "No project open";
            ProjectDirectory = string.Empty;
            RaisePropertyChanged(nameof(HasProject));
            return;
        }

        ProjectTitle = project.Manifest.DisplayName;
        ProjectDirectory = project.ProjectDirectory;
        ProjectDisplayName = project.Manifest.DisplayName;
        GameDirectory = project.Manifest.Game.GameDirectory;
        PaksDirectory = project.Manifest.Game.PaksDirectory;
        EngineMinor = project.Manifest.Game.EngineVersion.Minor;
        MountPoint = project.Manifest.Game.MountPoint;
        RepakVersion = project.Manifest.Game.RepakVersion;
        Compression = project.Manifest.Game.Compression;
        SelectedBackend = project.Manifest.PreferredBackend;
        SupportSummary = $"{project.Manifest.Game.SupportLevel} · {project.Manifest.Game.ContainerKind} · UE {project.Manifest.Game.EngineVersion}";
        foreach (var group in project.Manifest.AssetGroups.OrderBy(static group => group.LogicalPath, StringComparer.OrdinalIgnoreCase))
            AssetGroups.Add(group);
        RaisePropertyChanged(nameof(HasProject));
    }

    private ModProjectStore RequireProject() =>
        ApplicationService.ModWorkspace.CurrentProject ?? throw new InvalidOperationException("Create or open a mod project first.");

    private void OnProjectChanged(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.Invoke(LoadCurrentProject);

    public void Dispose()
    {
        ApplicationService.ModWorkspace.ProjectChanged -= OnProjectChanged;
        _operationCancellation?.Dispose();
        _httpClient.Dispose();
    }
}
