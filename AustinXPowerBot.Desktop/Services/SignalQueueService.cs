using System.Threading.Channels;
using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public sealed class SignalQueueService : ISignalQueueService
{
    private readonly Channel<SignalDto> _queue = Channel.CreateUnbounded<SignalDto>();
    private readonly ITradeExecutionCoordinator _tradeExecutionCoordinator;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _processor;

    public SignalQueueService(ITradeExecutionCoordinator? tradeExecutionCoordinator = null)
    {
        _tradeExecutionCoordinator = tradeExecutionCoordinator ?? new NullTradeExecutionCoordinator();
        _processor = Task.Run(ProcessAsync);
    }

    public bool AutoTradeEnabled { get; set; }

    public event Action<SignalDto>? SignalForFeed;

    public async Task EnqueueAsync(SignalDto signal, CancellationToken cancellationToken = default)
    {
        if (signal.TimestampUtc == default)
        {
            signal = signal with { TimestampUtc = DateTimeOffset.UtcNow };
        }

        await _queue.Writer.WriteAsync(signal, cancellationToken);
    }

    private async Task ProcessAsync()
    {
        while (await _queue.Reader.WaitToReadAsync(_shutdown.Token))
        {
            while (_queue.Reader.TryRead(out var signal))
            {
                if (!PassesFilter(signal))
                {
                    continue;
                }

                SignalForFeed?.Invoke(signal);

                if (AutoTradeEnabled)
                {
                    await _tradeExecutionCoordinator.HandleSignalAsync(signal, _shutdown.Token);
                }
            }
        }
    }

    private static bool PassesFilter(SignalDto signal)
    {
        return !string.IsNullOrWhiteSpace(signal.Pair)
               && !string.IsNullOrWhiteSpace(signal.Expiry)
               && !string.IsNullOrWhiteSpace(signal.Source);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _queue.Writer.TryComplete();

        try
        {
            await _processor;
        }
        catch (OperationCanceledException)
        {
        }

        _shutdown.Dispose();
    }
}
