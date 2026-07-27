$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Utf8([string]$Path, [string]$Content) {
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    [System.IO.File]::WriteAllText((Join-Path $PWD $Path), $Content, [System.Text.UTF8Encoding]::new($false))
}

function Replace-Required([string]$Path, [string]$Old, [string]$New) {
    $full = Join-Path $PWD $Path
    $content = [System.IO.File]::ReadAllText($full)
    if (-not $content.Contains($Old)) { throw "Required text was not found in $Path`n$Old" }
    [System.IO.File]::WriteAllText($full, $content.Replace($Old, $New), [System.Text.UTF8Encoding]::new($false))
}

function Replace-RegexRequired([string]$Path, [string]$Pattern, [string]$Replacement) {
    $full = Join-Path $PWD $Path
    $content = [System.IO.File]::ReadAllText($full)
    $updated = [regex]::Replace($content, $Pattern, $Replacement, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    if ($updated -eq $content) { throw "Required pattern was not found in $Path`n$Pattern" }
    [System.IO.File]::WriteAllText($full, $updated, [System.Text.UTF8Encoding]::new($false))
}

# -----------------------------------------------------------------------------
# Project identity and metadata
# -----------------------------------------------------------------------------
$csproj = 'FModel/FModel.csproj'
Replace-Required $csproj '<Version>4.4.4.0</Version>' '<Version>0.1.0</Version>'
Replace-Required $csproj '<AssemblyVersion>4.4.4.0</AssemblyVersion>' '<AssemblyVersion>0.1.0.0</AssemblyVersion>'
Replace-Required $csproj '<FileVersion>4.4.4.0</FileVersion>' '<FileVersion>0.1.0.0</FileVersion>'
Replace-Required $csproj '<StartupObject>FModel.App</StartupObject>' @'
<StartupObject>FModel.App</StartupObject>
    <AssemblyName>FModel-Recreate</AssemblyName>
    <RootNamespace>FModel</RootNamespace>
    <Product>FModel-Recreate</Product>
    <AssemblyTitle>FModel-Recreate</AssemblyTitle>
    <Description>An Unreal Engine archive explorer and future modding workspace based on FModel and CUE4Parse.</Description>
    <Authors>apexlions16 and FModel contributors</Authors>
    <Company>FModel-Recreate</Company>
    <RepositoryUrl>https://github.com/apexlions16/FModel-Recreate</RepositoryUrl>
    <PackageProjectUrl>https://github.com/apexlions16/FModel-Recreate</PackageProjectUrl>
    <NeutralLanguage>en-US</NeutralLanguage>
    <InformationalVersion>$(Version)+$(SourceRevisionId)</InformationalVersion>
'@

Replace-Required 'FModel/App.xaml' '#206BD4' '#7657D5'

# -----------------------------------------------------------------------------
# Application paths: isolate the fork while migrating existing FModel settings.
# -----------------------------------------------------------------------------
Write-Utf8 'FModel/AppPaths.cs' @'
using System;
using System.IO;

namespace FModel;

public static class AppPaths
{
    public const string AppFolderName = "FModel-Recreate";
    public const string LegacyAppFolderName = "FModel";

    public static readonly string AppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

    public static readonly string LegacyAppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyAppFolderName);

    public static readonly string UiLanguageFile = Path.Combine(AppDataDirectory, "ui-language.txt");
    public static readonly string FirstRunMarker = Path.Combine(AppDataDirectory, "first-run-v1.complete");

    public static bool MigrateLegacyData()
    {
        Directory.CreateDirectory(AppDataDirectory);
        if (!Directory.Exists(LegacyAppDataDirectory)) return false;

        var migrated = false;
        foreach (var fileName in new[] { "AppSettings.json", "AppSettings_Debug.json" })
        {
            var source = Path.Combine(LegacyAppDataDirectory, fileName);
            var destination = Path.Combine(AppDataDirectory, fileName);
            if (!File.Exists(source) || File.Exists(destination)) continue;

            File.Copy(source, destination, false);
            migrated = true;
        }

        if (migrated && !File.Exists(FirstRunMarker))
            File.WriteAllText(FirstRunMarker, DateTimeOffset.UtcNow.ToString("O"));

        return migrated;
    }
}
'@

Replace-RegexRequired 'FModel/Settings/UserSettings.cs' '#if DEBUG\r?\n\s*public static readonly string FilePath = Path\.Combine\(Environment\.GetFolderPath\(Environment\.SpecialFolder\.ApplicationData\), "FModel", "AppSettings_Debug\.json"\);\r?\n#else\r?\n\s*public static readonly string FilePath = Path\.Combine\(Environment\.GetFolderPath\(Environment\.SpecialFolder\.ApplicationData\), "FModel", "AppSettings\.json"\);\r?\n#endif' @'
#if DEBUG
        public static readonly string FilePath = Path.Combine(AppPaths.AppDataDirectory, "AppSettings_Debug.json");
#else
        public static readonly string FilePath = Path.Combine(AppPaths.AppDataDirectory, "AppSettings.json");
#endif
'@

