namespace IoTMonitoringPlatform.DTOs;

public class ReportRequestDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<Guid>? DeviceIds { get; set; }
    public List<string>? Metrics { get; set; }
    public string? Granularity { get; set; }
    public string? ReportType { get; set; }
}

public class DeviceAvailabilityReportDto
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public double AvailabilityPercentage { get; set; }
    public TimeSpan OnlineDuration { get; set; }
    public TimeSpan OfflineDuration { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class AlarmReportDto
{
    public string AlarmLevel { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int ResolvedCount { get; set; }
    public TimeSpan AverageResolutionTime { get; set; }
    public List<TopAlarmDeviceDto> TopAlarmDevices { get; set; } = new();
    public List<AlarmTrendDto> Trend { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class TopAlarmDeviceDto
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public int AlarmCount { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
}

public class AlarmTrendDto
{
    public DateTime Date { get; set; }
    public int TotalAlarms { get; set; }
    public int CriticalAlarms { get; set; }
    public int WarningAlarms { get; set; }
    public int InformationAlarms { get; set; }
}

public class DeviceStatisticsReportDto
{
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
    public int OfflineDevices { get; set; }
    public int WarningDevices { get; set; }
    public int ErrorDevices { get; set; }
    public int MaintenanceDevices { get; set; }
    public double OnlinePercentage { get; set; }
    public double OfflinePercentage { get; set; }
    public double WarningPercentage { get; set; }
    public double ErrorPercentage { get; set; }
    public List<DeviceTypeStatisticsDto> ByDeviceType { get; set; } = new();
    public List<DeviceGroupStatisticsDto> ByGroup { get; set; } = new();
}

public class DeviceTypeStatisticsDto
{
    public string DeviceType { get; set; } = string.Empty;
    public int Count { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
}

public class DeviceGroupStatisticsDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int DeviceCount { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
}

public class DataQualityReportDto
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public int TotalDataPoints { get; set; }
    public int GoodDataPoints { get; set; }
    public int BadDataPoints { get; set; }
    public int UncertainDataPoints { get; set; }
    public double GoodPercentage { get; set; }
    public double BadPercentage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class CommandExecutionReportDto
{
    public int TotalCommands { get; set; }
    public int SuccessfulCommands { get; set; }
    public int FailedCommands { get; set; }
    public int PendingCommands { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public List<CommandTypeStatisticsDto> ByCommandType { get; set; } = new();
    public List<CommandTrendDto> Trend { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class CommandTypeStatisticsDto
{
    public string Command { get; set; } = string.Empty;
    public int Count { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
}

public class CommandTrendDto
{
    public DateTime Date { get; set; }
    public int TotalCommands { get; set; }
    public int SuccessfulCommands { get; set; }
    public int FailedCommands { get; set; }
}
