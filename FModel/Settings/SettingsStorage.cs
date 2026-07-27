using System;
using System.Collections.Generic;
using System.IO;
using FModel.Framework;
using Newtonsoft.Json;

namespace FModel.Settings;

public static class SettingsStorage
{
    private static bool _saveEnabled = true;

    public static UserSettings Load()
    {
        AppPaths.MigrateLegacySettings();

        try
        {
            var settings = JsonConvert.DeserializeObject<UserSettings>(
                               File.ReadAllText(AppPaths.ActiveSettingsFile),
                               JsonNetSerializer.SerializerSettings)
                           ?? new UserSettings();
            return Normalize(settings);
        }
        catch
        {
            return Normalize(new UserSettings());
        }
    }

    private static UserSettings Normalize(UserSettings settings)
    {
        settings.PerDirectory ??= new Dictionary<string, DirectorySettings>();

        var configuredDirectory = settings.GameDirectory?.Trim() ?? string.Empty;
        if (IsAvailableGameDirectory(configuredDirectory))
            return settings;

        // A moved/uninstalled game or a migrated legacy setting must not be passed
        // to CUE4Parse. Clearing it makes the normal directory selector open again.
        if (!string.IsNullOrEmpty(configuredDirectory))
            settings.PerDirectory.Remove(configuredDirectory);

        settings.GameDirectory = string.Empty;
        settings.CurrentDir = null;
        return settings;
    }

    private static bool IsAvailableGameDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return false;
        if (string.Equals(directory, Constants._FN_LIVE_TRIGGER, StringComparison.Ordinal) ||
            string.Equals(directory, Constants._VAL_LIVE_TRIGGER, StringComparison.Ordinal))
            return true;

        return Directory.Exists(directory);
    }

    public static void Save()
    {
        if (!_saveEnabled || UserSettings.Default is null) return;

        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        if (UserSettings.Default.CurrentDir is not null)
            UserSettings.Default.PerDirectory[UserSettings.Default.CurrentDir.GameDirectory] = UserSettings.Default.CurrentDir;

        File.WriteAllText(
            AppPaths.ActiveSettingsFile,
            JsonConvert.SerializeObject(UserSettings.Default, Formatting.Indented));
    }

    public static void Delete()
    {
        _saveEnabled = false;
        if (File.Exists(AppPaths.ActiveSettingsFile))
            File.Delete(AppPaths.ActiveSettingsFile);
    }
}
