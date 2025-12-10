using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.Text.Json.Serialization;
using webProject.Data;
using webProject.Hubs;
using webProject.Models;

namespace webProject.Services
{
    public class MatrixWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MatrixWorker> _logger;
        private readonly IHubContext<ProgressHub> _hubContext;
        private int _workerNumber;
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        
        public MatrixWorker(
            IServiceProvider serviceProvider,
            ILogger<MatrixWorker> logger,
            IHubContext<ProgressHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hubContext = hubContext;
            _workerNumber = Random.Shared.Next(1, 100);
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"🚀 [WORKER-{_workerNumber}] Matrix Worker started and ready to process tasks!");
            Console.ResetColor();
            _logger.LogInformation($"[WORKER-{_workerNumber}] Matrix Worker started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var queueService = scope.ServiceProvider.GetRequiredService<IRedisQueueService>();
                    var combinedService = scope.ServiceProvider.GetRequiredService<ICombinedMatrixService>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    
                    var task = await queueService.DequeueTaskAsync();
                    
                    if (task == null)
                    {
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }
                    
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                    Console.WriteLine($"[WORKER-{_workerNumber:D2}] TASK PROCESSING STARTED");
                    Console.WriteLine($"[WORKER-{_workerNumber:D2}] Task ID    : {task.TaskId}");
                    Console.WriteLine($"[WORKER-{_workerNumber:D2}] Matrix Size: {task.Size}x{task.Size}");
                    Console.WriteLine($"[WORKER-{_workerNumber:D2}] User ID    : {task.UserId}");
                    Console.WriteLine($"[WORKER-{_workerNumber:D2}] Matrix ID  : {task.MatrixId}");
                    Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                    Console.ResetColor();
                    _logger.LogInformation($"[WORKER-{_workerNumber}] Processing task {task.TaskId} - Size: {task.Size}x{task.Size}");
                    
                    var groupName = $"user_{task.UserId}";
                    await _hubContext.Clients.Group(groupName)
                        .SendAsync("TaskStatusChanged", new { 
                            taskId = task.TaskId, 
                            status = "Processing",
                            size = task.Size,
                            message = $"Processing matrix {task.Size}x{task.Size}"
                        }, stoppingToken);
                    
                    var startTime = DateTime.UtcNow;
                    
                    try
                    {
                        if (await queueService.IsTaskCancelledAsync(task.TaskId))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[WORKER-{_workerNumber:D2}] Task {task.TaskId} was CANCELLED before processing started");
                            Console.ResetColor();
                            
                            await _hubContext.Clients.Group(groupName)
                                .SendAsync("TaskFailed", new { 
                                    taskId = task.TaskId,
                                    status = "Cancelled",
                                    size = task.Size,
                                    error = "Task was cancelled by user",
                                    message = $"Matrix {task.Size}x{task.Size} calculation was cancelled"
                                }, stoppingToken);
                            
                            continue;
                        }
                        
                        var matrixData = await queueService.GetMatrixAsync(task.MatrixId);
                        
                        if (matrixData == null)
                        {
                            throw new Exception($"Matrix {task.MatrixId} not found in Redis cache");
                        }
                        
                        using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        
                        var cancellationCheckTask = Task.Run(async () =>
                        {
                            while (!taskCts.Token.IsCancellationRequested)
                            {
                                await Task.Delay(500, stoppingToken);
                                
                                if (await queueService.IsTaskCancelledAsync(task.TaskId))
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"[WORKER-{_workerNumber:D2}] 🛑 CANCELLATION DETECTED for task {task.TaskId}");
                                    Console.ResetColor();
                                    taskCts.Cancel();
                                    break;
                                }
                            }
                        }, stoppingToken);
                        
                        var progress = new Progress<ProgressInfo>(async info =>
                        {
                            try
                            {
                                await _hubContext.Clients.Group(groupName)
                                    .SendAsync("ReceiveProgress", task.TaskId, info.Percent, info.Stage, info.Message);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[WORKER-{WorkerId}] Failed to send progress update", _workerNumber);
                            }
                        });
                        
                        var result = await combinedService.SolveAndDecomposeAsync(
                            matrixData.Coefficients, 
                            matrixData.RightHandSide,
                            progress,
                            cancellationToken: taskCts.Token
                        );
                        
                        var executionTime = (DateTime.UtcNow - startTime).TotalSeconds;
                        
                        if (await queueService.IsTaskCancelledAsync(task.TaskId) || taskCts.Token.IsCancellationRequested)
                        {
                            throw new OperationCanceledException("Task was cancelled by user");
                        }
                        
                        var history = new CalculationHistory
                        {
                            UserId = task.UserId,
                            Size = task.Size,
                            MatrixData = JsonSerializer.Serialize(new { matrixId = task.MatrixId }, JsonOptions),
                            Solution = JsonSerializer.Serialize(result.GaussianSolution.Solution, JsonOptions),
                            Success = true,
                            ErrorMessage = null,
                            CreatedAt = DateTime.UtcNow
                        };
                        
                        dbContext.CalculationHistories.Add(history);
                        await dbContext.SaveChangesAsync(stoppingToken);
                        
                        var resultJson = JsonSerializer.Serialize(new
                        {
                            solution = result.GaussianSolution.Solution,
                            determinant = result.LUDecomposition.Determinant,
                            executionTime,
                            historyId = history.Id
                        }, JsonOptions);
                        
                        await queueService.CompleteTaskAsync(task.TaskId, resultJson, executionTime);
                        
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] SENDING SIGNALR NOTIFICATION");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Group Name: {groupName}");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Event     : TaskCompleted");
                        Console.ResetColor();
                        
                        await _hubContext.Clients.Group(groupName)
                            .SendAsync("TaskCompleted", new { 
                                taskId = task.TaskId,
                                status = "Completed",
                                size = task.Size,
                                result = resultJson,
                                executionTime,
                                message = $"Matrix {task.Size}x{task.Size} solved successfully"
                            }, stoppingToken);
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] TASK COMPLETED SUCCESSFULLY");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Task ID       : {task.TaskId}");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Execution Time: {executionTime:F2}s");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] History ID    : {history.Id}");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] SignalR Sent  : YES");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                        Console.WriteLine($"");
                        Console.ResetColor();
                        _logger.LogInformation($"[WORKER-{_workerNumber}] Task {task.TaskId} completed in {executionTime:F2}s");
                    }
                    catch (OperationCanceledException)
                    {
                        var executionTime = (DateTime.UtcNow - startTime).TotalSeconds;
                        
                        await queueService.UpdateTaskStatusAsync(task.TaskId, Models.TaskStatus.Cancelled, "Task was cancelled by user");
                        
                        var history = new CalculationHistory
                        {
                            UserId = task.UserId,
                            Size = task.Size,
                            MatrixData = JsonSerializer.Serialize(new { matrixId = task.MatrixId }, JsonOptions),
                            Solution = "[]",
                            Success = false,
                            ErrorMessage = "Task was cancelled by user",
                            CreatedAt = DateTime.UtcNow
                        };
                        
                        dbContext.CalculationHistories.Add(history);
                        await dbContext.SaveChangesAsync(stoppingToken);
                        
                        await _hubContext.Clients.Group(groupName)
                            .SendAsync("TaskFailed", new { 
                                taskId = task.TaskId,
                                status = "Cancelled",
                                size = task.Size,
                                error = "Task was cancelled by user",
                                message = $"Matrix {task.Size}x{task.Size} calculation was cancelled"
                            }, stoppingToken);
                        
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] TASK CANCELLED BY USER");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Task ID       : {task.TaskId}");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Execution Time: {executionTime:F2}s (partial)");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                        Console.WriteLine($"");
                        Console.ResetColor();
                        _logger.LogWarning($"[WORKER-{_workerNumber}] Task {task.TaskId} was cancelled after {executionTime:F2}s");
                    }
                    catch (Exception ex)
                    {
                        await queueService.UpdateTaskStatusAsync(task.TaskId, Models.TaskStatus.Failed, ex.Message);
                        
                        var history = new CalculationHistory
                        {
                            UserId = task.UserId,
                            Size = task.Size,
                            MatrixData = JsonSerializer.Serialize(new { matrixId = task.MatrixId }, JsonOptions),
                            Solution = "[]",
                            Success = false,
                            ErrorMessage = ex.Message,
                            CreatedAt = DateTime.UtcNow
                        };
                        
                        dbContext.CalculationHistories.Add(history);
                        await dbContext.SaveChangesAsync(stoppingToken);
                        
                        await _hubContext.Clients.Group(groupName)
                            .SendAsync("TaskFailed", new { 
                                taskId = task.TaskId,
                                status = "Failed",
                                size = task.Size,
                                error = ex.Message,
                                message = $"Failed to solve matrix {task.Size}x{task.Size}"
                            }, stoppingToken);
                        
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] TASK FAILED");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Task ID: {task.TaskId}");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Error  : {ex.Message}");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                        Console.WriteLine($"");
                        Console.ResetColor();
                        _logger.LogError($"[WORKER-{_workerNumber}] Task {task.TaskId} failed: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[WORKER-{_workerNumber}] Error in worker loop: {ex.Message}");
                    await Task.Delay(5000, stoppingToken);
                }
            }
            
            _logger.LogInformation($"[WORKER-{_workerNumber}] Matrix Worker stopped");
        }
    }
}

