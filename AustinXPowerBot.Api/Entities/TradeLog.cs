using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Api.Entities;

public sealed class TradeLog
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Pair { get; set; } = string.Empty;
    public SignalDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Expiry { get; set; } = string.Empty;
    public TradeResult Result { get; set; }
    public decimal Profit { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
