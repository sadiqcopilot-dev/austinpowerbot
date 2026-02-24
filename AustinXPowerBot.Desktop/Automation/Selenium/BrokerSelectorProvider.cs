using System.IO;
using System.Text.Json;

namespace AustinXPowerBot.Desktop.Automation.Selenium;

public sealed class BrokerSelectorProvider
{
    private readonly string _selectorsRoot;

    public BrokerSelectorProvider(string? selectorsRoot = null)
    {
        _selectorsRoot = selectorsRoot ?? Path.Combine(AppContext.BaseDirectory, "Selectors");
    }

    public async Task<Dictionary<string, string>> LoadAsync(string broker, CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(_selectorsRoot, broker + ".json");
        if (!File.Exists(file))
        {
            return [];
        }

        await using var stream = File.OpenRead(file);
        var parsed = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken);
        return parsed ?? [];
    }
}
