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
            var json = await Client.GetStringAsync(Constants.GH_RELEASES + "/latest");
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
