using System.Collections.Concurrent;
using webProject.Models;

namespace webProject.Services;

public interface ITaskManager
{
    string CreateTask();
    string CreateTask(string taskId);
    CancellationTokenSource? GetCancellationToken(string taskId);
    void CancelTask(string taskId);
    void RemoveTask(string taskId);
}

public class TaskManager : ITaskManager
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tasks = new();

    public string CreateTask()
    {
        var taskId = Guid.NewGuid().ToString();
        return CreateTask(taskId);
    }
    
    public string CreateTask(string taskId)
    {
        var cts = new CancellationTokenSource();
        _tasks.TryAdd(taskId, cts);
        return taskId;
    }

    public CancellationTokenSource? GetCancellationToken(string taskId)
    {
        _tasks.TryGetValue(taskId, out var cts);
        return cts;
    }

    public void CancelTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var cts))
        {
            cts.Cancel();
        }
    }

    public void RemoveTask(string taskId)
    {
        if (_tasks.TryRemove(taskId, out var cts))
        {
            cts.Dispose();
        }
    }
}

