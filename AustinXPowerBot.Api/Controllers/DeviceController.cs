using System.Security.Claims;
using AustinXPowerBot.Api.Data;
using AustinXPowerBot.Api.Entities;
using AustinXPowerBot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AustinXPowerBot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/device")]
public sealed class DeviceController(AppDbContext dbContext) : ControllerBase
{
    [HttpPost("bind")]
    public async Task<ActionResult<DeviceBindResponse>> Bind([FromBody] DeviceBindRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new DeviceBindResponse(false, "Unauthorized: missing user id.", null, null));
        }

        if (string.IsNullOrWhiteSpace(request.DeviceIdHash) || string.IsNullOrWhiteSpace(request.DeviceModel) || request.TelegramId <= 0)
        {
            return BadRequest(new DeviceBindResponse(false, "DeviceIdHash, DeviceModel and TelegramId are required.", null, null));
        }

        var existingBinding = await dbContext.DeviceBindings.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (existingBinding is null)
        {
            existingBinding = new DeviceBinding
            {
                UserId = userId,
                DeviceIdHash = request.DeviceIdHash,
                DeviceModel = request.DeviceModel,
                TelegramId = request.TelegramId,
                BoundAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.DeviceBindings.Add(existingBinding);
        }
        else
        {
            existingBinding.DeviceIdHash = request.DeviceIdHash;
            existingBinding.DeviceModel = request.DeviceModel;
            existingBinding.TelegramId = request.TelegramId;
            existingBinding.BoundAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new DeviceBindResponse(true, "Device and Telegram binding updated.", existingBinding.BoundAtUtc.ToString("O"), userId));
    }
}
