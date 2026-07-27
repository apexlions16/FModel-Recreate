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

    public static readonly string SettingsFile = Path.Combine(AppDataDirectory, "AppSettings.json");
    public static readonly string DebugSettingsFile = Path.Combine(AppDataDirectory, "AppSettings_Debug.json");
    public static readonly string UiLanguageFile = Path.Combine(AppDataDirectory, "ui-language.txt");
    public static readonly string FirstRunMarker = Path.Combine(AppDataDirectory, "first-run-v1.complete");

    public static string ActiveSettingsFile
    {
        get
        {
#if DEBUG
            return DebugSettingsFile;
#else
            return SettingsFile;
#endif
        }
    }

    public static bool MigrateLegacySettings()
    {
        Directory.CreateDirectory(AppDataDirectory);

#if DEBUG
        const string fileName = "AppSettings_Debug.json";
#else
        const string fileName = "AppSettings.json";
#endif

        var source = Path.Combine(LegacyAppDataDirectory, fileName);
        var destination = ActiveSettingsFile;
        if (!File.Exists(source) || File.Exists(destination)) return false;

        File.Copy(source, destination, false);
        File.WriteAllText(FirstRunMarker, DateTimeOffset.UtcNow.ToString("O"));
        return true;
    }
}
