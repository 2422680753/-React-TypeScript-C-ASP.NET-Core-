using IoTMonitoringPlatform.DTOs;

namespace IoTMonitoringPlatform.Services.Interfaces;

public interface IDataService
{
    Task AddDataAsync(CreateDeviceDataDto dto);
    Task AddBatchDataAsync(BatchDeviceDataDto dto);
    Task<List<DeviceDataDto>> GetHistoricalDataAsync(HistoricalDataRequestDto request);
    Task<List<AggregatedDataDto>> GetAggregatedDataAsync(HistoricalDataRequestDto request);
    Task<RealTimeDataDto> GetLatestDataAsync(Guid deviceId);
    Task<List<RealTimeDataDto>> GetLatestDataForDevicesAsync(List<Guid> deviceIds);
    Task DeleteOldDataAsync(int retentionDays);
}
