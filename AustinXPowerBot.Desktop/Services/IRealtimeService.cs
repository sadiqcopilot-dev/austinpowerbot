using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public interface IRealtimeService : IAsyncDisposable
{
    event Action<SignalDto>? SignalReceived;
    event Action<NotificationDto>? NotificationReceived;
    event Action<RemoteCommandDto>? RemoteCommandReceived;

    Task StartAsync(string baseUrl, string? accessToken, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
