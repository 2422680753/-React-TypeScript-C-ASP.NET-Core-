namespace IoTMonitoringPlatform.DTOs;

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

public class CreateControlCommandDto
{
    public Guid DeviceId { get; set; }
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
    public int? MaxRetries { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string Priority { get; set; } = "Normal";
}

public class BatchControlCommandDto
{
    public List<Guid> DeviceIds { get; set; } = new();
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
    public int? MaxRetries { get; set; }
    public int? TimeoutSeconds { get; set; }
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
