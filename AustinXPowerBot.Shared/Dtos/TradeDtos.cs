using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Shared.Dtos;

public record TradeLogDto(
    string Pair,
    SignalDirection Direction,
    decimal Amount,
    string Expiry,
    TradeResult Result,
    decimal Profit,
    DateTimeOffset TimestampUtc
);
