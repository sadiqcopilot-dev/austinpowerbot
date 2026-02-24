namespace AustinXPowerBot.Desktop.Automation.Selenium;

public interface IBrokerAdapter
{
    Task<bool> EnsureLoggedIn(CancellationToken cancellationToken = default);
    Task SelectPair(string pair, CancellationToken cancellationToken = default);
    Task SetAmount(decimal amount, CancellationToken cancellationToken = default);
    Task SetExpiry(string expiry, CancellationToken cancellationToken = default);
    Task PlaceTrade(string direction, CancellationToken cancellationToken = default);
    Task<decimal?> ReadBalance(CancellationToken cancellationToken = default);
    Task<bool> HealthCheck(CancellationToken cancellationToken = default);
}
