using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CUE4Parse.FileProvider.Objects;
using FModel.ModTools;
using FModel.Views;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;

namespace FModel.Services;

public sealed class ModWorkspaceService
{
    private readonly SemaphoreSlim _stageLock = new(1, 1);

    public ModProjectStore? CurrentProject { get; private set; }
    public event EventHandler? ProjectChanged;

    public void SetProject(ModProjectStore? project)
    {
        CurrentProject = project;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OpenWorkspace()
    {
        Helper.OpenWindow<AdonisUI.Controls.AdonisWindow>("Mod Workspace", () => new ModWorkspace().Show());
    }

    public async Task StageGameFilesAsync(IEnumerable<GameFile> selectedFiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedFiles);
        var files = selectedFiles.Where(static file => file is not null).DistinctBy(static file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0) return;

        if (CurrentProject is null)
        {
            OpenWorkspace();
            MessageBox.Show(
                "Create or open a mod project before adding assets.",
                "Mod Workspace",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await _stageLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var providerFiles = ApplicationService.ApplicationView.CUE4Parse.Provider.Files;
            foreach (var selected in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var groupFiles = ResolvePackageGroup(selected, providerFiles.Values);
                var data = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var gameFile in groupFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bytes = await gameFile.SafeReadAsync().ConfigureAwait(false);
                    if (bytes is null)
                        throw new InvalidDataException($"Could not read {gameFile.Path} from the mounted archive.");
                    data[gameFile.Path] = bytes;
                }

                CurrentProject.StagePackage(selected.Path, data, ModAssetSourceKind.ExtractedPackage);
            }
        }
        finally
        {
            _stageLock.Release();
        }

        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<GameFile> ResolvePackageGroup(GameFile selected, IEnumerable<GameFile> allFiles)
    {
        if (!selected.IsUePackage && !selected.IsUePackagePayload)
            return [selected];

        var stem = selected.PathWithoutExtension;
        var candidates = allFiles
            .Where(file => file.PathWithoutExtension.Equals(stem, StringComparison.OrdinalIgnoreCase))
            .Where(file => file.IsUePackage || file.IsUePackagePayload)
            .OrderBy(static file => file.Extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return candidates.Length > 0 ? candidates : [selected];
    }
}
