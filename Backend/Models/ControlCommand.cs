using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IoTMonitoringPlatform.Models;

public class ControlCommand
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    
    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Command { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Parameters { get; set; }
    
    public CommandStatus Status { get; set; } = CommandStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? SentAt { get; set; }
    
    public DateTime? ExecutedAt { get; set; }
    
    public DateTime? FailedAt { get; set; }
    
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    
    [MaxLength(1000)]
    public string? Result { get; set; }
    
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
    
    public int? RetryCount { get; set; }
    
    public int? MaxRetries { get; set; }
    
    public int TimeoutSeconds { get; set; } = 30;
    
    public CommandPriority Priority { get; set; } = CommandPriority.Normal;
    
    [MaxLength(500)]
    public string? Metadata { get; set; }
}

public enum CommandStatus
{
    Pending,
    Queued,
    Sent,
    Executing,
    Executed,
    Failed,
    TimedOut,
    Cancelled
}

public enum CommandPriority
{
    Low,
    Normal,
    High,
    Critical
}
