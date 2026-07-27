using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace FModel.ModTools;

public sealed class RepakToolManager
{
    public const string Version = "0.2.3";
    public const string WindowsAssetName = "repak_cli-x86_64-pc-windows-msvc.zip";
    private const string ReleaseApi = "https://api.github.com/repos/trumank/repak/releases/tags/v" + Version;
    private static readonly SemaphoreSlim ToolLock = new(1, 1);

    private readonly HttpClient _httpClient;

    public RepakToolManager(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FModel-Recreate", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<string> EnsureAvailableAsync(string toolsDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsDirectory);
        var versionDirectory = Path.Combine(Path.GetFullPath(toolsDirectory), "repak", "v" + Version);
        var executablePath = Path.Combine(versionDirectory, "repak.exe");
        if (File.Exists(executablePath)) return executablePath;

        await ToolLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(executablePath)) return executablePath;
            Directory.CreateDirectory(versionDirectory);

            using var releaseResponse = await _httpClient.GetAsync(ReleaseApi, cancellationToken).ConfigureAwait(false);
            releaseResponse.EnsureSuccessStatusCode();
            await using var releaseStream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var releaseJson = await JsonDocument.ParseAsync(releaseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var asset = releaseJson.RootElement.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(element => element.GetProperty("name").GetString() == WindowsAssetName);
            if (asset.ValueKind == JsonValueKind.Undefined)
                throw new InvalidDataException($"The official repak release does not contain {WindowsAssetName}.");

            var downloadUrl = asset.GetProperty("browser_download_url").GetString()
                              ?? throw new InvalidDataException("The repak asset has no download URL.");
            var digest = asset.TryGetProperty("digest", out var digestElement)
                ? digestElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The repak release asset does not expose a SHA-256 digest.");
            var expectedSha256 = digest["sha256:".Length..];

            var tempRoot = Path.Combine(Path.GetTempPath(), "FModel-Recreate", "repak-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var archivePath = Path.Combine(tempRoot, WindowsAssetName);
            try
            {
                using var assetResponse = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                assetResponse.EnsureSuccessStatusCode();
                await using (var source = await assetResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                await using (var destination = File.Create(archivePath))
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

                var actualSha256 = ModProjectStore.ComputeSha256(archivePath);
                if (!actualSha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"The repak download failed SHA-256 verification. Expected {expectedSha256}, got {actualSha256}.");

                ZipFile.ExtractToDirectory(archivePath, tempRoot, true);
                var extractedExecutable = Directory.EnumerateFiles(tempRoot, "repak.exe", SearchOption.AllDirectories).SingleOrDefault()
                                          ?? throw new InvalidDataException("repak.exe was not found inside the verified archive.");
                foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(extractedExecutable)!, "*", SearchOption.TopDirectoryOnly))
                {
                    var destination = Path.Combine(versionDirectory, Path.GetFileName(file));
                    File.Copy(file, destination, true);
                }
                if (!File.Exists(executablePath))
                    throw new InvalidDataException("repak.exe was not installed after extraction.");

                var metadata = JsonSerializer.Serialize(new
                {
                    Version,
                    Asset = WindowsAssetName,
                    Sha256 = expectedSha256,
                    DownloadedUtc = DateTimeOffset.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(versionDirectory, "tool.json"), metadata);
                return executablePath;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                }
                catch (IOException)
                {
                    // Temporary cleanup must not invalidate an otherwise verified installation.
                }
            }
        }
        finally
        {
            ToolLock.Release();
        }
    }
}
