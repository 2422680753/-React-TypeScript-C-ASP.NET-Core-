using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IoTMonitoringPlatform.Models;

public class DeviceData
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    
    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Metric { get; set; } = string.Empty;
    
    public double Value { get; set; }
    
    [MaxLength(20)]
    public string? Unit { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [MaxLength(500)]
    public string? Metadata { get; set; }
    
    public DataQuality Quality { get; set; } = DataQuality.Good;
}

public enum DataQuality
{
    Good,
    Bad,
    Uncertain,
    ConfigurationError,
    DeviceFailure
}
