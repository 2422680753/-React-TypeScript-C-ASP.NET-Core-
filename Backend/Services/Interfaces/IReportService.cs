using IoTMonitoringPlatform.DTOs;

namespace IoTMonitoringPlatform.Services.Interfaces;

public interface IReportService
{
    Task<DeviceStatisticsReportDto> GetDeviceStatisticsReportAsync();
    Task<List<DeviceAvailabilityReportDto>> GetDeviceAvailabilityReportAsync(ReportRequestDto request);
    Task<AlarmReportDto> GetAlarmReportAsync(ReportRequestDto request);
    Task<List<DataQualityReportDto>> GetDataQualityReportAsync(ReportRequestDto request);
    Task<CommandExecutionReportDto> GetCommandExecutionReportAsync(ReportRequestDto request);
    Task<byte[]> ExportReportToCsvAsync(string reportType, ReportRequestDto request);
    Task<byte[]> ExportReportToPdfAsync(string reportType, ReportRequestDto request);
}
