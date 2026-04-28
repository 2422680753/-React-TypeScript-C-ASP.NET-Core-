namespace IoTMonitoringPlatform.DTOs;

public class DeviceDataDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }
    public DateTime Timestamp { get; set; }
    public string Quality { get; set; } = "Good";
}

public class CreateDeviceDataDto
{
    public Guid DeviceId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }
    public DateTime? Timestamp { get; set; }
    public string? Metadata { get; set; }
}

public class BatchDeviceDataDto
{
    public Guid DeviceId { get; set; }
    public List<MetricDataDto> Metrics { get; set; } = new();
    public DateTime? Timestamp { get; set; }
}

public class MetricDataDto
{
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }
}

public class HistoricalDataRequestDto
{
    public Guid DeviceId { get; set; }
    public string? Metric { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int? IntervalSeconds { get; set; }
    public int? Limit { get; set; }
}

public class AggregatedDataDto
{
    public string Metric { get; set; } = string.Empty;
    public double Min { get; set; }
    public double Max { get; set; }
    public double Avg { get; set; }
    public double Sum { get; set; }
    public int Count { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class RealTimeDataDto
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public Dictionary<string, double> Metrics { get; set; } = new();
    public Dictionary<string, string?> Units { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
}
