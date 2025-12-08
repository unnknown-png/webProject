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
        
        // JSON options to handle Infinity and NaN values
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
                    
                    // Try to dequeue a task
                    var task = await queueService.DequeueTaskAsync();
                    
                    if (task == null)
                    {
                        // No tasks in queue, wait a bit
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }
                    
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"⚙️  [WORKER-{_workerNumber}] Processing task {task.TaskId} - Size: {task.Size}x{task.Size}, User: {task.UserId}");
                    Console.ResetColor();
                    _logger.LogInformation($"[WORKER-{_workerNumber}] Processing task {task.TaskId} - Size: {task.Size}x{task.Size}");
                    
                    // Notify user that processing started
                    await _hubContext.Clients.User(task.UserId.ToString())
                        .SendAsync("TaskStatusChanged", new { 
                            taskId = task.TaskId, 
                            status = "Processing",
                            message = $"Worker-{_workerNumber} started processing your matrix {task.Size}x{task.Size}"
                        }, stoppingToken);
                    
                    var startTime = DateTime.UtcNow;
                    
                    try
                    {
                        // Get matrix from Redis cache using MatrixId
                        var matrixData = await queueService.GetMatrixAsync(task.MatrixId);
                        
                        if (matrixData == null)
                        {
                            throw new Exception($"Matrix {task.MatrixId} not found in Redis cache");
                        }
                        
                        // Solve the matrix using combined service
                        var result = await combinedService.SolveAndDecomposeAsync(
                            matrixData.Coefficients, 
                            matrixData.RightHandSide,
                            cancellationToken: stoppingToken
                        );
                        
                        var executionTime = (DateTime.UtcNow - startTime).TotalSeconds;
                        
                        // Save result to database
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
                        
                        // Serialize result with support for Infinity/NaN
                        var resultJson = JsonSerializer.Serialize(new
                        {
                            solution = result.GaussianSolution.Solution,
                            determinant = result.LUDecomposition.Determinant,
                            executionTime,
                            historyId = history.Id
                        }, JsonOptions);
                        
                        // Mark task as completed in Redis
                        await queueService.CompleteTaskAsync(task.TaskId, resultJson, executionTime);
                        
                        // Notify user via SignalR
                        await _hubContext.Clients.User(task.UserId.ToString())
                            .SendAsync("TaskCompleted", new { 
                                taskId = task.TaskId,
                                status = "Completed",
                                result = resultJson,
                                executionTime,
                                message = $"Matrix {task.Size}x{task.Size} solved successfully by Worker-{_workerNumber}"
                            }, stoppingToken);
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] TASK COMPLETED SUCCESSFULLY");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Task ID       : {task.TaskId}");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] Execution Time: {executionTime:F2}s");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] History ID    : {history.Id}");
                        Console.WriteLine($"[WORKER-{_workerNumber:D2}] ════════════════════════════════════");
                        Console.WriteLine($"");
                        Console.ResetColor();
                        _logger.LogInformation($"[WORKER-{_workerNumber}] Task {task.TaskId} completed in {executionTime:F2}s");
                    }
                    catch (Exception ex)
                    {
                        // Mark task as failed
                        await queueService.UpdateTaskStatusAsync(task.TaskId, Models.TaskStatus.Failed, ex.Message);
                        
                        // Save failed attempt to database
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
                        
                        // Notify user via SignalR
                        await _hubContext.Clients.User(task.UserId.ToString())
                            .SendAsync("TaskFailed", new { 
                                taskId = task.TaskId,
                                status = "Failed",
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
                    await Task.Delay(5000, stoppingToken); // Wait 5 seconds on error
                }
            }
            
            _logger.LogInformation($"[WORKER-{_workerNumber}] Matrix Worker stopped");
        }
    }
}

