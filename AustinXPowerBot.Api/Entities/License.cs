using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Api.Entities;

public sealed class License
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public LicenseState Status { get; set; } = LicenseState.Pending;
    public DateTimeOffset? ValidUntilUtc { get; set; }
    public string PlanName { get; set; } = "Basic";
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public User? LegacyUser { get; set; }
}