# -----------------------------------------------------------------------------
# Centralized English/Turkish UI catalog and runtime localization.
# -----------------------------------------------------------------------------
Write-Utf8 'FModel/Localization/LocalizationCatalog.cs' @'
using System.Collections.Generic;

namespace FModel.Localization;

internal static class LocalizationCatalog
{
    public static IReadOnlyDictionary<string, string> English { get; } = BuildEnglish();

    public static IReadOnlyDictionary<string, string> Turkish { get; } = new Dictionary<string, string>
    {
        ["About"] = "Hakkında",
        ["About FModel"] = "FModel-Recreate Hakkında",
        ["ADVANCED"] = "GELİŞMİŞ",
        ["AES"] = "AES",
        ["All Files (*.*)"] = "Tüm Dosyalar (*.*)",
        ["Archive Directory *"] = "Arşiv Klasörü *",
        ["Archive Mount Point"] = "Arşiv Bağlama Noktası",
        ["Archive Version"] = "Arşiv Sürümü",
        ["Archives"] = "Arşivler",
        ["Archives Info"] = "Arşiv Bilgileri",
        ["Audio Player"] = "Ses Oynatıcı",
        ["Backup"] = "Yedekle",
        ["Bring Selected Folder To View"] = "Seçili Klasörü Görünüme Getir",
        ["Bugs Report"] = "Hata Bildir",
        ["Cancel"] = "İptal",
        ["Collapse All"] = "Tümünü Daralt",
        ["Compressed Audio"] = "Sıkıştırılmış Ses",
        ["Contributors"] = "Katkıda Bulunanlar",
        ["Creator"] = "Oluşturucu",
        ["Description"] = "Açıklama",
        ["Directory"] = "Klasör",
        ["Directory Selector"] = "Klasör Seçici",
        ["Discord Rich Presence"] = "Discord Etkinlik Durumu",
        ["Discord Server"] = "Discord Sunucusu",
        ["Donate"] = "Destek Ol",
        ["Donators"] = "Destekçiler",
        ["English"] = "English",
        ["Fatal Error"] = "Kritik Hata",
        ["Favorite Directories"] = "Favori Klasörler",
        ["Folders"] = "Klasörler",
        ["GAME"] = "OYUN",
        ["General"] = "Genel",
        ["Global Unique Identifier"] = "Genel Benzersiz Kimlik",
        ["Help"] = "Yardım",
        ["Image Merger"] = "Görsel Birleştirici",
        ["Included In Archive"] = "Bulunduğu Arşiv",
        ["Information"] = "Bilgi",
        ["INFORMATION"] = "BİLGİ",
        ["Interface Language"] = "Arayüz Dili",
        ["Is Encrypted"] = "Şifreli mi",
        ["Keep Directory Structure"] = "Klasör Yapısını Koru",
        ["Keybindings"] = "Kısayollar",
        ["Language changes are applied immediately."] = "Dil değişiklikleri anında uygulanır.",
        ["Load"] = "Yükle",
        ["Loading Mode"] = "Yükleme Modu",
        ["Local Mapping File (drag & drop)"] = "Yerel Mapping Dosyası (sürükle ve bırak)",
        ["Mapping File Path"] = "Mapping Dosyası Yolu",
        ["Models"] = "Modeller",
        ["Mount Point"] = "Bağlama Noktası",
        ["Next"] = "İleri",
        ["No"] = "Hayır",
        ["None"] = "Yok",
        ["OK"] = "Tamam",
        ["Open"] = "Aç",
        ["Output Directory *"] = "Çıktı Klasörü *",
        ["Packages"] = "Paketler",
        ["Packages Count"] = "Paket Sayısı",
        ["Packages Language"] = "Paket Dili",
        ["Powered by"] = "Altyapı",
        ["Preview New Explorer System"] = "Yeni Gezgin Sistemini Önizle",
        ["References"] = "Referanslar",
        ["Release"] = "Kararlı",
        ["Releases"] = "Sürümler",
        ["Reset Settings"] = "Ayarları Sıfırla",
        ["Restart"] = "Yeniden Başlat",
        ["Save Audio Directory *"] = "Ses Kaydetme Klasörü *",
        ["Save Properties Directory *"] = "Özellik Kaydetme Klasörü *",
        ["Save Texture Directory *"] = "Doku Kaydetme Klasörü *",
        ["Search"] = "Ara",
        ["Select a mapping file"] = "Bir mapping dosyası seçin",
        ["Selector"] = "Seçici",
        ["Settings"] = "Ayarlar",
        ["Settings saved."] = "Ayarlar kaydedildi.",
        ["Start"] = "Başla",
        ["Themes"] = "Temalar",
        ["Texture Platform *"] = "Doku Platformu *",
        ["Turkish"] = "Türkçe",
        ["UE Versions *"] = "UE Sürümleri *",
        ["Unknown"] = "Bilinmiyor",
        ["Unluac"] = "Unluac",
        ["Views"] = "Görünümler",
        ["Welcome to FModel-Recreate"] = "FModel-Recreate'a Hoş Geldiniz",
        ["Yes"] = "Evet",
        ["3D Viewer"] = "3B Görüntüleyici",
        ["* May Require a restart for changes to take effect"] = "* Bazı değişikliklerin uygulanması için yeniden başlatma gerekebilir",
        ["Choose the interface language. The game directory selector will open next."] = "Arayüz dilini seçin. Ardından oyun klasörü seçicisi açılacaktır.",
        ["FModel-Recreate is an Unreal Engine archive explorer based on FModel and CUE4Parse. This release establishes an independent update channel, branding and localization foundation."] = "FModel-Recreate; FModel ve CUE4Parse tabanlı bir Unreal Engine arşiv gezginidir. Bu sürüm bağımsız güncelleme kanalını, marka kimliğini ve yerelleştirme temelini oluşturur.",
        ["Built by apexlions16. Based on the GPL-3.0 licensed FModel project by 4sval and its contributors."] = "apexlions16 tarafından geliştirilmektedir. 4sval ve katkıcılarının GPL-3.0 lisanslı FModel projesini temel alır.",
        ["It looks like you just changed something.\nFModel-Recreate will restart to apply your changes."] = "Bir ayarı değiştirdiniz.\nDeğişiklikleri uygulamak için FModel-Recreate yeniden başlatılacak.",
        ["A restart is needed"] = "Yeniden başlatma gerekiyor",
        ["FModel-Recreate cannot create the output directory where it is currently located. Please move FModel-Recreate.exe to a different location."] = "FModel-Recreate bulunduğu konumda çıktı klasörü oluşturamıyor. Lütfen FModel-Recreate.exe dosyasını farklı bir konuma taşıyın.",
        ["An unexpected error occurred. You can reset settings, restart the application, or ignore the error."] = "Beklenmeyen bir hata oluştu. Ayarları sıfırlayabilir, uygulamayı yeniden başlatabilir veya hatayı yok sayabilirsiniz.",
        ["A newer FModel-Recreate release is available. Open the GitHub release page?"] = "Daha yeni bir FModel-Recreate sürümü mevcut. GitHub sürüm sayfası açılsın mı?",
        ["Update available"] = "Güncelleme mevcut"
    };

