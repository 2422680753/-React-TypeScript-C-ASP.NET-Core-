using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;

namespace IoTMonitoringPlatform.Services.Interfaces;

public interface IHeartbeatService
{
    Task<HeartbeatResponseDto> ProcessHeartbeatAsync(HeartbeatDto heartbeat);
    Task<HeartbeatResponseDto> ProcessBatchHeartbeatsAsync(BatchHeartbeatDto batch);
    Task CheckDeviceTimeoutsAsync();
    Task<ConnectionStatusDto> GetConnectionStatusAsync(Guid deviceId);
    Task<List<ConnectionStatusDto>> GetAllConnectionStatusesAsync();
    Task<int> GetMissedHeartbeatsAsync(Guid deviceId);
    Task RegisterDeviceConnectionAsync(Guid deviceId, string connectionId, ConnectionType type, string? ipAddress = null);
    Task UnregisterDeviceConnectionAsync(string connectionId);
    Task<List<Guid>> GetOfflineDevicesAsync(int timeoutMinutes = 5);
}

public interface IDeviceStateService
{
    Task UpdateDeviceStatusAsync(Guid deviceId, DeviceStatus newStatus, string? reason = null, string? triggeredBy = null);
    Task<DeviceStateDto> GetDeviceStateAsync(Guid deviceId);
    Task<List<DeviceStateDto>> GetAllDeviceStatesAsync(string? statusFilter = null);
    Task<StateSyncResponseDto> GetStateSyncSnapshotAsync(StateSyncRequestDto request);
    Task<StateConsistencyCheckDto> CheckStateConsistencyAsync(Guid deviceId);
    Task<bool> SyncStateToFrontendAsync(Guid deviceId);
    Task SyncAllStatesToFrontendAsync();
    Task HandleDeviceOnlineAsync(DeviceOnlineEventDto eventDto);
    Task HandleDeviceOfflineAsync(DeviceOfflineEventDto eventDto);
    Task<List<DeviceStateHistory>> GetStateHistoryAsync(Guid deviceId, int limit = 100);
}

public interface IConnectionManager
{
    Task AddConnectionAsync(Guid deviceId, string connectionId, ConnectionType type);
    Task RemoveConnectionAsync(string connectionId);
    Task<Guid?> GetDeviceIdByConnectionIdAsync(string connectionId);
    Task<List<string>> GetConnectionIdsByDeviceIdAsync(Guid deviceId);
    Task<bool> IsDeviceConnectedAsync(Guid deviceId);
    Task<int> GetConnectedDeviceCountAsync();
    Task<List<Guid>> GetConnectedDeviceIdsAsync();
    Task<Dictionary<Guid, List<string>>> GetAllConnectionsAsync();
}
