using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Text.Json;
using webProject.Data;
using webProject.Hubs;
using webProject.Models;
using webProject.Services;
using webProject.Helpers;
using webProject.Constants;

namespace webProject.Controllers;

[Authorize]
public class MatrixController : Controller
{
    private readonly IGaussianEliminationService _gaussService;
    private readonly ICombinedMatrixService _combinedService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MatrixController> _logger;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<ProgressHub> _hubContext;
    private readonly ITaskManager _taskManager;
    private readonly IConfiguration _configuration;
    private readonly string _serverName;
    private readonly IRedisQueueService _queueService;


    public MatrixController(
        IGaussianEliminationService gaussService,
        ICombinedMatrixService combinedService,
        ApplicationDbContext context,
        ILogger<MatrixController> logger,
        IMemoryCache cache,
        IHubContext<ProgressHub> hubContext,
        ITaskManager taskManager,
        IConfiguration configuration,
        IRedisQueueService queueService)
    {
        _gaussService = gaussService;
        _combinedService = combinedService;
        _context = context;
        _logger = logger;
        _cache = cache;
        _hubContext = hubContext;
        _taskManager = taskManager;
        _configuration = configuration;
        _serverName = _configuration["ServerInfo:ServerName"] ?? "UNKNOWN-SERVER";
        _queueService = queueService;
    }

