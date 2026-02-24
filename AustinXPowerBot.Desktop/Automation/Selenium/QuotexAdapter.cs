namespace AustinXPowerBot.Desktop.Automation.Selenium;

public sealed class QuotexAdapter(BrokerSelectorProvider selectorProvider, Action<string>? log = null) : IBrokerAdapter
{
    private Dictionary<string, string>? _selectors;

    public async Task<bool> EnsureLoggedIn(CancellationToken cancellationToken = default)
    {
        _selectors ??= await selectorProvider.LoadAsync("Quotex", cancellationToken);
        log?.Invoke("QuotexAdapter.EnsureLoggedIn executed.");
        return true;
    }

    public Task SelectPair(string pair, CancellationToken cancellationToken = default)
    {
        log?.Invoke($"QuotexAdapter.SelectPair: {pair}");
        return Task.CompletedTask;
    }

    public Task SetAmount(decimal amount, CancellationToken cancellationToken = default)
    {
        log?.Invoke($"QuotexAdapter.SetAmount: {amount}");
        return Task.CompletedTask;
    }

    public Task SetExpiry(string expiry, CancellationToken cancellationToken = default)
    {
        log?.Invoke($"QuotexAdapter.SetExpiry: {expiry}");
        return Task.CompletedTask;
    }

    public Task PlaceTrade(string direction, CancellationToken cancellationToken = default)
    {
        log?.Invoke($"QuotexAdapter.PlaceTrade: {direction}");
        return Task.CompletedTask;
    }

    public Task<decimal?> ReadBalance(CancellationToken cancellationToken = default)
    {
        log?.Invoke("QuotexAdapter.ReadBalance called.");
        return Task.FromResult<decimal?>(null);
    }

    public Task<bool> HealthCheck(CancellationToken cancellationToken = default)
    {
        log?.Invoke("QuotexAdapter.HealthCheck called.");
        return Task.FromResult(true);
    }
}
