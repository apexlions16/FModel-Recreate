using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FModel.Views;

namespace FModel.Localization;

public sealed record LanguageOption(string CultureName, string DisplayName);

public static class LocalizationManager
{
    private const string LanguageSelectorTag = "FModelRecreateLanguageSelector";

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = LocalizationCatalog.English,
            ["tr-TR"] = LocalizationCatalog.Turkish
        };

    private static readonly Dictionary<string, string> ValueToKey = BuildValueIndex();
    private static bool _initialized;

    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new("en-US", "English"),
        new("tr-TR", "Türkçe")
    ];

    public static string CurrentCultureName { get; private set; } = "en-US";

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var saved = File.Exists(AppPaths.UiLanguageFile)
            ? File.ReadAllText(AppPaths.UiLanguageFile).Trim()
            : string.Empty;

        if (!Catalogs.ContainsKey(saved))
        {
            saved = CultureInfo.CurrentUICulture.Name.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
                ? "tr-TR"
                : "en-US";
        }

        SetLanguage(saved, persist: false);
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is not Window window) return;
                if (window is SettingsView settingsView) EnsureSettingsLanguageSelector(settingsView);
                Apply(window);
            }));
    }

    public static string Get(string englishText)
    {
        if (!Catalogs.TryGetValue(CurrentCultureName, out var selected)) return englishText;
        return selected.TryGetValue(englishText, out var translated) ? translated : englishText;
    }

    public static void SetLanguage(string cultureName, bool persist = true)
    {
        if (!Catalogs.ContainsKey(cultureName)) cultureName = "en-US";
        CurrentCultureName = cultureName;

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        if (persist)
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            File.WriteAllText(AppPaths.UiLanguageFile, cultureName);
        }

        ApplyToOpenWindows();
    }

    public static void ApplyToOpenWindows()
    {
        if (Application.Current is null) return;
        foreach (Window window in Application.Current.Windows)
        {
            if (window is SettingsView settingsView) EnsureSettingsLanguageSelector(settingsView);
            Apply(window);
        }
    }

    public static void Apply(DependencyObject root)
    {
        ApplyRecursive(root, new HashSet<DependencyObject>());
    }

    private static void EnsureSettingsLanguageSelector(SettingsView settingsView)
    {
        if (settingsView.Content is not Grid rootGrid) return;
        if (FindByTag(rootGrid, LanguageSelectorTag) is not null) return;

        var footer = rootGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 1);
        if (footer?.Child is not Grid footerGrid) return;

        var label = new TextBlock
        {
            Text = Get("Interface Language"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var languageBox = new ComboBox
        {
            ItemsSource = AvailableLanguages,
            DisplayMemberPath = nameof(LanguageOption.DisplayName),
            SelectedValuePath = nameof(LanguageOption.CultureName),
            SelectedValue = CurrentCultureName,
            MinWidth = 125
        };

        languageBox.SelectionChanged += (_, _) =>
        {
            if (languageBox.SelectedItem is not LanguageOption option ||
                option.CultureName == CurrentCultureName) return;

            SetLanguage(option.CultureName);
            label.Text = Get("Interface Language");
        };

        var panel = new StackPanel
        {
            Tag = LanguageSelectorTag,
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { label, languageBox }
        };
        Grid.SetColumn(panel, 0);
        Panel.SetZIndex(panel, 2);
        footerGrid.Children.Add(panel);
    }

    private static FrameworkElement FindByTag(DependencyObject root, object tag)
    {
        if (root is FrameworkElement element && Equals(element.Tag, tag)) return element;

        var count = 0;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch (InvalidOperationException)
        {
            // Some logical-only objects do not have a visual tree.
        }

        for (var i = 0; i < count; i++)
        {
            var found = FindByTag(VisualTreeHelper.GetChild(root, i), tag);
            if (found is not null) return found;
        }

        return null;
    }

    private static void ApplyRecursive(DependencyObject current, ISet<DependencyObject> visited)
    {
        if (!visited.Add(current)) return;

        switch (current)
        {
            case Window window when !BindingOperations.IsDataBound(window, Window.TitleProperty):
                window.Title = Translate(window.Title);
                break;
            case TextBlock textBlock when !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty):
                textBlock.Text = Translate(textBlock.Text);
                break;
            case HeaderedContentControl headered when headered.Header is string header &&
                                                       !BindingOperations.IsDataBound(headered, HeaderedContentControl.HeaderProperty):
                headered.Header = Translate(header);
                break;
            case ContentControl contentControl when contentControl.Content is string content &&
                                                     !BindingOperations.IsDataBound(contentControl, ContentControl.ContentProperty):
                contentControl.Content = Translate(content);
                break;
            case Separator separator when separator.Tag is string tag:
                separator.Tag = Translate(tag);
                break;
        }

        if (current is FrameworkElement element && element.ToolTip is string toolTip &&
            !BindingOperations.IsDataBound(element, FrameworkElement.ToolTipProperty))
        {
            element.ToolTip = Translate(toolTip);
        }

        foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
            ApplyRecursive(child, visited);

        int visualChildren;
        try
        {
            visualChildren = VisualTreeHelper.GetChildrenCount(current);
        }
        catch (InvalidOperationException)
        {
            visualChildren = 0;
        }

        for (var i = 0; i < visualChildren; i++)
            ApplyRecursive(VisualTreeHelper.GetChild(current, i), visited);
    }

    private static string Translate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (!ValueToKey.TryGetValue(value, out var key)) key = value;
        return Get(key);
    }

    private static Dictionary<string, string> BuildValueIndex()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var catalog in Catalogs.Values)
        foreach (var pair in catalog)
        {
            result.TryAdd(pair.Key, pair.Key);
            result.TryAdd(pair.Value, pair.Key);
        }

        return result;
    }
}
