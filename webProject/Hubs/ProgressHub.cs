using Microsoft.AspNetCore.SignalR;

namespace webProject.Hubs;

public class ProgressHub : Hub
{
    public async Task SendProgress(string taskId, int percent, string stage, string message)
    {
        await Clients.Caller.SendAsync("ReceiveProgress", taskId, percent, stage, message);
    }

    public async Task CancelTask(string taskId)
    {
        await Clients.Caller.SendAsync("TaskCancelled", taskId);
    }
}

