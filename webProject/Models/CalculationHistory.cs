using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace webProject.Models;

public class CalculationHistory
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
    
    [Required]
    public int Size { get; set; }
    
    [Required]
    public string MatrixData { get; set; } = string.Empty; 
    
    [Required]
    public string Solution { get; set; } = string.Empty; 
    
    public bool Success { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

