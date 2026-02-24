using Microsoft.AspNetCore.Identity;

namespace AustinXPowerBot.Api.Entities;

public sealed class ApplicationUser : IdentityUser<long>
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DeviceBinding? DeviceBinding { get; set; }
    public License? License { get; set; }
    public ICollection<TradeLog> TradeLogs { get; set; } = new List<TradeLog>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
