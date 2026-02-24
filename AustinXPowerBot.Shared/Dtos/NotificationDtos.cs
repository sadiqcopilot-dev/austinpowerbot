namespace AustinXPowerBot.Shared.Dtos;

public record NotificationDto(
    long Id,
    string Title,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAtUtc
);

public record RemoteCommandDto(
    string Command,
    string? Payload,
    DateTimeOffset TimestampUtc
);
