namespace AustinXPowerBot.Shared.Dtos;

public record DeviceInfoDto(
    string DeviceName,
    string DeviceModel,
    string DeviceIdHash,
    string DeviceDisplayId,
    string OsVersion
);

public record DeviceBindRequest(
    string DeviceIdHash,
    string DeviceModel,
    long TelegramId
);

public record DeviceBindResponse(
    bool Success,
    string Message,
    string? BoundAtUtc,
    long? UserId
);
