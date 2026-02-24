using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Shared.Dtos;

public record SignalDto(
    string Pair,
    SignalDirection Direction,
    SignalStrength Strength,
    string Expiry,
    string Source,
    DateTimeOffset TimestampUtc
);
