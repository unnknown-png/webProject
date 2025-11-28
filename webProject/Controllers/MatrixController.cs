using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Text.Json;
using webProject.Data;
using webProject.Hubs;
using webProject.Models;
using webProject.Services;

namespace webProject.Controllers;

[Authorize]
public class MatrixController : Controller
{
    private readonly IGaussianEliminationService _gaussService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MatrixController> _logger;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<ProgressHub> _hubContext;
    private readonly ITaskManager _taskManager;

    public MatrixController(
        IGaussianEliminationService gaussService,
        ApplicationDbContext context,
        ILogger<MatrixController> logger,
        IMemoryCache cache,
        IHubContext<ProgressHub> hubContext,
        ITaskManager taskManager)
    {
        _gaussService = gaussService;
        _context = context;
        _logger = logger;
        _cache = cache;
        _hubContext = hubContext;
        _taskManager = taskManager;
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

        try
        {
            var size = request.Coefficients.Length;
            
            // Validate matrix size
            if (size >= 10)
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = "For matrices >= 10x10, please use the generate and solve-stored endpoints" 
                });
            }

            // Create task ID for tracking (use client-provided taskId or generate new one)
            var taskId = !string.IsNullOrEmpty(request.TaskId) 
                ? _taskManager.CreateTask(request.TaskId)
                : _taskManager.CreateTask();
            
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

            var solution = await _gaussService.SolveAsync(
                request.Coefficients, 
                request.RightHandSide, 
                progress,
                cts?.Token ?? default);
            
            // Cleanup task
            _taskManager.RemoveTask(taskId);

            // Save to history if user is authenticated
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                var history = new CalculationHistory
                {
                    UserId = userId,
                    Size = request.Coefficients.Length,
                    MatrixData = JsonSerializer.Serialize(request),
                    Solution = JsonSerializer.Serialize(solution.Solution),
                    Success = solution.Success,
                    ErrorMessage = solution.ErrorMessage,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CalculationHistories.Add(history);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                success = solution.Success,
                solution = solution.Solution,
                error = solution.ErrorMessage,
                size = solution.Size,
                solvedAt = solution.SolvedAt,
                taskId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error solving matrix");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    // API: Generate random matrix
    [HttpPost]
    [Route("api/matrix/generate")]
    public IActionResult Generate([FromBody] MatrixGenerateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = "Invalid request data" });
        }

        try
        {
            var matrix = _gaussService.GenerateRandomMatrix(request.Size, request.MinValue, request.MaxValue);
            
            // For large matrices (>= 10), store in cache and return matrixId
            if (request.Size >= 10)
            {
                var matrixId = Guid.NewGuid().ToString();
                var cacheKey = $"matrix_{matrixId}";
                
                // Store for 30 minutes
                _cache.Set(cacheKey, matrix, TimeSpan.FromMinutes(30));
                
                _logger.LogInformation($"Generated and cached matrix {request.Size}x{request.Size} with ID: {matrixId}");
                
                return Ok(new
                {
                    success = true,
                    matrixId,
                    size = matrix.Size,
                    message = $"Matrix {request.Size}×{request.Size} generated and ready for computation"
                });
            }
            
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
            _logger.LogError(ex, "Error generating matrix");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    // API: Solve stored matrix (for large matrices)
    [HttpPost]
    [Route("api/matrix/solve-stored")]
    public async Task<IActionResult> SolveStored([FromBody] StoredMatrixRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = "Invalid request data" });
        }

        try
        {
            var cacheKey = $"matrix_{request.MatrixId}";
            
            if (!_cache.TryGetValue<GeneratedMatrix>(cacheKey, out var matrix) || matrix == null)
            {
                return NotFound(new { success = false, error = "Matrix not found or expired. Please generate a new one." });
            }

            _logger.LogInformation($"Solving cached matrix {matrix.Size}x{matrix.Size}");
            
            // Create task ID for tracking (use client-provided taskId or generate new one)
            var taskId = !string.IsNullOrEmpty(request.TaskId) 
                ? _taskManager.CreateTask(request.TaskId)
                : _taskManager.CreateTask();
            
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
                    _logger.LogError(ex, "Error sending progress update for stored matrix");
                }
            });

            var solution = await _gaussService.SolveAsync(
                matrix.Coefficients, 
                matrix.RightHandSide,
                progress,
                cts?.Token ?? default);
            
            // Cleanup task
            _taskManager.RemoveTask(taskId);

            // Remove from cache after solving
            _cache.Remove(cacheKey);
            
            // Save to history
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                var history = new CalculationHistory
                {
                    UserId = userId,
                    Size = matrix.Size,
                    MatrixData = JsonSerializer.Serialize(new { size = matrix.Size, matrixId = request.MatrixId }),
                    Solution = JsonSerializer.Serialize(solution.Solution),
                    Success = solution.Success,
                    ErrorMessage = solution.ErrorMessage,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CalculationHistories.Add(history);
                await _context.SaveChangesAsync();
            }

            // For large matrices, return summarized result
            return Ok(new
            {
                success = solution.Success,
                size = solution.Size,
                solutionSummary = solution.Success 
                    ? $"Solution computed successfully. First 5 values: [{string.Join(", ", solution.Solution.Take(5).Select(v => v.ToString("F4")))}...]"
                    : null,
                solutionLength = solution.Solution?.Length ?? 0,
                error = solution.ErrorMessage,
                solvedAt = solution.SolvedAt,
                taskId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error solving stored matrix");
            return StatusCode(500, new { success = false, error = "Internal server error" });
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

    // API: Get user's calculation history
    [HttpGet]
    [Route("api/matrix/history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 20)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var history = await _context.CalculationHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(Math.Min(limit, 100))
                .Select(h => new
                {
                    id = h.Id,
                    size = h.Size,
                    success = h.Success,
                    solution = h.Solution,
                    errorMessage = h.ErrorMessage,
                    createdAt = h.CreatedAt,
                    time = h.CreatedAt.ToString("g")
                })
                .ToListAsync();

            return Ok(new { success = true, history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving history");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    // API: Clear user's calculation history
    [HttpDelete]
    [Route("api/matrix/history")]
    public async Task<IActionResult> ClearHistory()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var historyItems = await _context.CalculationHistories
                .Where(h => h.UserId == userId)
                .ToListAsync();

            _context.CalculationHistories.RemoveRange(historyItems);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "History cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing history");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    // API: Export history as JSON
    [HttpGet]
    [Route("api/matrix/history/export")]
    public async Task<IActionResult> ExportHistory()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var history = await _context.CalculationHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new
                {
                    id = h.Id,
                    size = h.Size,
                    success = h.Success,
                    matrixData = h.MatrixData,
                    solution = h.Solution,
                    errorMessage = h.ErrorMessage,
                    createdAt = h.CreatedAt
                })
                .ToListAsync();

            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return File(
                System.Text.Encoding.UTF8.GetBytes(json),
                "application/json",
                $"gauss_history_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting history");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }
}

