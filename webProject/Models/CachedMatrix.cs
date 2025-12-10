using System.ComponentModel.DataAnnotations;

namespace webProject.Models;

public class CachedMatrix
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    public int Size { get; set; }
    
    public string CoefficientsJson { get; set; } = string.Empty;
    public string RightHandSideJson { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

