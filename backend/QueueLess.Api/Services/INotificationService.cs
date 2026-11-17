using QueueLess.Api.DTOs;

namespace QueueLess.Api.Services;

public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(Guid userId, string title, string message, string type, Guid? relatedEntityId = null);
    Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);
}
