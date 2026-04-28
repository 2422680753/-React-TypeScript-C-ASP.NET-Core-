using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IoTMonitoringPlatform.Models;

public class AlarmDeduplication
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid DeviceId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Metric { get; set; } = string.Empty;
    
    [Required]
    public ComparisonOperator Operator { get; set; }
    
    public double Threshold { get; set; }
    
    public double TriggeredValue { get; set; }
    
    public int OccurrenceCount { get; set; } = 1;
    
    public DateTime FirstTriggeredAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastTriggeredAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ExpiresAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public int SuppressionLevel { get; set; } = 0;
    
    [MaxLength(100)]
    public string? CorrelationId { get; set; }
}

public class AlarmSuppression
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public SuppressionType Type { get; set; }
    
    public Guid? DeviceId { get; set; }
    
    [MaxLength(100)]
    public string? Metric { get; set; }
    
    public AlarmLevel? MinLevel { get; set; }
    
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    public DateTime EndTime { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public int SuppressedCount { get; set; } = 0;
    
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum SuppressionType
{
    Maintenance,
    KnownIssue,
    PlannedDowntime,
    Custom
}

public class AlarmAggregation
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;
    
    public Guid? DeviceId { get; set; }
    
    [MaxLength(100)]
    public string? GroupKey { get; set; }
    
    public AlarmLevel HighestLevel { get; set; } = AlarmLevel.Information;
    
    public int AlarmCount { get; set; } = 0;
    
    [MaxLength(200)]
    public string AggregatedTitle { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? AggregatedDescription { get; set; }
    
    public DateTime FirstOccurredAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastOccurredAt { get; set; } = DateTime.UtcNow;
    
    public bool IsResolved { get; set; } = false;
    
    public DateTime? ResolvedAt { get; set; }
    
    public virtual ICollection<Alarm> Alarms { get; set; } = new List<Alarm>();
}

public class AlarmRateLimit
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string RuleKey { get; set; } = string.Empty;
    
    public Guid? DeviceId { get; set; }
    
    [MaxLength(100)]
    public string? Metric { get; set; }
    
    public int MaxAlarmsPerMinute { get; set; } = 10;
    
    public int MaxAlarmsPerHour { get; set; } = 100;
    
    public int CurrentMinuteCount { get; set; } = 0;
    
    public int CurrentHourCount { get; set; } = 0;
    
    public DateTime MinuteResetAt { get; set; } = DateTime.UtcNow;
    
    public DateTime HourResetAt { get; set; } = DateTime.UtcNow;
    
    public int DroppedCount { get; set; } = 0;
    
    public bool IsEnabled { get; set; } = true;
}

public class AlarmQueueItem
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid DeviceId { get; set; }
    
    public Guid? RuleId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public AlarmLevel Level { get; set; } = AlarmLevel.Information;
    
    public double? TriggeredValue { get; set; }
    
    [MaxLength(50)]
    public string? TriggeredMetric { get; set; }
    
    public int Priority { get; set; } = 0;
    
    public QueueItemStatus Status { get; set; } = QueueItemStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ProcessedAt { get; set; }
    
    public DateTime? FailedAt { get; set; }
    
    public int RetryCount { get; set; } = 0;
    
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
    
    [MaxLength(500)]
    public string? Metadata { get; set; }
}

public enum QueueItemStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    Dropped
}

public class AlarmBatch
{
    [Key]
    public Guid Id { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ProcessedAt { get; set; }
    
    public int ItemCount { get; set; }
    
    public BatchStatus Status { get; set; } = BatchStatus.Pending;
    
    public virtual ICollection<Alarm> Alarms { get; set; } = new List<Alarm>();
}

public enum BatchStatus
{
    Pending,
    Processing,
    Completed,
    PartialFailure,
    Failed
}
