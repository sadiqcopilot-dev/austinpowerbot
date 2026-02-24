using AustinXPowerBot.Api.Hubs;
using AustinXPowerBot.Shared.Contracts;
using AustinXPowerBot.Shared.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace AustinXPowerBot.Api.Services;

public sealed class RealtimeDispatcher(IHubContext<RealtimeHub> hubContext) : IRealtimeDispatcher
{
    public Task BroadcastSignalAsync(SignalDto signal, CancellationToken cancellationToken = default)
    {
        return hubContext.Clients.All.SendAsync(SignalREvents.Client.SignalReceived, signal, cancellationToken);
    }

    public Task SendNotificationToUserAsync(long userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        return hubContext.Clients.Group(SignalREvents.Groups.UserPrefix + userId)
            .SendAsync(SignalREvents.Client.NotificationReceived, notification, cancellationToken);
    }

    public Task SendRemoteCommandToUserAsync(long userId, RemoteCommandDto command, CancellationToken cancellationToken = default)
    {
        return hubContext.Clients.Group(SignalREvents.Groups.UserPrefix + userId)
            .SendAsync(SignalREvents.Client.RemoteCommandReceived, command, cancellationToken);
    }
}
