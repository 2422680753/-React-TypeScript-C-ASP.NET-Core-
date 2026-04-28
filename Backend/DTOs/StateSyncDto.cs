namespace IoTMonitoringPlatform.DTOs;

public class HeartbeatDto
{
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "Online";
    public Dictionary<string, double>? Metrics { get; set; }
    public int? LatencyMs { get; set; }
}

public class HeartbeatResponseDto
{
    public Guid DeviceId { get; set; }
    public DateTime ServerTime { get; set; }
    public int NextHeartbeatIntervalMs { get; set; }
    public List<ControlCommandDto>? Commands { get; set; }
}

public class DeviceStateDto
{
    public Guid DeviceId { get; set; }
    public string CurrentStatus { get; set; } = "Offline";
    public string? PreviousStatus { get; set; }
    public DateTime StatusChangedAt { get; set; }
    public int Version { get; set; }
    public Dictionary<string, double>? Metrics { get; set; }
    public bool IsConsistent { get; set; }
}

public class StateSyncRequestDto
{
    public int? LastKnownVersion { get; set; }
    public List<Guid>? DeviceIds { get; set; }
    public DateTime Timestamp { get; set; }
    public bool RequestFullSnapshot { get; set; }
}

public class StateSyncResponseDto
{
    public DateTime Timestamp { get; set; }
    public bool IsFullSnapshot { get; set; }
    public List<DeviceStateDto> States { get; set; } = new();
    public string Checksum { get; set; } = string.Empty;
    public string SyncId { get; set; } = string.Empty;
}

public class StateConsistencyCheckDto
{
    public Guid DeviceId { get; set; }
    public string CacheStatus { get; set; } = string.Empty;
    public string DatabaseStatus { get; set; } = string.Empty;
    public bool IsConsistent { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public bool AutoFixApplied { get; set; }
}

public class DeviceOnlineEventDto
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DateTime OnlineTime { get; set; }
    public string? PreviousStatus { get; set; }
    public string ConnectionType { get; set; } = "WebSocket";
    public Dictionary<string, double>? Metrics { get; set; }
}

public class DeviceOfflineEventDto
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DateTime OfflineTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? LastHeartbeatTime { get; set; }
}

public class ConnectionStatusDto
{
    public bool IsConnected { get; set; }
    public string? ConnectionId { get; set; }
    public int ReconnectAttempts { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastDisconnectedAt { get; set; }
}

public class AlarmDeduplicationDto
{
    public Guid DeviceId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public DateTime FirstTriggeredAt { get; set; }
    public DateTime LastTriggeredAt { get; set; }
    public double LastValue { get; set; }
}

public class AlarmSuppressionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Custom";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsActive { get; set; }
    public int SuppressedCount { get; set; }
}

public class AlarmAggregationDto
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
    public string HighestLevel { get; set; } = "Information";
    public int AlarmCount { get; set; }
    public string AggregatedTitle { get; set; } = string.Empty;
    public DateTime FirstOccurredAt { get; set; }
    public DateTime LastOccurredAt { get; set; }
    public bool IsResolved { get; set; }
    public List<AlarmDto> Alarms { get; set; } = new();
}

public class AlarmGovernanceStatsDto
{
    public int ActiveSuppressions { get; set; }
    public int ActiveAggregations { get; set; }
    public int QueueSize { get; set; }
    public int ProcessedCount { get; set; }
    public int DroppedCount { get; set; }
    public int DeduplicatedCount { get; set; }
    public int SuppressedCount { get; set; }
}

public class RealTimeDataDto
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public Dictionary<string, double> Metrics { get; set; } = new();
    public Dictionary<string, string?> Units { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "Online";
}

public class ControlCommandDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int? RetryCount { get; set; }
    public int? MaxRetries { get; set; }
    public int TimeoutSeconds { get; set; }
    public string Priority { get; set; } = "Normal";
}

public class CommandResultDto
{
    public Guid CommandId { get; set; }
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum ConnectionType
{
    WebSocket,
    MQTT,
    HTTP
}

public enum SuppressionType
{
    Maintenance,
    KnownIssue,
    PlannedDowntime,
    Custom
}
