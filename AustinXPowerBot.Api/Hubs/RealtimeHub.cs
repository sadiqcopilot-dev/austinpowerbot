using AustinXPowerBot.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AustinXPowerBot.Api.Hubs;

[Authorize]
public sealed class RealtimeHub : Hub
{
    public Task JoinUserGroup(long userId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, SignalREvents.Groups.UserPrefix + userId);
    }

    public Task JoinDeviceGroup(string deviceIdHash)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, SignalREvents.Groups.DevicePrefix + deviceIdHash);
    }
}
