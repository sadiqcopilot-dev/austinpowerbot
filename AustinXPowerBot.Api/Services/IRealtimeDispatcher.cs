using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Api.Services;

public interface IRealtimeDispatcher
{
    Task BroadcastSignalAsync(SignalDto signal, CancellationToken cancellationToken = default);
    Task SendNotificationToUserAsync(long userId, NotificationDto notification, CancellationToken cancellationToken = default);
    Task SendRemoteCommandToUserAsync(long userId, RemoteCommandDto command, CancellationToken cancellationToken = default);
}
