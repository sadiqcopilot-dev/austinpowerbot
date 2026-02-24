using AustinXPowerBot.Api.Data;
using AustinXPowerBot.Api.Entities;
using AustinXPowerBot.Api.Services;
using AustinXPowerBot.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AustinXPowerBot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AppDbContext dbContext, IJwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponse(false, "Email and password are required.", null, null, null, null));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.LegacyUsers.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
        {
            return Conflict(new LoginResponse(false, "Email is already registered.", null, null, null, null));
        }

        var (hash, salt) = PasswordHasher.CreateHash(request.Password);
        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? normalizedEmail : request.DisplayName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt
        };

        dbContext.LegacyUsers.Add(user);
        dbContext.Licenses.Add(new License { LegacyUser = user, Status = Shared.Enums.LicenseState.Pending, PlanName = "Basic" });
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = jwtTokenService.CreateAccessToken(user);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(int.TryParse(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Jwt:AccessTokenMinutes"], out var m) ? m : 120);

        return Ok(new LoginResponse(true, "Registration succeeded.", token, user.Id, user.DisplayName, expiresAt));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponse(false, "Email and password are required.", null, null, null, null));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.LegacyUsers.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Unauthorized(new LoginResponse(false, "Invalid credentials.", null, null, null, null));
        }

        var token = jwtTokenService.CreateAccessToken(user);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(int.TryParse(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Jwt:AccessTokenMinutes"], out var m) ? m : 120);
        return Ok(new LoginResponse(true, "Login succeeded.", token, user.Id, user.DisplayName, expiresAt));
    }
}
