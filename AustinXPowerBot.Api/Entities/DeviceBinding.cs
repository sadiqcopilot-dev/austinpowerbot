namespace AustinXPowerBot.Api.Entities;

public sealed class DeviceBinding
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string DeviceIdHash { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public long TelegramId { get; set; }
    public DateTimeOffset BoundAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
