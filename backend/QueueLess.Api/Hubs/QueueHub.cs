using Microsoft.AspNetCore.SignalR;

namespace QueueLess.Api.Hubs;

public class QueueHub : Hub
{
    public async Task JoinQueueSession(string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"QueueSession_{sessionId}");
        }
    }

    public async Task LeaveQueueSession(string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"QueueSession_{sessionId}");
        }
    }

    public async Task JoinPatientChannel(string patientUserId)
    {
        if (!string.IsNullOrWhiteSpace(patientUserId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Patient_{patientUserId}");
        }
    }

    public async Task LeavePatientChannel(string patientUserId)
    {
        if (!string.IsNullOrWhiteSpace(patientUserId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Patient_{patientUserId}");
        }
    }
}
