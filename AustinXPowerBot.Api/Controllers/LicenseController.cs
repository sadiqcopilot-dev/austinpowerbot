using System.Security.Claims;
using AustinXPowerBot.Api.Data;
using AustinXPowerBot.Shared.Dtos;
using AustinXPowerBot.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AustinXPowerBot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/license")]
public sealed class LicenseController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<LicenseStatusDto>> GetStatus([FromQuery] string deviceIdHash, [FromQuery] long telegramId, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new LicenseStatusDto(LicenseState.Revoked, "Unknown", null, false, false, "Unauthorized: missing user id."));
        }

        var license = await dbContext.Licenses.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (license is null)
        {
            return NotFound(new LicenseStatusDto(LicenseState.Pending, "Unknown", null, false, false, "No license record exists for this user."));
        }

        var binding = await dbContext.DeviceBindings.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var isDeviceBound = binding is not null && binding.DeviceIdHash == deviceIdHash;
        var isTelegramBound = binding is not null && binding.TelegramId == telegramId;

        var isExpiredByDate = license.ValidUntilUtc.HasValue && license.ValidUntilUtc.Value <= DateTimeOffset.UtcNow;
        var effectiveStatus = license.Status;
        if (effectiveStatus == LicenseState.Active && (isExpiredByDate || !isDeviceBound || !isTelegramBound))
        {
            effectiveStatus = isExpiredByDate ? LicenseState.Expired : LicenseState.Pending;
        }

        var message = effectiveStatus switch
        {
            LicenseState.Active when isDeviceBound && isTelegramBound => "License active and binding validated.",
            LicenseState.Active => "License active but binding mismatch detected.",
            LicenseState.Pending => "Activation pending. Bind current device and Telegram account.",
            LicenseState.Expired => "License expired. Request activation.",
            _ => "License revoked. Contact support."
        };

        return Ok(new LicenseStatusDto(effectiveStatus, license.PlanName, license.ValidUntilUtc, isDeviceBound, isTelegramBound, message));
    }

    [HttpPost("request-activation")]
    public async Task<ActionResult<LicenseStatusDto>> RequestActivation(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new LicenseStatusDto(LicenseState.Revoked, "Unknown", null, false, false, "Unauthorized: missing user id."));
        }

        var license = await dbContext.Licenses.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (license is null)
        {
            return NotFound(new LicenseStatusDto(LicenseState.Pending, "Unknown", null, false, false, "No license record exists for this user."));
        }

        license.Status = LicenseState.Pending;
        license.RequestedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new LicenseStatusDto(license.Status, license.PlanName, license.ValidUntilUtc, false, false, "Activation request submitted."));
    }

    [HttpPost("admin/approve")]
    public async Task<ActionResult<LicenseStatusDto>> AdminApprove([FromBody] AdminApproveLicenseRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId <= 0 || request.ValidDays <= 0)
        {
            return BadRequest(new LicenseStatusDto(LicenseState.Pending, "Unknown", null, false, false, "UserId and ValidDays must be greater than zero."));
        }

        var license = await dbContext.Licenses.SingleOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken);
        if (license is null)
        {
            return NotFound(new LicenseStatusDto(LicenseState.Pending, "Unknown", null, false, false, "No license record exists for target user."));
        }

        license.Status = LicenseState.Active;
        license.PlanName = request.PlanName;
        license.ValidUntilUtc = DateTimeOffset.UtcNow.AddDays(request.ValidDays);
        license.ApprovedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new LicenseStatusDto(license.Status, license.PlanName, license.ValidUntilUtc, false, false, "License approved."));
    }

    public sealed record AdminApproveLicenseRequest(long UserId, string PlanName, int ValidDays);
}
