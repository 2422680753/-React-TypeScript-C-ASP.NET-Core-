using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;

namespace IoTMonitoringPlatform.Services.Interfaces;

public interface IDeviceService
{
    Task<DeviceDto> GetByIdAsync(Guid id);
    Task<List<DeviceDto>> GetAllAsync(string? status = null, string? deviceType = null, Guid? groupId = null);
    Task<DeviceDto> CreateAsync(CreateDeviceDto dto);
    Task<DeviceDto> UpdateAsync(Guid id, UpdateDeviceDto dto);
    Task DeleteAsync(Guid id);
    Task UpdateStatusAsync(Guid deviceId, DeviceStatus status);
    Task<DeviceStatisticsDto> GetStatisticsAsync();
    Task<List<DeviceDto>> GetByGroupAsync(Guid groupId);
    Task<DeviceDto?> GetBySerialNumberAsync(string serialNumber);
    Task<bool> ExistsAsync(Guid id);
    Task UpdateLastOnlineTimeAsync(Guid deviceId);
}
