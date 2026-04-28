using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IoTMonitoringPlatform.Models;

public class Alarm
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    
    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }
    
    public Guid? RuleId { get; set; }
    
    [ForeignKey("RuleId")]
    public virtual AlarmRule? Rule { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public AlarmLevel Level { get; set; } = AlarmLevel.Information;
    
    public AlarmStatus Status { get; set; } = AlarmStatus.Active;
    
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? AcknowledgedAt { get; set; }
    
    public DateTime? ResolvedAt { get; set; }
    
    [MaxLength(100)]
    public string? AcknowledgedBy { get; set; }
    
    [MaxLength(100)]
    public string? ResolvedBy { get; set; }
    
    [MaxLength(500)]
    public string? ResolutionNotes { get; set; }
    
    public double? TriggeredValue { get; set; }
    
    [MaxLength(50)]
    public string? TriggeredMetric { get; set; }
    
    [MaxLength(1000)]
    public string? Metadata { get; set; }
}

public enum AlarmLevel
{
    Information,
    Warning,
    Critical,
    Emergency
}

public enum AlarmStatus
{
    Active,
    Acknowledged,
    Resolved,
    Cleared,
    Suppressed
}
