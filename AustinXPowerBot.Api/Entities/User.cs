using System.ComponentModel.DataAnnotations;

namespace AustinXPowerBot.Api.Entities;

public sealed class User
{
	public long Id { get; set; }

	[Required]
	[MaxLength(256)]
	public string Email { get; set; } = string.Empty;

	[MaxLength(128)]
	public string DisplayName { get; set; } = string.Empty;

	public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
	public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
}

