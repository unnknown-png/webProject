using System.Collections.Concurrent;

namespace webProject.Services;

public interface ITaskManager
{
    string CreateTask();
    string CreateTask(string taskId);
    CancellationTokenSource? GetCancellationToken(string taskId);
    void CancelTask(string taskId);
    void RemoveTask(string taskId);
    int GetActiveTaskCount(int userId);
    bool CanCreateTask(int userId);
    void AssociateTaskWithUser(string taskId, int userId);
}

public class TaskManager : ITaskManager
{
    private const int MaxConcurrentTasksPerUser = 3;
    
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tasks = new();
    private readonly ConcurrentDictionary<string, int> _taskUserMap = new();
    private readonly ConcurrentDictionary<int, ConcurrentBag<string>> _userTasks = new();

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
        
        if (_taskUserMap.TryRemove(taskId, out var userId))
        {
            if (_userTasks.TryGetValue(userId, out var userTaskList))
            {
                var updatedList = new ConcurrentBag<string>(userTaskList.Where(t => t != taskId));
                _userTasks.TryUpdate(userId, updatedList, userTaskList);
            }
        }
    }
    
    public int GetActiveTaskCount(int userId)
    {
        if (_userTasks.TryGetValue(userId, out var tasks))
        {
            return tasks.Count(taskId => _tasks.ContainsKey(taskId));
        }
        return 0;
    }
    
    public bool CanCreateTask(int userId)
    {
        return GetActiveTaskCount(userId) < MaxConcurrentTasksPerUser;
    }
    
    public void AssociateTaskWithUser(string taskId, int userId)
    {
        _taskUserMap.TryAdd(taskId, userId);
        
        var userTaskList = _userTasks.GetOrAdd(userId, _ => new ConcurrentBag<string>());
        userTaskList.Add(taskId);
    }
}

