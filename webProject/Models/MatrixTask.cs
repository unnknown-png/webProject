namespace webProject.Models
{
    public class MatrixTask
    {
        public string TaskId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string MatrixId { get; set; } = string.Empty;
        public int Size { get; set; }
        public string Method { get; set; } = string.Empty;
        public TaskStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public double? ExecutionTime { get; set; }
        
        public string? ResultJson { get; set; }
    }
    
    public enum TaskStatus
    {
        Queued,      
        Processing,  
        Completed,   
        Failed,      
        Cancelled    
    }
}