    private static IReadOnlyDictionary<string, string> BuildEnglish()
    {
        var result = new Dictionary<string, string>();
        foreach (var key in Turkish.Keys) result[key] = key;
        return result;
    }
}
'@

Write-Utf8 'FModel/Localization/LocalizationManager.cs' @'
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

namespace FModel.Localization;

public sealed record LanguageOption(string CultureName, string DisplayName);

public static class LocalizationManager
{
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
            saved = CultureInfo.CurrentUICulture.Name.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
                ? "tr-TR"
                : "en-US";

        SetLanguage(saved, persist: false);
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window) Apply(window);
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
        foreach (Window window in Application.Current.Windows) Apply(window);
    }

    public static void Apply(DependencyObject root)
    {
        var visited = new HashSet<DependencyObject>();
        ApplyRecursive(root, visited);
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
            element.ToolTip = Translate(toolTip);

        foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
            ApplyRecursive(child, visited);

        var visualChildren = 0;
        try { visualChildren = VisualTreeHelper.GetChildrenCount(current); }
        catch (InvalidOperationException) { }

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
'@

Write-Utf8 'FModel/Views/FirstRunWizard.cs' @'
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FModel.Localization;

namespace FModel.Views;

public sealed class FirstRunWizard : Window
{
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly Button _continueButton;

    public FirstRunWizard()
    {
        Title = "FModel-Recreate";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _title = new TextBlock
        {
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _description = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var languageLabel = new TextBlock { Margin = new Thickness(0, 0, 0, 6) };
        var languageBox = new ComboBox
        {
            ItemsSource = LocalizationManager.AvailableLanguages,
            DisplayMemberPath = nameof(LanguageOption.DisplayName),
            SelectedValuePath = nameof(LanguageOption.CultureName),
            SelectedValue = LocalizationManager.CurrentCultureName,
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 24)
        };

        _continueButton = new Button
        {
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };

        languageBox.SelectionChanged += (_, _) =>
        {
            if (languageBox.SelectedItem is not LanguageOption option) return;
            LocalizationManager.SetLanguage(option.CultureName);
            RefreshText(languageLabel);
        };
        _continueButton.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            File.WriteAllText(AppPaths.FirstRunMarker, DateTimeOffset.UtcNow.ToString("O"));
            DialogResult = true;
        };

        Content = new StackPanel
        {
            Margin = new Thickness(32),
            Children = { _title, _description, languageLabel, languageBox, _continueButton }
        };

        RefreshText(languageLabel);
    }

    private void RefreshText(TextBlock languageLabel)
    {
        _title.Text = LocalizationManager.Get("Welcome to FModel-Recreate");
        _description.Text = LocalizationManager.Get("Choose the interface language. The game directory selector will open next.");
        languageLabel.Text = LocalizationManager.Get("Interface Language");
        _continueButton.Content = LocalizationManager.Get("Start");
        LocalizationManager.Apply(this);
    }
}
'@

