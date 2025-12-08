using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace webProject.Models;

// Model for storing generated matrices in database (for load balancing)
public class CachedMatrix
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    public int Size { get; set; }
    
    // Serialize matrix data as JSON
    public string CoefficientsJson { get; set; } = string.Empty;
    public string RightHandSideJson { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

