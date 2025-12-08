using StackExchange.Redis;
using System.Text.Json;
using webProject.Models;

namespace webProject.Services
{
    public interface IRedisQueueService
    {
        Task<string> EnqueueTaskAsync(MatrixTask task);
        Task<MatrixTask?> DequeueTaskAsync();
        Task<MatrixTask?> GetTaskStatusAsync(string taskId);
        Task UpdateTaskStatusAsync(string taskId, Models.TaskStatus status, string? errorMessage = null);
        Task CompleteTaskAsync(string taskId, string resultJson, double executionTime);
        Task<List<MatrixTask>> GetUserTasksAsync(int userId);
        Task<long> GetQueueLengthAsync();
        
        // Matrix cache methods
        Task StoreMatrixAsync(string matrixId, GeneratedMatrix matrix, TimeSpan? expiration = null);
        Task<GeneratedMatrix?> GetMatrixAsync(string matrixId);
        Task RemoveMatrixAsync(string matrixId);
    }
    
    public class RedisQueueService : IRedisQueueService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private const string QUEUE_KEY = "matrix:tasks:queue";
        private const string TASK_PREFIX = "matrix:task:";
        private const string USER_TASKS_PREFIX = "matrix:user:";
        private const string MATRIX_PREFIX = "matrix:cache:";
        
        public RedisQueueService(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = redis.GetDatabase();
        }
        
        public async Task<string> EnqueueTaskAsync(MatrixTask task)
        {
            // Use existing task ID if provided, otherwise generate new one
            if (string.IsNullOrEmpty(task.TaskId))
            {
                task.TaskId = $"task_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString()[..8]}";
            }
            
            task.Status = Models.TaskStatus.Queued;
            task.CreatedAt = DateTime.UtcNow;
            
            // Serialize task to JSON
            var taskJson = JsonSerializer.Serialize(task);
            
            // Store task data in Redis hash
            await _db.StringSetAsync(TASK_PREFIX + task.TaskId, taskJson, TimeSpan.FromHours(24));
            
            // Add task to user's task list
            await _db.SetAddAsync(USER_TASKS_PREFIX + task.UserId, task.TaskId);
            
            // Add task to queue
            await _db.ListRightPushAsync(QUEUE_KEY, task.TaskId);
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[REDIS QUEUE] Task added to queue");
            Console.WriteLine($"[REDIS QUEUE] Task ID: {task.TaskId}");
            Console.WriteLine($"[REDIS QUEUE] User  : {task.UserId}");
            Console.ResetColor();
            
            return task.TaskId;
        }
        
        public async Task<MatrixTask?> DequeueTaskAsync()
        {
            // Pop task from left side of queue (FIFO)
            var taskId = await _db.ListLeftPopAsync(QUEUE_KEY);
            
            if (taskId.IsNullOrEmpty)
                return null;
            
            // Get task data
            var taskJson = await _db.StringGetAsync(TASK_PREFIX + taskId);
            
            if (taskJson.IsNullOrEmpty)
                return null;
            
            var task = JsonSerializer.Deserialize<MatrixTask>(taskJson!);
            
            if (task != null)
            {
                // Update status to processing
                task.Status = Models.TaskStatus.Processing;
                task.StartedAt = DateTime.UtcNow;
                await _db.StringSetAsync(TASK_PREFIX + task.TaskId, JsonSerializer.Serialize(task), TimeSpan.FromHours(24));
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[REDIS QUEUE] Task dequeued from queue");
                Console.WriteLine($"[REDIS QUEUE] Task ID: {task.TaskId}");
                Console.WriteLine($"[REDIS QUEUE] Status : Processing");
                Console.ResetColor();
            }
            
            return task;
        }
        
        public async Task<MatrixTask?> GetTaskStatusAsync(string taskId)
        {
            var taskJson = await _db.StringGetAsync(TASK_PREFIX + taskId);
            
            if (taskJson.IsNullOrEmpty)
                return null;
            
            return JsonSerializer.Deserialize<MatrixTask>(taskJson!);
        }
        
        public async Task UpdateTaskStatusAsync(string taskId, Models.TaskStatus status, string? errorMessage = null)
        {
            var task = await GetTaskStatusAsync(taskId);
            
            if (task == null)
                return;
            
            task.Status = status;
            
            if (errorMessage != null)
                task.ErrorMessage = errorMessage;
            
            await _db.StringSetAsync(TASK_PREFIX + taskId, JsonSerializer.Serialize(task), TimeSpan.FromHours(24));
            
            Console.WriteLine($"[REDIS QUEUE] Task {taskId} status updated to {status}");
        }
        
        public async Task CompleteTaskAsync(string taskId, string resultJson, double executionTime)
        {
            var task = await GetTaskStatusAsync(taskId);
            
            if (task == null)
                return;
            
            task.Status = Models.TaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.ExecutionTime = executionTime;
            task.ResultJson = resultJson;
            
            await _db.StringSetAsync(TASK_PREFIX + taskId, JsonSerializer.Serialize(task), TimeSpan.FromHours(24));
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[REDIS QUEUE] Task marked as completed");
            Console.WriteLine($"[REDIS QUEUE] Task ID       : {taskId}");
            Console.WriteLine($"[REDIS QUEUE] Execution Time: {executionTime:F2}s");
            Console.ResetColor();
        }
        
        public async Task<List<MatrixTask>> GetUserTasksAsync(int userId)
        {
            var taskIds = await _db.SetMembersAsync(USER_TASKS_PREFIX + userId);
            var tasks = new List<MatrixTask>();
            
            foreach (var taskId in taskIds)
            {
                var task = await GetTaskStatusAsync(taskId!);
                if (task != null)
                    tasks.Add(task);
            }
            
            return tasks.OrderByDescending(t => t.CreatedAt).ToList();
        }
        
        public async Task<long> GetQueueLengthAsync()
        {
            return await _db.ListLengthAsync(QUEUE_KEY);
        }
        
        // Matrix cache methods
        public async Task StoreMatrixAsync(string matrixId, GeneratedMatrix matrix, TimeSpan? expiration = null)
        {
            var matrixJson = JsonSerializer.Serialize(matrix);
            var cacheKey = MATRIX_PREFIX + matrixId;
            
            // Default expiration is 1 hour
            await _db.StringSetAsync(cacheKey, matrixJson, expiration ?? TimeSpan.FromHours(1));
            
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[REDIS CACHE] Matrix stored: {matrixId}");
            Console.WriteLine($"[REDIS CACHE] Size: {matrix.Size}x{matrix.Size}");
            Console.ResetColor();
        }
        
        public async Task<GeneratedMatrix?> GetMatrixAsync(string matrixId)
        {
            var cacheKey = MATRIX_PREFIX + matrixId;
            var matrixJson = await _db.StringGetAsync(cacheKey);
            
            if (matrixJson.IsNullOrEmpty)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[REDIS CACHE] Matrix not found: {matrixId}");
                Console.ResetColor();
                return null;
            }
            
            var matrix = JsonSerializer.Deserialize<GeneratedMatrix>(matrixJson!);
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[REDIS CACHE] Matrix retrieved: {matrixId}");
            Console.WriteLine($"[REDIS CACHE] Size: {matrix?.Size}x{matrix?.Size}");
            Console.ResetColor();
            return matrix;
        }
        
        public async Task RemoveMatrixAsync(string matrixId)
        {
            var cacheKey = MATRIX_PREFIX + matrixId;
            await _db.KeyDeleteAsync(cacheKey);
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[REDIS CACHE] Matrix removed: {matrixId}");
            Console.ResetColor();
        }
    }
}

