using System;
using System.IO;
using System.Threading.Tasks;
using FModel.Framework;
using FModel.ViewModels.ApiEndpoints;
using RestSharp;

namespace FModel.ViewModels;

public class ApiEndpointViewModel
{
    private const string DefaultImGuiSettings = """
[Window][Debug##Default]
Pos=60,60
Size=400,400
Collapsed=0
""";

    private readonly RestClient _client = new (new RestClientOptions
    {
        UserAgent = $"FModel-Recreate/{Constants.APP_VERSION}",
        Timeout = TimeSpan.FromSeconds(5)
    }, configureSerialization: s => s.UseSerializer<JsonNetSerializer>());

    public FortniteApiEndpoint FortniteApi { get; }
    public ValorantApiEndpoint ValorantApi { get; }
    public DillyApiEndpoint DillyApi { get; }
    public EpicApiEndpoint EpicApi { get; }
    public FModelApiEndpoint FModelApi { get; }
    public GitHubApiEndpoint GitHubApi { get; }
    public DynamicApiEndpoint DynamicApi { get; }

    public ApiEndpointViewModel()
    {
        FortniteApi = new FortniteApiEndpoint(_client);
        ValorantApi = new ValorantApiEndpoint(_client);
        DillyApi = new DillyApiEndpoint(_client);
        EpicApi = new EpicApiEndpoint(_client);
        FModelApi = new FModelApiEndpoint(_client);
        GitHubApi = new GitHubApiEndpoint(_client);
        DynamicApi = new DynamicApiEndpoint(_client);
    }

    public async Task DownloadFileAsync(string fileLink, string installationPath)
    {
        var parentDirectory = Path.GetDirectoryName(installationPath);
        if (!string.IsNullOrEmpty(parentDirectory))
            Directory.CreateDirectory(parentDirectory);

        // The viewer layout is an application default, not remote data. Keeping it
        // local removes the obsolete cdn.fmodel.app dependency and provides a valid
        // layout even during the first offline launch.
        if (string.Equals(Path.GetFileName(installationPath), "imgui.ini", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(installationPath, DefaultImGuiSettings);
            return;
        }

        var request = new FRestRequest(fileLink);
        var data = _client.DownloadData(request) ?? Array.Empty<byte>();
        await File.WriteAllBytesAsync(installationPath, data);
    }

    public void DownloadFile(string fileLink, string installationPath)
    {
        DownloadFileAsync(fileLink, installationPath).GetAwaiter().GetResult();
    }
}
