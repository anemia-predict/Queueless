using System.ComponentModel.DataAnnotations;

namespace QueueLess.Api.DTOs;

public class RegisterRequestDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = Models.UserRoles.Patient;
}

public class LoginRequestDto
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public UserProfileDto User { get; set; } = null!;
}

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public Guid? HospitalId { get; set; }
    public string? HospitalName { get; set; }
    public string? HospitalStatus { get; set; }
}
