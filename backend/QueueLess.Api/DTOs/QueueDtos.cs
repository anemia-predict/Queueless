using System.ComponentModel.DataAnnotations;

namespace QueueLess.Api.DTOs;

public class QueueSessionDto
{
    public Guid Id { get; set; }
    public Guid HospitalId { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string SessionDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public int CurrentTokenNumber { get; set; }
    public int TotalBookings { get; set; }
    public int WaitingCount { get; set; }
    public int CompletedCount { get; set; }
    public BookingResponseDto? CurrentServingBooking { get; set; }
    public List<BookingResponseDto> WaitingBookings { get; set; } = new();
}

public class OpenQueueSessionDto
{
    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? ScheduleId { get; set; }

    public string? SessionDate { get; set; } // YYYY-MM-DD
    public string StartTime { get; set; } = "09:00:00";
    public string EndTime { get; set; } = "17:00:00";
    public int MaxCapacity { get; set; } = 30;
}

public class QueueActionResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public QueueSessionDto? Session { get; set; }
    public BookingResponseDto? Booking { get; set; }
}