# Inject localization and first-run flow into App.xaml.cs.
Replace-Required 'FModel/App.xaml.cs' 'using FModel.Framework;' "using FModel.Framework;`r`nusing FModel.Localization;`r`nusing FModel.Views;"
Replace-Required 'FModel/App.xaml.cs' '        base.OnStartup(e);' "        base.OnStartup(e);`r`n`r`n        AppPaths.MigrateLegacyData();"
Replace-Required 'FModel/App.xaml.cs' @'
        catch
        {
            UserSettings.Default = new UserSettings();
        }

        var createMe = false;
'@ @'
        catch
        {
            UserSettings.Default = new UserSettings();
        }

        LocalizationManager.Initialize();
        if (!File.Exists(AppPaths.FirstRunMarker))
            new FirstRunWizard().ShowDialog();

        var createMe = false;
'@
Replace-Required 'FModel/App.xaml.cs' 'FModel cannot create the output directory where it is currently located. Please move FModel.exe to a different location.' 'FModel-Recreate cannot create the output directory where it is currently located. Please move FModel-Recreate.exe to a different location.'
Replace-Required 'FModel/App.xaml.cs' 'Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FModel"));' 'Directory.CreateDirectory(AppPaths.AppDataDirectory);'
Replace-Required 'FModel/App.xaml.cs' 'FModel-Debug-Log-' 'FModel-Recreate-Debug-Log-'
Replace-Required 'FModel/App.xaml.cs' 'FModel-Log-' 'FModel-Recreate-Log-'
Replace-Required 'FModel/App.xaml.cs' '            Text = $"An unhandled {e.Exception.GetBaseException().GetType()} occurred: {e.Exception.Message}",' '            Text = $"{LocalizationManager.Get("An unexpected error occurred. You can reset settings, restart the application, or ignore the error.")}\n\n{e.Exception.GetBaseException().GetType()}: {e.Exception.Message}",'
Replace-Required 'FModel/App.xaml.cs' '            Caption = "Fatal Error",' '            Caption = LocalizationManager.Get("Fatal Error"),'
Replace-Required 'FModel/App.xaml.cs' 'MessageBoxButtons.Custom("Reset Settings", EErrorKind.ResetSettings)' 'MessageBoxButtons.Custom(LocalizationManager.Get("Reset Settings"), EErrorKind.ResetSettings)'
Replace-Required 'FModel/App.xaml.cs' 'MessageBoxButtons.Custom("Restart", EErrorKind.Restart)' 'MessageBoxButtons.Custom(LocalizationManager.Get("Restart"), EErrorKind.Restart)'

# Add a language selector to the existing settings footer without rewriting the large XAML file.
Replace-Required 'FModel/Views/SettingsView.xaml.cs' 'using FModel.Framework;' "using FModel.Framework;`r`nusing FModel.Localization;"
Replace-Required 'FModel/Views/SettingsView.xaml.cs' @'
        foreach (var item in SettingsTree.Items)
        {
            if (item is not TreeViewItem { Visibility: Visibility.Visible } treeItem) continue;
            treeItem.IsSelected = i == UserSettings.Default.LastOpenedSettingTab;
            i++;
        }
    }

    private async void OnClick(object sender, RoutedEventArgs e)
