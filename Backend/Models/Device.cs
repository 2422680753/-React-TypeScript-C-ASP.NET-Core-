using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IoTMonitoringPlatform.Models;

public class Device
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string DeviceType { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Description { get; set; }
    
    [MaxLength(50)]
    public string? Location { get; set; }
    
    public double? Latitude { get; set; }
    
    public double? Longitude { get; set; }
    
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    
    public DateTime? LastOnlineTime { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
    
    [MaxLength(100)]
    public string? Manufacturer { get; set; }
    
    [MaxLength(50)]
    public string? FirmwareVersion { get; set; }
    
    [MaxLength(50)]
    public string? HardwareVersion { get; set; }
    
    public Guid? GroupId { get; set; }
    
    [ForeignKey("GroupId")]
    public virtual DeviceGroup? Group { get; set; }
    
    public virtual ICollection<DeviceData> DataPoints { get; set; } = new List<DeviceData>();
    
    public virtual ICollection<Alarm> Alarms { get; set; } = new List<Alarm>();
    
    public virtual ICollection<ControlCommand> Commands { get; set; } = new List<ControlCommand>();
}

public enum DeviceStatus
{
    Offline,
    Online,
    Warning,
    Error,
    Maintenance
}
