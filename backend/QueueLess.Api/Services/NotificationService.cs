using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QueueLess.Api.Data;
using QueueLess.Api.DTOs;
using QueueLess.Api.Hubs;
using QueueLess.Api.Models;

namespace QueueLess.Api.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<QueueHub> _hub;

    public NotificationService(ApplicationDbContext db, IHubContext<QueueHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<NotificationDto> CreateNotificationAsync(Guid userId, string title, string message, string type, Guid? relatedEntityId = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        var dto = new NotificationDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            RelatedEntityId = notification.RelatedEntityId,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt
        };

        // Broadcast to patient group
        await _hub.Clients.Group($"Patient_{userId}").SendAsync("NotificationReceived", dto);

        return dto;
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        return notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            RelatedEntityId = n.RelatedEntityId,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return false;

        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return true;
    }
}
