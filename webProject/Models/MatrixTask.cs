namespace webProject.Models
{
    public class MatrixTask
    {
        public string TaskId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string MatrixId { get; set; } = string.Empty;
        public int Size { get; set; }
        public string Method { get; set; } = string.Empty; // "Gaussian" or "LU"
        public TaskStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public double? ExecutionTime { get; set; }
        
        // Result data (stored as JSON in Redis, then moved to DB)
        public string? ResultJson { get; set; }
    }
    
    public enum TaskStatus
    {
        Queued,      // Задача в черзі
        Processing,  // Задача обробляється
        Completed,   // Задача виконана успішно
        Failed,      // Задача завершилась помилкою
        Cancelled    // Задача скасована
    }
}

