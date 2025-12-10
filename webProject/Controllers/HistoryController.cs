using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using webProject.Data;
using webProject.Helpers;

namespace webProject.Controllers;

[Authorize]
public class HistoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HistoryController> _logger;

    public HistoryController(
        ApplicationDbContext context,
        ILogger<HistoryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Route("api/history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 20)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var historyData = await _context.CalculationHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(Math.Min(limit, 100))
                .ToListAsync();
            
            var history = historyData.Select(h => new
            {
                id = h.Id,
                size = h.Size,
                success = h.Success,
                solution = h.Solution,
                errorMessage = h.ErrorMessage,
                createdAt = TimeZoneHelper.ToKyivTime(h.CreatedAt),
                time = TimeZoneHelper.ToKyivTime(h.CreatedAt).ToString("g")
            }).ToList();

            return Ok(new { success = true, history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving history");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    [HttpDelete]
    [Route("api/history")]
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

    [HttpGet]
    [Route("api/history/export")]
    public async Task<IActionResult> ExportHistory()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var historyData = await _context.CalculationHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
            
            var history = historyData.Select(h => new
            {
                id = h.Id,
                size = h.Size,
                success = h.Success,
                matrixData = h.MatrixData,
                solution = h.Solution,
                errorMessage = h.ErrorMessage,
                createdAt = TimeZoneHelper.ToKyivTime(h.CreatedAt)
            }).ToList();

            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return File(
                System.Text.Encoding.UTF8.GetBytes(json),
                "application/json",
                $"gauss_history_{TimeZoneHelper.KyivNow:yyyyMMdd_HHmmss}.json"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting history");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }
}

