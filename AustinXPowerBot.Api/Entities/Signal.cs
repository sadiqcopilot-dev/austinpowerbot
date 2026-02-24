using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Api.Entities;

public sealed class Signal
{
    public long Id { get; set; }
    public string Pair { get; set; } = string.Empty;
    public SignalDirection Direction { get; set; }
    public SignalStrength Strength { get; set; }
    public string Expiry { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