    // API: Solve matrix system (for small matrices < 10)
    [HttpPost]
    [Route("api/matrix/solve")]
    public async Task<IActionResult> Solve([FromBody] MatrixRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = "Invalid request data" });
        }

        // Get user ID (declared at method level for catch block access)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        try
        {
            // Check if user can create new task (max 3 concurrent)
            if (!_taskManager.CanCreateTask(userId))
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = "Maximum concurrent tasks limit reached (3). Please wait for a task to complete or cancel one." 
                });
            }
            
            var size = request.Coefficients.Length;
            
            // Log matrix solving request
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{_serverName}] Received matrix solve request - Size: {size}x{size}");
            Console.ResetColor();
            
            // Validate matrix size
            if (size < MatrixConstants.MinMatrixSize || size > MatrixConstants.MaxMatrixSize)
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = $"Matrix size must be between {MatrixConstants.MinMatrixSize} and {MatrixConstants.MaxMatrixSize}" 
                });
            }

            // Validate matrix values (NaN, Infinity, too large numbers)
            if (!ValidateMatrixValues(request.Coefficients, request.RightHandSide))
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = "Matrix contains invalid values (NaN, Infinity, or values exceeding allowed range)" 
                });
            }

            // For display optimization: small matrices (<10) can be sent directly
            if (size >= MatrixConstants.SmallMatrixThreshold)
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = $"For matrices >= {MatrixConstants.SmallMatrixThreshold}x{MatrixConstants.SmallMatrixThreshold}, please use the generate and solve-stored endpoints" 
                });
            }

            // Create task ID for tracking (use client-provided taskId or generate new one)
            var taskId = !string.IsNullOrEmpty(request.TaskId) 
                ? _taskManager.CreateTask(request.TaskId)
                : _taskManager.CreateTask();
            
            // Associate task with user
            _taskManager.AssociateTaskWithUser(taskId, userId);
            
            var cts = _taskManager.GetCancellationToken(taskId);

            // Create progress reporter
            var progress = new Progress<ProgressInfo>(async info =>
            {
                try
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveProgress", taskId, info.Percent, info.Stage, info.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending progress update");
                }
            });

            // Use combined service to solve and decompose simultaneously
            var result = await _combinedService.SolveAndDecomposeAsync(
                request.Coefficients, 
                request.RightHandSide, 
                progress,
                cts?.Token ?? default);
            
            // Cleanup task
            _taskManager.RemoveTask(taskId);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{_serverName}] Successfully solved matrix {size}x{size} - Time: {result.ComputationTimeSeconds:F3}s");
            Console.ResetColor();

            // Save to history (userId already parsed at the beginning)
            var history = new CalculationHistory
            {
                UserId = userId,
                Size = request.Coefficients.Length,
                MatrixData = JsonSerializer.Serialize(request),
                Solution = JsonSerializer.Serialize(result.GaussianSolution.Solution),
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CreatedAt = TimeZoneHelper.UtcNow
            };

            _context.CalculationHistories.Add(history);
            await _context.SaveChangesAsync();

            // Check for invalid numbers (Infinity, NaN)
            double? determinant = null;
            if (result.LUDecomposition.Success)
            {
                var det = result.LUDecomposition.Determinant;
                if (!double.IsNaN(det) && !double.IsInfinity(det))
                {
                    determinant = det;
                }
            }

            return Ok(new
            {
                success = result.Success,
                solution = result.GaussianSolution.Solution,
                error = result.ErrorMessage,
                size = result.Size,
                solvedAt = result.SolvedAt,
                determinant = determinant,
                luDecomposition = result.LUDecomposition.Success && size < MatrixConstants.SmallMatrixThreshold ? new
                {
                    lMatrix = result.LUDecomposition.LMatrix,
                    uMatrix = result.LUDecomposition.UMatrix
                } : null,
                computationTime = result.ComputationTimeSeconds,
                taskId
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Matrix calculation was cancelled by user");
            
            // Save cancellation to history (userId already parsed at the beginning)
            var history = new CalculationHistory
            {
                UserId = userId,
                Size = request.Coefficients.Length,
                MatrixData = JsonSerializer.Serialize(request),
                Solution = "[]",
                Success = false,
                ErrorMessage = "Calculation was cancelled by user",
                CreatedAt = TimeZoneHelper.UtcNow
            };

            _context.CalculationHistories.Add(history);
            await _context.SaveChangesAsync();
            
            return StatusCode(408, new 
            { 
                success = false, 
                error = "Calculation was cancelled by user"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error solving matrix. Size: {Size}", request.Coefficients?.Length ?? 0);
            
            // Try to save error to history
            try
            {
                var history = new CalculationHistory
                {
                    UserId = userId,
                    Size = request.Coefficients?.Length ?? 0,
                    MatrixData = JsonSerializer.Serialize(new { size = request.Coefficients?.Length ?? 0 }),
                    Solution = "[]",
                    Success = false,
                    ErrorMessage = $"Error: {ex.Message}",
                    CreatedAt = TimeZoneHelper.UtcNow
                };

                _context.CalculationHistories.Add(history);
                await _context.SaveChangesAsync();
            }
            catch (Exception historyEx)
            {
                _logger.LogError(historyEx, "Failed to save error to history");
            }
            
            return StatusCode(500, new 
            { 
                success = false, 
                error = "Internal server error",
                details = ex.Message
            });
        }
    }

    // API: Generate random matrix
    [HttpPost]
    [Route("api/matrix/generate")]
    public async Task<IActionResult> Generate([FromBody] MatrixGenerateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = "Invalid request data" });
        }

        // Validate matrix size
        if (request.Size < MatrixConstants.MinMatrixSize || request.Size > MatrixConstants.MaxMatrixSize)
        {
            return BadRequest(new 
            { 
                success = false, 
                error = $"Matrix size must be between {MatrixConstants.MinMatrixSize} and {MatrixConstants.MaxMatrixSize}" 
            });
        }

        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{_serverName}] MATRIX GENERATION REQUEST");
            Console.WriteLine($"[{_serverName}] Size: {request.Size}x{request.Size}");
            Console.ResetColor();
            
            var matrix = _gaussService.GenerateRandomMatrix(request.Size, request.MinValue, request.MaxValue);
            
            // For large matrices (>= 10), store in cache and return matrixId
            if (request.Size >= MatrixConstants.SmallMatrixThreshold)
            {
                var matrixId = Guid.NewGuid().ToString();
                
                // Store in Redis cache (shared across all servers)
                await _queueService.StoreMatrixAsync(matrixId, matrix, TimeSpan.FromMinutes(MatrixConstants.MatrixCacheExpirationMinutes));
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{_serverName}] MATRIX GENERATED SUCCESSFULLY");
                Console.WriteLine($"[{_serverName}] Matrix ID: {matrixId}");
                Console.WriteLine($"[{_serverName}] Cached in Redis for 30 minutes");
                Console.WriteLine($"");
                Console.ResetColor();
                
                _logger.LogInformation($"Generated and cached matrix {request.Size}x{request.Size} with ID: {matrixId}");
                
                return Ok(new
                {
                    success = true,
                    matrixId,
                    size = matrix.Size,
                    message = $"Matrix {request.Size}×{request.Size} generated and ready for computation"
                });
            }
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{_serverName}] SMALL MATRIX GENERATED");
            Console.WriteLine($"[{_serverName}] Size: {request.Size}x{request.Size} (direct return)");
            Console.WriteLine($"");
            Console.ResetColor();
            
            // For small matrices, return data directly
            return Ok(new
            {
                success = true,
                coefficients = matrix.Coefficients,
                rightHandSide = matrix.RightHandSide,
                size = matrix.Size
            });
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{_serverName}] ========================================");
            Console.WriteLine($"[{_serverName}] ERROR IN MATRIX GENERATION");
            Console.WriteLine($"[{_serverName}] Message: {ex.Message}");
            Console.WriteLine($"[{_serverName}] ========================================");
            Console.WriteLine($"");
            Console.ResetColor();
            
            _logger.LogError(ex, "Error generating matrix");
            return StatusCode(500, new { success = false, error = "Internal server error", details = ex.Message });
        }
    }

    // API: Solve stored matrix (for large matrices)
    [HttpPost]
    [Route("api/matrix/solve-stored")]
    [SuppressMessage("ReSharper.DPA", "DPA0000: DPA issues")]
    public async Task<IActionResult> SolveStored([FromBody] StoredMatrixRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = "Invalid request data" });
        }

        // Get user ID (declared at method level for catch block access)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        try
        {
            // Check if user can create new task (max 3 concurrent)
            if (!_taskManager.CanCreateTask(userId))
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = "Maximum concurrent tasks limit reached (3). Please wait for a task to complete or cancel one." 
                });
            }
            
            // Get matrix from Redis cache (shared across all servers)
            var matrix = await _queueService.GetMatrixAsync(request.MatrixId);
            
            if (matrix == null)
            {
                return NotFound(new { success = false, error = "Matrix not found or expired. Please generate a new one." });
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{_serverName}] ----------------------------------------");
            Console.WriteLine($"[{_serverName}] ENQUEUE TASK REQUEST");
            Console.WriteLine($"[{_serverName}] Matrix ID  : {request.MatrixId}");
            Console.WriteLine($"[{_serverName}] Matrix Size: {matrix.Size}x{matrix.Size}");
            Console.WriteLine($"[{_serverName}] User ID    : {userId}");
            Console.WriteLine($"[{_serverName}] ----------------------------------------");
            Console.ResetColor();

            _logger.LogInformation($"Enqueuing matrix {matrix.Size}x{matrix.Size} to Redis queue for user {userId}");
            
            // Use taskId from client if provided, otherwise generate new one
            var taskId = request.TaskId ?? $"task_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString("N")[..8]}";
            
            // Create MatrixTask for Redis queue
            var matrixTask = new MatrixTask
            {
                TaskId = taskId,  // Use client's taskId!
                MatrixId = request.MatrixId,
                UserId = userId,
                Size = matrix.Size,
                Method = "LU",
                Status = Models.TaskStatus.Queued
            };
            
            // Enqueue task to Redis - workers will pick it up
            
            // Associate task with user in TaskManager for tracking
            _taskManager.AssociateTaskWithUser(taskId, userId);
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{_serverName}] TASK ENQUEUED SUCCESSFULLY");
            Console.WriteLine($"[{_serverName}] Task ID: {taskId}");
            Console.WriteLine($"[{_serverName}] Status : Queued for worker processing");
            Console.WriteLine($"");
            Console.ResetColor();

            // Notify user via SignalR using Groups (works with Redis Backplane)
            var groupName = $"user_{userId}";
            await _hubContext.Clients.Group(groupName)
                .SendAsync("TaskQueued", new { 
                    taskId,
                    status = "Queued",
                    size = matrix.Size,
                    message = $"Matrix {matrix.Size}×{matrix.Size} added to processing queue"
                });

            // Return immediately - workers will process asynchronously
            return Ok(new
            {
                success = true,
                taskId,
                size = matrix.Size,
                status = "queued",
                message = $"Matrix {matrix.Size}×{matrix.Size} added to queue. You will be notified when complete."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing stored matrix. MatrixId: {MatrixId}", request.MatrixId);
            
            return StatusCode(500, new 
            { 
                success = false, 
                error = "Internal server error", 
                details = ex.Message
            });
        }
    }

    // API: Cancel task
    [HttpPost]
    [Route("api/matrix/cancel/{taskId}")]
    public IActionResult CancelTask(string taskId)
    {
        try
        {
            _taskManager.CancelTask(taskId);
            _logger.LogInformation($"Task {taskId} cancellation requested");
            return Ok(new { success = true, message = "Task cancellation requested" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling task");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    // Private helper method for validating matrix values
    private bool ValidateMatrixValues(double[][] coefficients, double[] rightHandSide)
    {
        // Validate coefficients
        foreach (var row in coefficients)
        {
            foreach (var value in row)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    return false;
                }
                if (Math.Abs(value) > MatrixConstants.MaxMatrixValue)
                {
                    return false;
                }
            }
        }

        // Validate right hand side
        foreach (var value in rightHandSide)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }
            if (Math.Abs(value) > MatrixConstants.MaxMatrixValue)
            {
                return false;
            }
        }

        return true;
    }
    
    // API: Queue matrix for async processing (NEW - uses Redis Queue + Background Workers)
    [HttpPost]
    [Route("api/matrix/queue-solve")]
    public async Task<IActionResult> QueueSolveMatrix([FromBody] StoredMatrixRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = "Invalid request data" });
        }

        // Get user ID
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        try
        {
            var cacheKey = $"matrix_{request.MatrixId}";
            
            if (!_cache.TryGetValue<GeneratedMatrix>(cacheKey, out var matrix) || matrix == null)
            {
                return NotFound(new { success = false, error = "Matrix not found or expired. Please generate a new one." });
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{_serverName}] Queueing matrix task - Size: {matrix.Size}x{matrix.Size}, User: {userId}");
            Console.ResetColor();

            // Create task for Redis queue
            var task = new MatrixTask
            {
                UserId = userId,
                MatrixId = request.MatrixId,
                Size = matrix.Size,
                Method = "LU", // Default to LU decomposition
                Status = Models.TaskStatus.Queued
            };

            // Enqueue task
            var taskId = await _queueService.EnqueueTaskAsync(task);

            _logger.LogInformation($"Matrix task {taskId} queued for user {userId} - Size: {matrix.Size}x{matrix.Size}");

            // Notify user via SignalR that task is queued
            await _hubContext.Clients.User(userId.ToString())
                .SendAsync("TaskQueued", new { 
                    taskId = taskId,
                    status = "Queued",
                    size = matrix.Size,
                    message = $"Matrix {matrix.Size}x{matrix.Size} added to processing queue"
                });

            return Ok(new
            {
                success = true,
                taskId = taskId,
                message = "Matrix task queued successfully",
                status = "Queued",
                size = matrix.Size
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing matrix task");
            return StatusCode(500, new { success = false, error = "Internal server error", details = ex.Message });
        }
    }
    
    // API: Get task status from Redis
    [HttpGet]
    [Route("api/matrix/task-status/{taskId}")]
    public async Task<IActionResult> GetTaskStatus(string taskId)
    {
        try
        {
            var task = await _queueService.GetTaskStatusAsync(taskId);
            
            if (task == null)
            {
                return NotFound(new { success = false, error = "Task not found" });
            }
            
            return Ok(new
            {
                success = true,
                taskId = task.TaskId,
                status = task.Status.ToString(),
                size = task.Size,
                method = task.Method,
                createdAt = task.CreatedAt,
                startedAt = task.StartedAt,
                completedAt = task.CompletedAt,
                executionTime = task.ExecutionTime,
                errorMessage = task.ErrorMessage,
                result = task.ResultJson
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task status");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }
    
    // API: Get all user tasks from Redis
    [HttpGet]
    [Route("api/matrix/my-tasks")]
    public async Task<IActionResult> GetMyTasks()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        try
        {
            var tasks = await _queueService.GetUserTasksAsync(userId);
            
            return Ok(new
            {
                success = true,
                tasks = tasks.Select(t => new
                {
                    taskId = t.TaskId,
                    status = t.Status.ToString(),
                    size = t.Size,
                    method = t.Method,
                    createdAt = t.CreatedAt,
                    startedAt = t.StartedAt,
                    completedAt = t.CompletedAt,
                    executionTime = t.ExecutionTime,
                    errorMessage = t.ErrorMessage
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user tasks");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }
}

