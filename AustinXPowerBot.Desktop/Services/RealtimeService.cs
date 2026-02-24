using AustinXPowerBot.Shared.Contracts;
using AustinXPowerBot.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace AustinXPowerBot.Desktop.Services;

public sealed class RealtimeService : IRealtimeService
{
    private HubConnection? _connection;

    public event Action<SignalDto>? SignalReceived;
    public event Action<NotificationDto>? NotificationReceived;
    public event Action<RemoteCommandDto>? RemoteCommandReceived;

    public async Task StartAsync(string baseUrl, string? accessToken, CancellationToken cancellationToken = default)
    {
        if (_connection is not null && _connection.State != HubConnectionState.Disconnected)
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(baseUrl), SignalREvents.RealtimeHubPath), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<SignalDto>(SignalREvents.Client.SignalReceived, signal => SignalReceived?.Invoke(signal));
        _connection.On<NotificationDto>(SignalREvents.Client.NotificationReceived, notification => NotificationReceived?.Invoke(notification));
        _connection.On<RemoteCommandDto>(SignalREvents.Client.RemoteCommandReceived, command => RemoteCommandReceived?.Invoke(command));

        await _connection.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
