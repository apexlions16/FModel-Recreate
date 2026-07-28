using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FModel.ModTools;
using FModel.Services;
using FModel.ViewModels;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;

namespace FModel.Views;

public partial class ModWorkspace
{
    private readonly ModWorkspaceViewModel _viewModel;

    public ModWorkspace()
    {
        InitializeComponent();
        DataContext = _viewModel = new ModWorkspaceViewModel();
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnBrowseGame(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Select the Unreal Engine game directory or its Content/Paks directory.",
            ShowNewFolderButton = false,
            SelectedPath = Directory.Exists(_viewModel.GameDirectory) ? _viewModel.GameDirectory : string.Empty
        };
        if (dialog.ShowDialog(this) == true) _viewModel.GameDirectory = dialog.SelectedPath;
    }

    private void OnBrowseUnrealPak(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "UnrealPak.exe|UnrealPak.exe|Executable files (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) _viewModel.UnrealPakPath = dialog.FileName;
    }

    private void OnBrowseEngine(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Select the Unreal Engine 4 installation root.",
            ShowNewFolderButton = false,
            SelectedPath = Directory.Exists(_viewModel.EngineRoot) ? _viewModel.EngineRoot : string.Empty
        };
        if (dialog.ShowDialog(this) == true) _viewModel.EngineRoot = dialog.SelectedPath;
    }

    private void OnBrowseConversionSource(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Supported sources|*.png;*.tga;*.dds;*.wav|Texture files|*.png;*.tga;*.dds|Wave audio|*.wav|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true) _viewModel.ConversionSource = dialog.FileName;
    }

    private void OnNewProject(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_viewModel.GameDirectory) || !Directory.Exists(_viewModel.GameDirectory))
            {
                ShowError("Select a valid game directory first.");
                return;
            }

            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select the parent directory where the mod project will be created.",
                ShowNewFolderButton = true,
                SelectedPath = AppPaths.ModProjectsDirectory
            };
            if (dialog.ShowDialog(this) != true) return;
            _viewModel.CreateProject(dialog.SelectedPath);
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private void OnOpenProject(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open ModProject.json",
                Filter = "FModel-Recreate mod project (ModProject.json)|ModProject.json|JSON files (*.json)|*.json",
                InitialDirectory = AppPaths.ModProjectsDirectory,
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == true) _viewModel.OpenProject(dialog.FileName);
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private void OnOpenProjectFolder(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_viewModel.ProjectDirectory)) return;
        Process.Start(new ProcessStartInfo { FileName = _viewModel.ProjectDirectory, UseShellExecute = true });
    }

    private void OnAnalyze(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.AnalyzeCurrentGame();
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private void OnSaveProfile(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.SaveProfile();
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private void OnValidate(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = _viewModel.ValidateProject();
            MessageBox.Show(result.IsValid ? "Project validation passed." : "Project validation failed. Review the validation list.",
                "Mod Workspace", MessageBoxButton.OK, result.IsValid ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private async void OnBuild(object sender, RoutedEventArgs e)
    {
        await GuardAsync(async () =>
        {
            var result = await _viewModel.BuildAsync();
            MessageBox.Show(result.Success ? $"PAK build completed.\n\n{result.OutputPakPath}" : string.Join(Environment.NewLine, result.Errors),
                "Mod Workspace", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        });
    }

    private void OnInstall(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = _viewModel.Install();
            MessageBox.Show(result.Message, "Mod Workspace", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private void OnUninstall(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = _viewModel.Uninstall();
            MessageBox.Show(result.Message, "Mod Workspace", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private void OnAddCookedFiles(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Cooked Unreal files|*.uasset;*.umap;*.uexp;*.ubulk;*.uptnl;*.locres;*.wem;*.bnk|All files|*.*",
                Multiselect = true,
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true) return;

            var commonStem = Path.GetFileNameWithoutExtension(dialog.FileNames[0]);
            var virtualDialog = new TextPromptDialog("Virtual Directory", "Enter the Unreal virtual directory. Example: GameName/Content/UI", "Game/Content") { Owner = this };
            if (virtualDialog.ShowDialog() != true) return;
            _viewModel.AddExternalFiles(commonStem, dialog.FileNames, virtualDialog.Value);
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private void OnOpenLocRes(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog { Filter = "Unreal localization resource (*.locres)|*.locres", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            var virtualDialog = new TextPromptDialog("LocRes Virtual Path", "Enter the full virtual .locres path inside the PAK.", $"Game/Content/Localization/Game/tr/{Path.GetFileName(dialog.FileName)}") { Owner = this };
            if (virtualDialog.ShowDialog() != true) return;
            new LocResEditor(dialog.FileName, virtualDialog.Value) { Owner = this }.ShowDialog();
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private void OnRemoveSelected(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.RemoveGroup(AssetsGrid.SelectedItem as ModAssetGroup);
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private async void OnConvert(object sender, RoutedEventArgs e)
    {
        await GuardAsync(async () =>
        {
            var result = await _viewModel.ConvertAndStageAsync();
            MessageBox.Show(result.Success ? "Conversion completed and cooked files were staged." : string.Join(Environment.NewLine, result.Errors),
                "Mod Workspace", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        });
    }

    private void OnCancel(object sender, RoutedEventArgs e) => _viewModel.CancelOperation();

    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // The status bar and log already show cancellation.
        }
        catch (Exception exception)
        {
            ShowException(exception);
        }
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "Mod Workspace", MessageBoxButton.OK, MessageBoxImage.Error);

    private void ShowException(Exception exception)
    {
        _viewModel.AppendLog(exception.ToString());
        ShowError(exception.GetBaseException().Message);
    }
}
