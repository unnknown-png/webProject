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


    public MatrixController(
        IGaussianEliminationService gaussService,
        ICombinedMatrixService combinedService,
        ApplicationDbContext context,
        ILogger<MatrixController> logger,
        IMemoryCache cache,
        IHubContext<ProgressHub> hubContext,
        ITaskManager taskManager)
    {
        _gaussService = gaussService;
        _combinedService = combinedService;
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
    public IActionResult Generate([FromBody] MatrixGenerateRequest request)
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
            var matrix = _gaussService.GenerateRandomMatrix(request.Size, request.MinValue, request.MaxValue);
            
            // For large matrices (>= 10), store in cache and return matrixId
            if (request.Size >= MatrixConstants.SmallMatrixThreshold)
            {
                var matrixId = Guid.NewGuid().ToString();
                var cacheKey = $"matrix_{matrixId}";
                
                // Store for 30 minutes
                _cache.Set(cacheKey, matrix, TimeSpan.FromMinutes(MatrixConstants.MatrixCacheExpirationMinutes));
                
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

        string? taskId = null;

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
            
            var cacheKey = $"matrix_{request.MatrixId}";
            
            if (!_cache.TryGetValue<GeneratedMatrix>(cacheKey, out var matrix) || matrix == null)
            {
                return NotFound(new { success = false, error = "Matrix not found or expired. Please generate a new one." });
            }

            _logger.LogInformation($"Solving cached matrix {matrix.Size}x{matrix.Size} with LU decomposition");
            
            // Create task ID for tracking (use client-provided taskId or generate new one)
            taskId = !string.IsNullOrEmpty(request.TaskId) 
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
                    _logger.LogError(ex, "Error sending progress update for stored matrix");
                }
            });

            // Use combined service to solve and decompose simultaneously
            var result = await _combinedService.SolveAndDecomposeAsync(
                matrix.Coefficients, 
                matrix.RightHandSide,
                progress,
                cts?.Token ?? default);
            
            // Cleanup task
            _taskManager.RemoveTask(taskId);

            // Remove from cache after solving
            _cache.Remove(cacheKey);
            
            // Save to history (userId already parsed above)
            var history = new CalculationHistory
            {
                UserId = userId,
                Size = matrix.Size,
                MatrixData = JsonSerializer.Serialize(new { size = matrix.Size, matrixId = request.MatrixId }),
                Solution = JsonSerializer.Serialize(result.GaussianSolution.Solution),
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CreatedAt = TimeZoneHelper.UtcNow
            };

            _context.CalculationHistories.Add(history);
            await _context.SaveChangesAsync();

            // Prepare response - don't send large arrays for big matrices
            // Also check for invalid numbers (Infinity, NaN)
            double? determinant = null;
            if (result.LUDecomposition.Success)
            {
                var det = result.LUDecomposition.Determinant;
                if (!double.IsNaN(det) && !double.IsInfinity(det))
                {
                    determinant = det;
                }
                else
                {
                    _logger.LogWarning($"Invalid determinant value for matrix {matrix.Size}x{matrix.Size}: {det} - this is normal for large matrices");
                }
            }
            
            // Check if solution contains invalid values
            bool solutionValid = result.GaussianSolution.Solution.All(v => !double.IsNaN(v) && !double.IsInfinity(v));
            
            object response;
            
            if (matrix.Size < MatrixConstants.SmallMatrixThreshold)
            {
                // For small matrices, send everything including LU matrices
                response = new
                {
                    success = result.Success,
                    size = result.Size,
                    solution = result.GaussianSolution.Solution,
                    determinant = determinant,
                    luDecomposition = result.LUDecomposition.Success ? new
                    {
                        lMatrix = result.LUDecomposition.LMatrix,
                        uMatrix = result.LUDecomposition.UMatrix
                    } : null,
                    error = result.ErrorMessage,
                    solvedAt = result.SolvedAt,
                    computationTime = result.ComputationTimeSeconds,
                    taskId
                };
            }
            else
            {
                // For large matrices, send only summary to avoid JSON overflow
                var summaryMessage = result.GaussianSolution.Success && solutionValid
                    ? $"Solution computed successfully. First 5 values: [{string.Join(", ", result.GaussianSolution.Solution.Take(5).Select(v => v.ToString("F4")))}...]"
                    : "Solution computed (contains special values)";
                
                response = new
                {
                    success = result.Success,
                    size = result.Size,
                    solutionSummary = summaryMessage,
                    solutionLength = result.GaussianSolution.Solution?.Length ?? 0,
                    determinant = determinant,
                    luNote = $"LU decomposition matrices are not displayed for large matrices (size >= {MatrixConstants.SmallMatrixThreshold}) to avoid performance issues",
                    error = result.ErrorMessage,
                    solvedAt = result.SolvedAt,
                    computationTime = result.ComputationTimeSeconds,
                    taskId
                };
            }

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            if (taskId != null)
            {
                _taskManager.RemoveTask(taskId);
            }
            
            _logger.LogWarning("Stored matrix calculation was cancelled by user");
            
            // Save cancellation to history (userId already parsed at the beginning)
            // Try to get matrix from cache to know the size
            var cacheKey = $"matrix_{request.MatrixId}";
            int matrixSize = 0;
            if (_cache.TryGetValue<GeneratedMatrix>(cacheKey, out var cachedMatrix))
            {
                matrixSize = cachedMatrix?.Size ?? 0;
            }
            
            var history = new CalculationHistory
            {
                UserId = userId,
                Size = matrixSize > 0 ? matrixSize : 0,
                MatrixData = JsonSerializer.Serialize(new { matrixId = request.MatrixId, size = matrixSize }),
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
                error = "Calculation was cancelled by user",
                taskId
            });
        }
        catch (Exception ex)
        {
            if (taskId != null)
            {
                _taskManager.RemoveTask(taskId);
            }
            
            _logger.LogError(ex, "Error solving stored matrix. TaskId: {TaskId}, MatrixId: {MatrixId}", 
                taskId, request.MatrixId);
            
            // Try to save error to history
            try
            {
                var cacheKey = $"matrix_{request.MatrixId}";
                int matrixSize = 0;
                if (_cache.TryGetValue<GeneratedMatrix>(cacheKey, out var cachedMatrix))
                {
                    matrixSize = cachedMatrix?.Size ?? 0;
                }
                
                var history = new CalculationHistory
                {
                    UserId = userId,
                    Size = matrixSize,
                    MatrixData = JsonSerializer.Serialize(new { matrixId = request.MatrixId, size = matrixSize }),
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
                details = ex.Message,
                taskId 
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
}

