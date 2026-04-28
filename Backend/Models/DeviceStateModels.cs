using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IoTMonitoringPlatform.Models;

public class DeviceHeartbeat
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    
    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    [MaxLength(50)]
    public string? ConnectionId { get; set; }
    
    public int LatencyMs { get; set; }
    
    [MaxLength(1000)]
    public string? Metadata { get; set; }
    
    public HeartbeatStatus Status { get; set; } = HeartbeatStatus.Normal;
}

public enum HeartbeatStatus
{
    Normal,
    Late,
    Missing,
    Unstable
}

public class DeviceStateHistory
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    
    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }
    
    public DeviceStatus FromStatus { get; set; }
    
    public DeviceStatus ToStatus { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [MaxLength(500)]
    public string? Reason { get; set; }
    
    [MaxLength(50)]
    public string? TriggeredBy { get; set; }
    
    [MaxLength(1000)]
    public string? Metadata { get; set; }
}

public class DeviceConnection
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    
    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string ConnectionId { get; set; } = string.Empty;
    
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? DisconnectedAt { get; set; }
    
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    public ConnectionType ConnectionType { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    [MaxLength(1000)]
    public string? Metadata { get; set; }
}

public enum ConnectionType
{
    WebSocket,
    MQTT,
    HTTP,
    SignalR,
    Other
}

public class StateSyncPacket
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [MaxLength(50)]
    public string Version { get; set; } = "1.0";
    
    public int SequenceNumber { get; set; }
    
    [MaxLength(5000)]
    public string StateData { get; set; } = string.Empty;
    
    public string? Checksum { get; set; }
    
    public bool Acknowledged { get; set; }
    
    public DateTime? AcknowledgedAt { get; set; }
}
