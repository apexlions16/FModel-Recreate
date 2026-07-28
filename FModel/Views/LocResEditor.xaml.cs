using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using FModel.ModTools;
using FModel.Services;
using Microsoft.Win32;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;

namespace FModel.Views;

public partial class LocResEditor
{
    private readonly string _sourcePath;
    private readonly string _virtualPath;
    private readonly LocResCodec _codec = new();
    private readonly LocResDocument _document;
    private readonly ObservableCollection<LocResEntry> _entries;
    private readonly ICollectionView _view;

    public LocResEditor(string sourcePath, string virtualPath)
    {
        InitializeComponent();
        _sourcePath = Path.GetFullPath(sourcePath);
        _virtualPath = UnrealPath.NormalizeVirtualPath(virtualPath);
        using var stream = File.OpenRead(_sourcePath);
        _document = _codec.Read(stream);
        _entries = new ObservableCollection<LocResEntry>(_document.Entries);
        _view = CollectionViewSource.GetDefaultView(_entries);
        EntriesGrid.ItemsSource = _view;
        InfoText.Text = $"{_document.Version} · {_virtualPath}";
        UpdateCount();
    }

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        _view.Filter = item => item is LocResEntry entry &&
            (string.IsNullOrEmpty(query) || entry.Namespace.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             entry.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             entry.LocalizedString.Contains(query, StringComparison.OrdinalIgnoreCase));
        _view.Refresh();
        UpdateCount();
    }

    private void OnSaveCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                FileName = Path.GetFileName(_sourcePath),
                Filter = "Unreal localization resource (*.locres)|*.locres",
                InitialDirectory = Path.GetDirectoryName(_sourcePath)
            };
            if (dialog.ShowDialog(this) != true) return;
            SaveDocument(dialog.FileName);
            MessageBox.Show("LocRes copy saved.", "LocRes Editor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void OnSaveAndStage(object sender, RoutedEventArgs e)
    {
        try
        {
            var project = ApplicationService.ModWorkspace.CurrentProject ?? throw new InvalidOperationException("Create or open a mod project first.");
            var tempPath = Path.Combine(project.SourcesDirectory, $"{Guid.NewGuid():N}-{Path.GetFileName(_sourcePath)}");
            SaveDocument(tempPath);
            project.StageExternalFiles(_virtualPath, [(tempPath, _virtualPath)], ModAssetSourceKind.LocResEditor);
            ApplicationService.ModWorkspace.SetProject(project);
            MessageBox.Show("LocRes was compiled and staged in the current mod project.", "LocRes Editor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void SaveDocument(string path)
    {
        _document.Entries.Clear();
        foreach (var entry in _entries) _document.Entries.Add(entry);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        _codec.Write(stream, _document);
    }

    private void UpdateCount() => CountText.Text = $"{_view.Cast<object>().Count()} / {_entries.Count}";

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static void ShowError(Exception exception) =>
        MessageBox.Show(exception.GetBaseException().Message, "LocRes Editor", MessageBoxButton.OK, MessageBoxImage.Error);
}
