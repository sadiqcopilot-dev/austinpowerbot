using AustinXPowerBot.Api.Entities;

namespace AustinXPowerBot.Api.Services;

public interface IJwtTokenService
{
    string CreateAccessToken(ApplicationUser user);
    string CreateAccessToken(User user);
}
