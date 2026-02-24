using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public sealed class NullTradeExecutionCoordinator : ITradeExecutionCoordinator
{
    public event Action<string>? ActionLogged;
    public event Action<TradeLogDto>? TradeLogged;

    public Task HandleSignalAsync(SignalDto signal, CancellationToken cancellationToken = default)
    {
        ActionLogged?.Invoke($"Signal received (no-op coordinator): {signal.Pair} {signal.Direction}");
        return Task.CompletedTask;
    }

    public Task StopAllAsync()
    {
        ActionLogged?.Invoke("StopAll requested (no-op coordinator).");
        return Task.CompletedTask;
    }
}
