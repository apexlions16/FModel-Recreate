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
            return JsonConvert.DeserializeObject<UserSettings>(
                       File.ReadAllText(AppPaths.ActiveSettingsFile),
                       JsonNetSerializer.SerializerSettings)
                   ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
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
