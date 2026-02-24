using AustinXPowerBot.Api.Data;
using AustinXPowerBot.Api.Entities;
using AustinXPowerBot.Api.Services;
using AustinXPowerBot.Shared.Dtos;
using AustinXPowerBot.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AustinXPowerBot.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramController(AppDbContext dbContext, IRealtimeDispatcher realtimeDispatcher) : ControllerBase
{
    [HttpPost("link")]
    public async Task<ActionResult<TelegramStatusDto>> Link([FromBody] TelegramLinkRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId <= 0 || request.TelegramId <= 0)
        {
            return BadRequest(new TelegramStatusDto(false, request.TelegramId, "Unknown", "NotBound", "UserId and TelegramId are required."));
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return NotFound(new TelegramStatusDto(false, request.TelegramId, "Unknown", "NotBound", "User not found."));
        }

        var binding = await dbContext.DeviceBindings.SingleOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken);
        if (binding is null)
        {
            binding = new DeviceBinding
            {
                UserId = request.UserId,
                DeviceIdHash = "UNBOUND",
                DeviceModel = "UNBOUND",
                TelegramId = request.TelegramId,
                BoundAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.DeviceBindings.Add(binding);
        }
        else
        {
            binding.TelegramId = request.TelegramId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new TelegramStatusDto(true, request.TelegramId, "Pending", "DevicePending", "Telegram linked. Complete device bind in desktop app."));
    }

    [HttpGet("status")]
    public async Task<ActionResult<TelegramStatusDto>> Status([FromQuery] long telegramId, CancellationToken cancellationToken)
    {
        if (telegramId <= 0)
        {
            return BadRequest(new TelegramStatusDto(false, telegramId, "Unknown", "NotBound", "TelegramId must be greater than zero."));
        }

        var binding = await dbContext.DeviceBindings.SingleOrDefaultAsync(x => x.TelegramId == telegramId, cancellationToken);
        if (binding is null)
        {
            return NotFound(new TelegramStatusDto(false, telegramId, "Unknown", "NotBound", "Telegram is not linked to any user."));
        }

        var license = await dbContext.Licenses.SingleOrDefaultAsync(x => x.UserId == binding.UserId, cancellationToken);
        var status = license?.Status.ToString() ?? LicenseState.Pending.ToString();
        var deviceStatus = binding.DeviceIdHash == "UNBOUND" ? "DevicePending" : "Bound";

        return Ok(new TelegramStatusDto(true, telegramId, status, deviceStatus, "Status resolved."));
    }

    [HttpPost("command")]
    public async Task<IActionResult> Command([FromBody] TelegramCommandRequest request, CancellationToken cancellationToken)
    {
        if (request.TelegramId <= 0 || string.IsNullOrWhiteSpace(request.Command))
        {
            return BadRequest("TelegramId and Command are required.");
        }

        var binding = await dbContext.DeviceBindings.SingleOrDefaultAsync(x => x.TelegramId == request.TelegramId, cancellationToken);
        if (binding is null)
        {
            return NotFound("Telegram is not linked to any user.");
        }

        var command = new RemoteCommandDto(request.Command.Trim(), request.Payload, DateTimeOffset.UtcNow);
        await realtimeDispatcher.SendRemoteCommandToUserAsync(binding.UserId, command, cancellationToken);

        var notification = new Notification
        {
            UserId = binding.UserId,
            Title = "Telegram Command",
            Message = $"Command '{request.Command}' sent from Telegram {request.TelegramId}.",
            IsRead = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        await realtimeDispatcher.SendNotificationToUserAsync(
            binding.UserId,
            new NotificationDto(notification.Id, notification.Title, notification.Message, notification.IsRead, notification.CreatedAtUtc),
            cancellationToken);

        return Ok(new { success = true, userId = binding.UserId });
    }
}
