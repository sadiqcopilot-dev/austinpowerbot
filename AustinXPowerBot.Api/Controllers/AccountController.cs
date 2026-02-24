using System.Threading.Tasks;
using AustinXPowerBot.Api.Entities;
using AustinXPowerBot.Api.Services;
using AustinXPowerBot.Shared.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AustinXPowerBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtService;

    public AccountController(UserManager<ApplicationUser> userManager, IJwtTokenService jwtService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
    }

    public sealed record RegisterRequest(string Email, string Password, string DisplayName);
    public sealed record AuthResponse(string Token, long UserId, string Email, string DisplayName);
    public sealed record LoginRequest(string Email, string Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var existing = await _userManager.FindByEmailAsync(req.Email);
        if (existing is not null) return BadRequest("Email already registered.");

        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, DisplayName = req.DisplayName };
        var res = await _userManager.CreateAsync(user, req.Password);
        if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));

        var token = _jwtService.CreateAccessToken(user);
        return Ok(new AuthResponse(token, user.Id, user.Email ?? string.Empty, user.DisplayName));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null) return Unauthorized("Invalid credentials.");

        if (!await _userManager.CheckPasswordAsync(user, req.Password))
            return Unauthorized("Invalid credentials.");

        var token = _jwtService.CreateAccessToken(user);
        return Ok(new AuthResponse(token, user.Id, user.Email ?? string.Empty, user.DisplayName));
    }
}
