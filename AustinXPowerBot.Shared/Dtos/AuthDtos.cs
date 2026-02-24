namespace AustinXPowerBot.Shared.Dtos;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string Email, string Password, string DisplayName);

public record LoginResponse(
    bool Success,
    string Message,
    string? AccessToken,
    long? UserId,
    string? DisplayName,
    DateTimeOffset? ExpiresAtUtc
);
