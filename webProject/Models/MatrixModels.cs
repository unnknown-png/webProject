using System.ComponentModel.DataAnnotations;

namespace webProject.Models;

public class MatrixRequest
{
    [Required]
    public double[][] Coefficients { get; set; } = Array.Empty<double[]>();
    
    [Required]
    public double[] RightHandSide { get; set; } = Array.Empty<double>();
    
    public string? TaskId { get; set; }
}

public class StoredMatrixRequest
{
    [Required]
    public string MatrixId { get; set; } = string.Empty;
    
    public string? TaskId { get; set; }
}

public class MatrixSolution
{
    public double[] Solution { get; set; } = Array.Empty<double>();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int Size { get; set; }
    public DateTime SolvedAt { get; set; }
}

public class MatrixGenerateRequest
{
    [Required]
    [Range(1, 10000)]
    public int Size { get; set; }
    
    [Range(-1000, 1000)]
    public double MinValue { get; set; } = -10;
    
    [Range(-1000, 1000)]
    public double MaxValue { get; set; } = 10;
}

public class GeneratedMatrix
{
    public double[][] Coefficients { get; set; } = Array.Empty<double[]>();
    public double[] RightHandSide { get; set; } = Array.Empty<double>();
    public int Size { get; set; }
}

