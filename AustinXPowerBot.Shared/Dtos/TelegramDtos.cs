namespace AustinXPowerBot.Shared.Dtos;

public record TelegramLinkRequest(
    long UserId,
    long TelegramId,
    string Username
);

public record TelegramStatusDto(
    bool Linked,
    long TelegramId,
    string LicenseStatus,
    string DeviceBindingStatus,
    string Message
);

public record TelegramCommandRequest(
    long TelegramId,
    string Command,
    string? Payload
);
