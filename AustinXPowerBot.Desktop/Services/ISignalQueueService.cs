using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public interface ISignalQueueService : IAsyncDisposable
{
    bool AutoTradeEnabled { get; set; }
    Task EnqueueAsync(SignalDto signal, CancellationToken cancellationToken = default);
    event Action<SignalDto>? SignalForFeed;
}
