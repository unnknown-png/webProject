namespace webProject.Models;

public class ProgressInfo
{
    public int Percent { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum CalculationStage
{
    Initializing,
    ForwardElimination,
    BackSubstitution,
    LUDecomposition,
    Finalizing,
    Completed,
    Cancelled,
    Failed
}

