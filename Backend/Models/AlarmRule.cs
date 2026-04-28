using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IoTMonitoringPlatform.Models;

public class AlarmRule
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public Guid? DeviceId { get; set; }
    
    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }
    
    public Guid? GroupId { get; set; }
    
    [ForeignKey("GroupId")]
    public virtual DeviceGroup? Group { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Metric { get; set; } = string.Empty;
    
    public ComparisonOperator Operator { get; set; } = ComparisonOperator.GreaterThan;
    
    public double Threshold { get; set; }
    
    public double? WarningThreshold { get; set; }
    
    public double? CriticalThreshold { get; set; }
    
    public int DurationSeconds { get; set; } = 0;
    
    public int ConsecutiveOccurrences { get; set; } = 1;
    
    public AlarmLevel AlarmLevel { get; set; } = AlarmLevel.Warning;
    
    public bool IsEnabled { get; set; } = true;
    
    public bool IsNotificationEnabled { get; set; } = true;
    
    [MaxLength(500)]
    public string? NotificationChannels { get; set; }
    
    public int CooldownMinutes { get; set; } = 5;
    
    public DateTime? LastTriggeredAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    
    public virtual ICollection<Alarm> Alarms { get; set; } = new List<Alarm>();
}

public enum ComparisonOperator
{
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Equal,
    NotEqual,
    Between,
    Outside
}