'@ @'
        foreach (var item in SettingsTree.Items)
        {
            if (item is not TreeViewItem { Visibility: Visibility.Visible } treeItem) continue;
            treeItem.IsSelected = i == UserSettings.Default.LastOpenedSettingTab;
            i++;
        }

        AddLanguageSelector();
        LocalizationManager.Apply(this);
    }

    private void AddLanguageSelector()
    {
        if (Content is not Grid rootGrid) return;
        var footer = rootGrid.Children.OfType<Border>().FirstOrDefault(x => Grid.GetRow(x) == 1);
        if (footer?.Child is not Grid footerGrid) return;

        var label = new TextBlock
        {
            Text = LocalizationManager.Get("Interface Language"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var languageBox = new ComboBox
        {
            ItemsSource = LocalizationManager.AvailableLanguages,
            DisplayMemberPath = nameof(LanguageOption.DisplayName),
            SelectedValuePath = nameof(LanguageOption.CultureName),
            SelectedValue = LocalizationManager.CurrentCultureName,
            MinWidth = 125
        };
        languageBox.SelectionChanged += (_, _) =>
        {
            if (languageBox.SelectedItem is not LanguageOption option ||
                option.CultureName == LocalizationManager.CurrentCultureName) return;

            LocalizationManager.SetLanguage(option.CultureName);
            label.Text = LocalizationManager.Get("Interface Language");
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { label, languageBox }
        };
        Grid.SetColumn(panel, 0);
        footerGrid.Children.Add(panel);
    }

    private async void OnClick(object sender, RoutedEventArgs e)
'@
Replace-Required 'FModel/Views/SettingsView.xaml.cs' '            Title = "Select a mapping file",' '            Title = LocalizationManager.Get("Select a mapping file"),'

# -----------------------------------------------------------------------------
# Fork-owned links, title, updater and About screen.
# -----------------------------------------------------------------------------
Write-Utf8 'FModel/Constants.cs' @'
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.Utils;

namespace FModel;

public static class Constants
{
    public const string APP_NAME = "FModel-Recreate";
    public static readonly string APP_PATH = Path.GetFullPath(Environment.GetCommandLineArgs()[0]);
    public static readonly string APP_VERSION = FileVersionInfo.GetVersionInfo(APP_PATH).FileVersion ?? "0.0.0.0";
    public static readonly string APP_COMMIT_ID = FileVersionInfo.GetVersionInfo(APP_PATH).ProductVersion?.SubstringAfter('+') ?? "release";
    public static readonly string APP_SHORT_COMMIT_ID = APP_COMMIT_ID.Length > 7 ? APP_COMMIT_ID[..7] : APP_COMMIT_ID;
    public static readonly DateTime APP_BUILD_DATE = File.GetLastWriteTime(APP_PATH);

    public const string ZERO_64_CHAR = "0000000000000000000000000000000000000000000000000000000000000000";
    public static readonly FGuid ZERO_GUID = new(0U);

    public const float SCALE_DOWN_RATIO = 0.01F;
    public const int SAMPLES_COUNT = 4;

    public const string WHITE = "#DAE5F2";
    public const string GRAY = "#BBBBBB";
    public const string RED = "#E06C75";
    public const string GREEN = "#98C379";
    public const string YELLOW = "#E5C07B";
    public const string BLUE = "#7657D5";

    public const string REPOSITORY_URL = "https://github.com/apexlions16/FModel-Recreate";
    public const string UPSTREAM_REPOSITORY_URL = "https://github.com/4sval/FModel";
    public const string ISSUE_LINK = REPOSITORY_URL + "/discussions";
    public const string GH_REPO = "https://api.github.com/repos/apexlions16/FModel-Recreate";
    public const string GH_COMMITS_HISTORY = GH_REPO + "/commits";
    public const string GH_RELEASES = GH_REPO + "/releases";
    public const string RELEASES_PAGE = REPOSITORY_URL + "/releases";
    public const string DONATE_LINK = REPOSITORY_URL;
    public const string DISCORD_LINK = REPOSITORY_URL + "/discussions";

    public const string _FN_LIVE_TRIGGER = "fortnite-live.manifest";
    public const string _VAL_LIVE_TRIGGER = "valorant-live.manifest";
    public const string _NO_PRESET_TRIGGER = "Hand Made";

    public const string MAPPING_ISSUE_LINK = ISSUE_LINK;
    public const string AUDIO_ISSUE_LINK = ISSUE_LINK;
    public const string RADA_ISSUE_LINK = ISSUE_LINK;
    public const string VERSION_ISSUE_LINK = ISSUE_LINK;

    public static int PALETTE_LENGTH => COLOR_PALETTE.Length;
    public static readonly Vector3[] COLOR_PALETTE =
    {
        new (0.231f, 0.231f, 0.231f),
        new (0.376f, 0.490f, 0.545f),
        new (0.957f, 0.263f, 0.212f),
        new (0.196f, 0.804f, 0.196f),
        new (0.957f, 0.647f, 0.212f),
        new (0.612f, 0.153f, 0.690f),
        new (0.129f, 0.588f, 0.953f),
        new (1.000f, 0.920f, 0.424f),
        new (0.824f, 0.412f, 0.118f),
        new (0.612f, 0.800f, 0.922f)
    };
}
'@

Replace-Required 'FModel/ViewModels/ApplicationViewModel.cs' '    public string InitialWindowTitle => $"FModel ({Constants.APP_SHORT_COMMIT_ID} - {Constants.APP_BUILD_DATE:MMM d, yyyy})";' '    public string InitialWindowTitle => $"{Constants.APP_NAME} ({Constants.APP_SHORT_COMMIT_ID} - {Constants.APP_BUILD_DATE:MMM d, yyyy})";'
Replace-Required 'FModel/ViewModels/ApplicationViewModel.cs' 'MessageBox.Show("It looks like you just changed something.\nFModel will restart to apply your changes.", "Uh oh, a restart is needed", MessageBoxButton.OK, MessageBoxImage.Warning);' 'MessageBox.Show(Localization.LocalizationManager.Get("It looks like you just changed something.\nFModel-Recreate will restart to apply your changes."), Localization.LocalizationManager.Get("A restart is needed"), MessageBoxButton.OK, MessageBoxImage.Warning);'

Write-Utf8 'FModel/Services/GitHubUpdateService.cs' @'
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using FModel.Localization;
using FModel.Settings;
using Newtonsoft.Json.Linq;

namespace FModel.Services;

public static class GitHubUpdateService
{
    private static readonly HttpClient Client = CreateClient();

    public static async Task CheckForUpdatesAsync(bool force = false)
    {
        if (!force && UserSettings.Default.NextUpdateCheck > DateTime.Now) return;

        try
        {
            var json = await Client.GetStringAsync("https://api.github.com/repos/apexlions16/FModel-Recreate/releases/latest");
            var release = JObject.Parse(json);
            var tag = release.Value<string>("tag_name")?.TrimStart('v');
            var url = release.Value<string>("html_url") ?? Constants.RELEASES_PAGE;

            UserSettings.Default.LastUpdateCheck = DateTime.Now;
            UserSettings.Default.NextUpdateCheck = DateTime.Now.AddHours(12);

            if (!Version.TryParse(tag, out var latest)) return;
            if (!Version.TryParse(Constants.APP_VERSION, out var current)) current = new Version(0, 0);
            if (latest <= current) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = System.Windows.MessageBox.Show(
                    LocalizationManager.Get("A newer FModel-Recreate release is available. Open the GitHub release page?"),
                    LocalizationManager.Get("Update available"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            });
        }
        catch
        {
            UserSettings.Default.NextUpdateCheck = DateTime.Now.AddHours(2);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FModel-Recreate", "0.1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
'@

Replace-Required 'FModel/MainWindow.xaml.cs' '        ApplicationService.ApiEndpointView.FModelApi.CheckForUpdates(true);' '        _ = GitHubUpdateService.CheckForUpdatesAsync(true);'

Write-Utf8 'FModel/Views/About.xaml' @'
<adonisControls:AdonisWindow x:Class="FModel.Views.About"
                              xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                              xmlns:local="clr-namespace:FModel"
                              xmlns:adonisControls="clr-namespace:AdonisUI.Controls;assembly=AdonisUI"
                              WindowStartupLocation="CenterScreen" ResizeMode="NoResize"
                              IconVisibility="Collapsed" Width="560" SizeToContent="Height"
                              Loaded="OnLoaded">
    <adonisControls:AdonisWindow.Style>
        <Style TargetType="adonisControls:AdonisWindow" BasedOn="{StaticResource {x:Type adonisControls:AdonisWindow}}">
            <Setter Property="Title" Value="About FModel" />
        </Style>
    </adonisControls:AdonisWindow.Style>
    <StackPanel Margin="32 18">
        <StackPanel HorizontalAlignment="Center" Margin="0 0 0 26">
            <TextBlock Text="{Binding Source={x:Static local:Constants.APP_VERSION}, StringFormat={}FModel-Recreate {0}}" FontSize="18" FontWeight="600" Foreground="#9D8BE8" />
            <TextBlock Text="Built by apexlions16" FontSize="28" FontWeight="700" Foreground="#DAE5F2" HorizontalAlignment="Center" />
            <TextBlock Text="Based on FModel by 4sval and contributors" FontSize="12" Foreground="#8D93A1" HorizontalAlignment="Center" />
        </StackPanel>

        <TextBlock Text="Description" FontSize="15" FontWeight="700" Foreground="#9D8BE8" HorizontalAlignment="Center" />
        <TextBlock FontSize="12" Foreground="#A4A7AE" TextWrapping="Wrap" Margin="0 8 0 24" Text="{Binding DescriptionLabel}" />

        <TextBlock Text="Contributors" FontSize="15" FontWeight="700" Foreground="#9D8BE8" HorizontalAlignment="Center" />
        <TextBlock FontSize="12" Foreground="#A4A7AE" TextWrapping="Wrap" Margin="0 8 0 24" Text="{Binding ContributorsLabel}" />

        <TextBlock Text="Powered by" FontSize="15" FontWeight="700" Foreground="#9D8BE8" HorizontalAlignment="Center" />
        <TextBlock FontSize="12" Foreground="#A4A7AE" TextWrapping="Wrap" Margin="0 8 0 0" Text="{Binding ReferencesLabel}" />
    </StackPanel>
</adonisControls:AdonisWindow>
'@

Write-Utf8 'FModel/ViewModels/AboutViewModel.cs' @'
using System.Threading.Tasks;
using FModel.Framework;
using FModel.Localization;

namespace FModel.ViewModels;

public class AboutViewModel : ViewModel
{
    private string _descriptionLabel;
    public string DescriptionLabel
    {
        get => _descriptionLabel;
        set => SetProperty(ref _descriptionLabel, value);
    }

    private string _contributorsLabel;
    public string ContributorsLabel
    {
        get => _contributorsLabel;
        set => SetProperty(ref _contributorsLabel, value);
    }

    private string _referencesLabel;
    public string ReferencesLabel
    {
        get => _referencesLabel;
        set => SetProperty(ref _referencesLabel, value);
    }

    public Task Initialize()
    {
        DescriptionLabel = LocalizationManager.Get("FModel-Recreate is an Unreal Engine archive explorer based on FModel and CUE4Parse. This release establishes an independent update channel, branding and localization foundation.");
        ContributorsLabel = LocalizationManager.Get("Built by apexlions16. Based on the GPL-3.0 licensed FModel project by 4sval and its contributors.");
        ReferencesLabel = "FModel, CUE4Parse, Adonis UI, AvalonEdit, CSCore, EpicManifestParser, OpenTK, Newtonsoft.Json, Serilog, SkiaSharp, vgmstream";
        return Task.CompletedTask;
    }
}
'@

Replace-Required 'FModel/Views/About.xaml.cs' 'using FModel.ViewModels;' "using FModel.ViewModels;`r`nusing FModel.Localization;"
Replace-Required 'FModel/Views/About.xaml.cs' '        InitializeComponent();' "        InitializeComponent();`r`n        LocalizationManager.Apply(this);"

# -----------------------------------------------------------------------------
# Documentation and contribution metadata.
# -----------------------------------------------------------------------------
Write-Utf8 'README.md' @'
# FModel-Recreate

FModel-Recreate is a Windows Unreal Engine archive explorer and the foundation for a future modding workspace. It is derived from the GPL-3.0 licensed [FModel](https://github.com/4sval/FModel) project and uses [CUE4Parse](https://github.com/FabianFG/CUE4Parse) for UE4 and UE5 package parsing.

## Current capabilities

- Browse Unreal Engine PAK and IoStore archives supported by CUE4Parse.
- Supply static and dynamic AES keys.
- Search packages and inspect references.
- Preview and export textures, audio, meshes, animations, materials and package data.
- Use English or Turkish interface localization.
- Receive updates from this repository's own GitHub Releases channel.

## Release channels

- **Stable:** semantic-versioned releases such as `v0.1.0`.
- **QA:** a rolling prerelease built from the latest successful `dev` commit.

Every release includes a Windows x64 ZIP and a SHA-256 checksum file.

## Building

Requirements:

- Windows
- .NET 10 SDK
- Git with submodule support

```powershell
git clone --recursive https://github.com/apexlions16/FModel-Recreate.git
cd FModel-Recreate
dotnet restore .\FModel\FModel.slnx -r win-x64
dotnet build .\FModel\FModel.slnx -c Release -r win-x64 --no-restore
```

## Attribution and license

FModel-Recreate remains licensed under GPL-3.0. The original FModel copyright, license and third-party notices are preserved in `LICENSE` and `NOTICE`. This repository is an independent derivative and is not the official FModel distribution.

## Support

Use this repository's Discussions and Releases pages for project-specific support and downloads.
'@

Write-Utf8 'BRANDING.md' @'
# FModel-Recreate identity

- Product name: **FModel-Recreate**
- Repository and update source: `apexlions16/FModel-Recreate`
- Default accent: `#7657D5`
- Application data directory: `%APPDATA%\FModel-Recreate`
- Supported UI languages: English (`en-US`) and Turkish (`tr-TR`)

The project is a GPL-3.0 derivative of FModel. Upstream attribution must remain visible in the README, About screen, LICENSE and NOTICE.
'@

# -----------------------------------------------------------------------------
# CI, rolling QA releases and stable semantic releases.
# -----------------------------------------------------------------------------
Write-Utf8 '.github/workflows/qa.yml' @'
name: FModel-Recreate QA

on:
  push:
    branches: [dev]
  pull_request:
    branches: [dev]
  workflow_dispatch:

permissions:
  contents: write

concurrency:
  group: qa-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build:
    runs-on: windows-latest
    steps:
      - name: Checkout with submodules
        uses: actions/checkout@v6
        with:
          submodules: recursive
          fetch-depth: 0

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x

      - name: Verify submodules
        shell: pwsh
        run: |
          git submodule status --recursive
          if ((git submodule status --recursive) -match '^[-+]') { throw 'Submodule state is incomplete or detached from the recorded commit.' }

      - name: Restore
        run: dotnet restore .\FModel\FModel.slnx -r win-x64

      - name: Audit NuGet dependencies
        shell: pwsh
        run: dotnet list .\FModel\FModel.csproj package --vulnerable --include-transitive

      - name: Build
        run: dotnet build .\FModel\FModel.slnx -c Release -r win-x64 --no-restore -p:ContinuousIntegrationBuild=true

      - name: Publish QA executable
        if: github.event_name != 'pull_request'
        run: dotnet publish .\FModel\FModel.csproj -c Release --no-restore --no-self-contained -r win-x64 -f net10.0-windows -o .\artifacts\publish -p:PublishReadyToRun=false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:InformationalVersion=0.1.0-qa+${{ github.sha }}

      - name: Package QA build
        if: github.event_name != 'pull_request'
        shell: pwsh
        run: |
          Compress-Archive -Path .\artifacts\publish\FModel-Recreate.exe -DestinationPath .\artifacts\FModel-Recreate-QA-${{ github.sha }}.zip -Force
          (Get-FileHash .\artifacts\FModel-Recreate-QA-${{ github.sha }}.zip -Algorithm SHA256).Hash.ToLower() + '  FModel-Recreate-QA-${{ github.sha }}.zip' | Set-Content .\artifacts\FModel-Recreate-QA-${{ github.sha }}.sha256

      - name: Upload QA artifact
        if: github.event_name != 'pull_request'
        uses: actions/upload-artifact@v4
        with:
          name: FModel-Recreate-QA-${{ github.sha }}
          path: |
            artifacts/FModel-Recreate-QA-${{ github.sha }}.zip
            artifacts/FModel-Recreate-QA-${{ github.sha }}.sha256
          if-no-files-found: error

      - name: Update rolling QA release
        if: github.event_name == 'push' && github.ref == 'refs/heads/dev'
        uses: softprops/action-gh-release@v2
        with:
          tag_name: qa
          name: FModel-Recreate QA
          body: Rolling test build from `${{ github.sha }}`. This prerelease is replaced after each successful `dev` build.
          prerelease: true
          make_latest: false
          files: |
            artifacts/FModel-Recreate-QA-${{ github.sha }}.zip
            artifacts/FModel-Recreate-QA-${{ github.sha }}.sha256
'@

Write-Utf8 '.github/workflows/release.yml' @'
name: FModel-Recreate Release

on:
  push:
    branches: [dev]
    paths:
      - FModel/FModel.csproj
  workflow_dispatch:
    inputs:
      version:
        description: Semantic version without the v prefix (for example 0.2.0)
        required: false
        type: string
      prerelease:
        description: Publish as a prerelease
        required: false
        default: false
        type: boolean

permissions:
  contents: write

concurrency:
  group: stable-release
  cancel-in-progress: false

jobs:
  release:
    runs-on: windows-latest
    steps:
      - name: Checkout with submodules
        uses: actions/checkout@v6
        with:
          submodules: recursive
          fetch-depth: 0

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x

      - name: Resolve and validate version
        id: version
        shell: pwsh
        run: |
          [xml]$project = Get-Content .\FModel\FModel.csproj
          $projectVersion = [string]$project.Project.PropertyGroup.Version | Select-Object -First 1
          $requested = '${{ inputs.version }}'
          $version = if ([string]::IsNullOrWhiteSpace($requested)) { $projectVersion } else { $requested }
          if ($version -notmatch '^\d+\.\d+\.\d+([.-][0-9A-Za-z.-]+)?$') { throw "Invalid semantic version: $version" }
          "version=$version" >> $env:GITHUB_OUTPUT
          "tag=v$version" >> $env:GITHUB_OUTPUT

      - name: Skip an existing release
        id: existing
        shell: pwsh
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          gh release view '${{ steps.version.outputs.tag }}' --repo '${{ github.repository }}' *> $null
          if ($LASTEXITCODE -eq 0) { "exists=true" >> $env:GITHUB_OUTPUT } else { "exists=false" >> $env:GITHUB_OUTPUT; exit 0 }

      - name: Restore
        if: steps.existing.outputs.exists != 'true'
        run: dotnet restore .\FModel\FModel.slnx -r win-x64

      - name: Build
        if: steps.existing.outputs.exists != 'true'
        run: dotnet build .\FModel\FModel.slnx -c Release -r win-x64 --no-restore -p:ContinuousIntegrationBuild=true

      - name: Publish
        if: steps.existing.outputs.exists != 'true'
        run: dotnet publish .\FModel\FModel.csproj -c Release --no-restore --no-self-contained -r win-x64 -f net10.0-windows -o .\artifacts\publish -p:PublishReadyToRun=false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:Version=${{ steps.version.outputs.version }} -p:FileVersion=${{ steps.version.outputs.version }}.0 -p:AssemblyVersion=${{ steps.version.outputs.version }}.0

      - name: Package and checksum
        if: steps.existing.outputs.exists != 'true'
        shell: pwsh
        run: |
          $name = 'FModel-Recreate-${{ steps.version.outputs.version }}-win-x64'
          Compress-Archive -Path .\artifacts\publish\FModel-Recreate.exe -DestinationPath ".\artifacts\$name.zip" -Force
          (Get-FileHash ".\artifacts\$name.zip" -Algorithm SHA256).Hash.ToLower() + "  $name.zip" | Set-Content ".\artifacts\$name.sha256"

      - name: Create GitHub release
        if: steps.existing.outputs.exists != 'true'
        uses: softprops/action-gh-release@v2
        with:
          tag_name: ${{ steps.version.outputs.tag }}
          name: FModel-Recreate ${{ steps.version.outputs.tag }}
          target_commitish: dev
          generate_release_notes: true
          prerelease: ${{ inputs.prerelease || false }}
          make_latest: true
          files: |
            artifacts/FModel-Recreate-${{ steps.version.outputs.version }}-win-x64.zip
            artifacts/FModel-Recreate-${{ steps.version.outputs.version }}-win-x64.sha256
'@

# Remove upstream-only deployment references that should never run in this fork.
Get-ChildItem '.github/workflows' -File | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    if ($text.Contains('api.fmodel.app') -or $text.Contains('github.com/4sval/FModel/releases')) {
        throw "Upstream deployment reference remains in $($_.FullName)"
    }
}

# Build validation before publishing the generated commit.
dotnet restore .\FModel\FModel.slnx -r win-x64
dotnet build .\FModel\FModel.slnx -c Release -r win-x64 --no-restore -p:ContinuousIntegrationBuild=true

# The bootstrap mechanism must not remain in the product branch.
Remove-Item '.github/workflows/bootstrap-foundation.yml' -Force
Remove-Item 'tools/bootstrap-foundation.ps1' -Force
if (Test-Path 'tools' -PathType Container -and -not (Get-ChildItem 'tools' -Force)) { Remove-Item 'tools' -Force }

git config user.name 'github-actions[bot]'
git config user.email '41898282+github-actions[bot]@users.noreply.github.com'
git add --all
git commit -m 'build foundation and FModel-Recreate identity'
git push origin HEAD
