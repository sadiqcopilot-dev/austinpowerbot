using System.IO;
using System.Text.Json;

namespace AustinXPowerBot.Desktop.Services;

public sealed class LocalStorageService : ILocalStorageService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static string BaseFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AustinXPowerBot");

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(BaseFolder);
        var path = BuildPath(key);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, _jsonOptions, cancellationToken);
    }

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(key);
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
    }

    public Task DeleteAsync(string key)
    {
        try
        {
            var path = BuildPath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    private static string BuildPath(string key) => Path.Combine(BaseFolder, key + ".json");
}
