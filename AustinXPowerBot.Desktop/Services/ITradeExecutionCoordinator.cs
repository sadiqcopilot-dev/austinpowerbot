using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public interface ITradeExecutionCoordinator
{
    event Action<string>? ActionLogged;
    event Action<TradeLogDto>? TradeLogged;

    Task HandleSignalAsync(SignalDto signal, CancellationToken cancellationToken = default);
    Task StopAllAsync();
}
