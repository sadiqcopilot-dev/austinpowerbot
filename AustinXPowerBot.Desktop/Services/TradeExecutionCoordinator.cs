using AustinXPowerBot.Desktop.Automation.Selenium;
using AustinXPowerBot.Shared.Dtos;
using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Desktop.Services;

public sealed class TradeExecutionCoordinator : ITradeExecutionCoordinator
{
    private readonly IBrokerAdapter _brokerAdapter;
    private readonly RiskManagerService _riskManagerService;
    private CancellationTokenSource _pipelineCancellation = new();

    public TradeExecutionCoordinator(IBrokerAdapter brokerAdapter, RiskManagerService riskManagerService)
    {
        _brokerAdapter = brokerAdapter;
        _riskManagerService = riskManagerService;
    }

    public event Action<string>? ActionLogged;
    public event Action<TradeLogDto>? TradeLogged;

    public async Task HandleSignalAsync(SignalDto signal, CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _pipelineCancellation.Token);
        var ct = linkedCts.Token;

        if (!_riskManagerService.CanExecute(out var reason))
        {
            ActionLogged?.Invoke(reason);
            return;
        }

        _riskManagerService.RegisterAttempt();
        ActionLogged?.Invoke($"Processing signal {signal.Pair} {signal.Direction}.");

        await _brokerAdapter.EnsureLoggedIn(ct);
        await _brokerAdapter.SelectPair(signal.Pair, ct);
        await _brokerAdapter.SetExpiry(signal.Expiry, ct);
        await _brokerAdapter.PlaceTrade(signal.Direction.ToString(), ct);

        var log = new TradeLogDto(
            signal.Pair,
            signal.Direction,
            0m,
            signal.Expiry,
            TradeResult.Pending,
            0m,
            DateTimeOffset.UtcNow);

        _riskManagerService.RegisterResult(log);
        TradeLogged?.Invoke(log);
        ActionLogged?.Invoke("Trade execution pipeline completed.");
    }

    public Task StopAllAsync()
    {
        _pipelineCancellation.Cancel();
        _pipelineCancellation.Dispose();
        _pipelineCancellation = new CancellationTokenSource();
        ActionLogged?.Invoke("Stop All: pending operations cancelled.");
        return Task.CompletedTask;
    }
}
