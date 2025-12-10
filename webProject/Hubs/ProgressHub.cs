using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace webProject.Hubs;

[Authorize]
public class ProgressHub : Hub
{
    private readonly ILogger<ProgressHub> _logger;

    public ProgressHub(ILogger<ProgressHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var connectionId = Context.ConnectionId;
        
        _logger.LogInformation(
            "[SIGNALR HUB] ========================================\n" +
            "[SIGNALR HUB] CLIENT CONNECTED\n" +
            "[SIGNALR HUB] Connection ID: {ConnectionId}\n" +
            "[SIGNALR HUB] User ID      : {UserId}\n" +
            "[SIGNALR HUB] ========================================",
            connectionId, userId ?? "ANONYMOUS");

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(connectionId, $"user_{userId}");
            _logger.LogInformation("[SIGNALR HUB] User {UserId} added to group user_{UserId}", userId, userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var connectionId = Context.ConnectionId;
        
        _logger.LogInformation(
            "[SIGNALR HUB] CLIENT DISCONNECTED\n" +
            "[SIGNALR HUB] Connection ID: {ConnectionId}\n" +
            "[SIGNALR HUB] User ID      : {UserId}",
            connectionId, userId ?? "ANONYMOUS");

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(connectionId, $"user_{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendProgress(string taskId, int percent, string stage, string message)
    {
        await Clients.Caller.SendAsync("ReceiveProgress", taskId, percent, stage, message);
    }

    public async Task CancelTask(string taskId)
    {
        await Clients.Caller.SendAsync("TaskCancelled", taskId);
    }
}

