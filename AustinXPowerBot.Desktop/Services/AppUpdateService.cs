using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace AustinXPowerBot.Desktop.Services;

public sealed class AppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string UpdateSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AustinXPowerBot",
        "update.settings.json");

    public async Task<UpdateManifest?> GetAvailableUpdateAsync(CancellationToken cancellationToken = default)
    {
        var manifestUrl = ResolveManifestUrl();
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, cancellationToken);
            if (manifest is null
                || string.IsNullOrWhiteSpace(manifest.Version)
                || string.IsNullOrWhiteSpace(manifest.InstallerUrl))
            {
                return null;
            }

            if (!Version.TryParse(manifest.Version, out var latestVersion))
            {
                return null;
            }

            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
            return latestVersion > current ? manifest : null;
        }
        catch
        {
            return null;
        }
    }

    public void LaunchInstaller(string installerUrl)
    {
        if (string.IsNullOrWhiteSpace(installerUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = installerUrl,
            UseShellExecute = true
        });
    }

    private static string? ResolveManifestUrl()
    {
        var fromEnv = Environment.GetEnvironmentVariable("AUSTINXPOWERBOT_UPDATE_MANIFEST_URL");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        try
        {
            if (!File.Exists(UpdateSettingsPath))
            {
                return null;
            }

            var json = File.ReadAllText(UpdateSettingsPath);
            var settings = JsonSerializer.Deserialize<UpdateSettings>(json, JsonOptions);
            return settings?.UpdateManifestUrl;
        }
        catch
        {
            return null;
        }
    }

    private sealed record UpdateSettings(string? UpdateManifestUrl);

    public sealed record UpdateManifest(
        string Version,
        string InstallerUrl,
        string? Notes);
}
