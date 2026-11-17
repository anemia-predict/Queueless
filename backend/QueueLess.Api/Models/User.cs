using System.ComponentModel.DataAnnotations;

namespace QueueLess.Api.Models;

public static class UserRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string HospitalAdmin = "HospitalAdmin";
    public const string Doctor = "Doctor";
    public const string Patient = "Patient";
}

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Required]
    [MaxLength(30)]
    public string Role { get; set; } = UserRoles.Patient;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<HospitalMember> HospitalMemberships { get; set; } = new List<HospitalMember>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
