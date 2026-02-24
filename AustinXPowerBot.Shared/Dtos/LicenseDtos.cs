using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Shared.Dtos;

public record LicenseStatusDto(
    LicenseState Status,
    string PlanName,
    DateTimeOffset? ValidUntilUtc,
    bool IsDeviceBound,
    bool IsTelegramBound,
    string? Message
);
