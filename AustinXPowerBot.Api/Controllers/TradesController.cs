using System.Security.Claims;
using AustinXPowerBot.Api.Data;
using AustinXPowerBot.Api.Entities;
using AustinXPowerBot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustinXPowerBot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/trades")]
public sealed class TradesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<TradeLogDto>> GetLogs()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Unauthorized: missing user id.");
        }

        var logs = dbContext.TradeLogs
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.TimestampUtc)
            .Take(200)
            .Select(x => new TradeLogDto(x.Pair, x.Direction, x.Amount, x.Expiry, x.Result, x.Profit, x.TimestampUtc))
            .ToList();

        return Ok(logs);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TradeLogDto request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Unauthorized: missing user id.");
        }

        var entity = new TradeLog
        {
            UserId = userId,
            Pair = request.Pair,
            Direction = request.Direction,
            Amount = request.Amount,
            Expiry = request.Expiry,
            Result = request.Result,
            Profit = request.Profit,
            TimestampUtc = request.TimestampUtc == default ? DateTimeOffset.UtcNow : request.TimestampUtc
        };

        dbContext.TradeLogs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, id = entity.Id });
    }
}
