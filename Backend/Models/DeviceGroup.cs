using System.ComponentModel.DataAnnotations;

namespace IoTMonitoringPlatform.Models;

public class DeviceGroup
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public Guid? ParentId { get; set; }
    
    [MaxLength(50)]
    public string? Location { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
    
    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
}
