using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AustinXPowerBot.Api.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AustinXPowerBot.Api.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateAccessToken(ApplicationUser user)
    {
        return CreateTokenInternal(user.Id, user.Email, user.DisplayName);
    }

    public string CreateAccessToken(User user)
    {
        return CreateTokenInternal(user.Id, user.Email, user.DisplayName);
    }

    private string CreateTokenInternal(long id, string? email, string? displayName)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer missing.");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience missing.");
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing.");
        var ttlMinutes = int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var value) ? value : 120;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id.ToString()),
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new("display_name", displayName ?? string.Empty)
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(ttlMinutes),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
