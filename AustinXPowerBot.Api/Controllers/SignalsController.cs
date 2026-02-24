using AustinXPowerBot.Api.Data;
using AustinXPowerBot.Api.Entities;
using AustinXPowerBot.Api.Services;
using AustinXPowerBot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustinXPowerBot.Api.Controllers;

[ApiController]
[Route("api/signals")]
public sealed class SignalsController(AppDbContext dbContext, IRealtimeDispatcher realtimeDispatcher) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public ActionResult<IReadOnlyList<SignalDto>> GetLatest()
    {
        var data = dbContext.Signals
            .OrderByDescending(x => x.TimestampUtc)
            .Take(100)
            .Select(x => new SignalDto(x.Pair, x.Direction, x.Strength, x.Expiry, x.Source, x.TimestampUtc))
            .ToList();

        return Ok(data);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<SignalDto>> Create([FromBody] SignalDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Pair) || string.IsNullOrWhiteSpace(request.Expiry) || string.IsNullOrWhiteSpace(request.Source))
        {
            return BadRequest("Pair, Expiry and Source are required.");
        }

        var signal = new Signal
        {
            Pair = request.Pair,
            Direction = request.Direction,
            Strength = request.Strength,
            Expiry = request.Expiry,
            Source = request.Source,
            TimestampUtc = request.TimestampUtc == default ? DateTimeOffset.UtcNow : request.TimestampUtc
        };

        dbContext.Signals.Add(signal);
        await dbContext.SaveChangesAsync(cancellationToken);

        var payload = new SignalDto(signal.Pair, signal.Direction, signal.Strength, signal.Expiry, signal.Source, signal.TimestampUtc);
        await realtimeDispatcher.BroadcastSignalAsync(payload, cancellationToken);

        return Ok(payload);
    }
}
