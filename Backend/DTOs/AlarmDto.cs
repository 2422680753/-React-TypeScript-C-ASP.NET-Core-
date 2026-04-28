namespace IoTMonitoringPlatform.DTOs;

public class AlarmDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public Guid? RuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Level { get; set; } = "Information";
    public string Status { get; set; } = "Active";
    public DateTime TriggeredAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public double? TriggeredValue { get; set; }
    public string? TriggeredMetric { get; set; }
}

public class AlarmRuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public string Metric { get; set; } = string.Empty;
    public string Operator { get; set; } = "GreaterThan";
    public double Threshold { get; set; }
    public double? WarningThreshold { get; set; }
    public double? CriticalThreshold { get; set; }
    public int DurationSeconds { get; set; }
    public int ConsecutiveOccurrences { get; set; }
    public string AlarmLevel { get; set; } = "Warning";
    public bool IsEnabled { get; set; }
    public bool IsNotificationEnabled { get; set; }
    public string? NotificationChannels { get; set; }
    public int CooldownMinutes { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateAlarmRuleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? GroupId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public string Operator { get; set; } = "GreaterThan";
    public double Threshold { get; set; }
    public double? WarningThreshold { get; set; }
    public double? CriticalThreshold { get; set; }
    public int DurationSeconds { get; set; } = 0;
    public int ConsecutiveOccurrences { get; set; } = 1;
    public string AlarmLevel { get; set; } = "Warning";
    public bool IsEnabled { get; set; } = true;
    public bool IsNotificationEnabled { get; set; } = true;
    public string? NotificationChannels { get; set; }
    public int CooldownMinutes { get; set; } = 5;
}

public class UpdateAlarmRuleDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Metric { get; set; }
    public string? Operator { get; set; }
    public double? Threshold { get; set; }
    public double? WarningThreshold { get; set; }
    public double? CriticalThreshold { get; set; }
    public int? DurationSeconds { get; set; }
    public int? ConsecutiveOccurrences { get; set; }
    public string? AlarmLevel { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsNotificationEnabled { get; set; }
    public string? NotificationChannels { get; set; }
    public int? CooldownMinutes { get; set; }
}

public class AcknowledgeAlarmDto
{
    public string? Notes { get; set; }
}

public class ResolveAlarmDto
{
    public string ResolutionNotes { get; set; } = string.Empty;
}

public class AlarmStatisticsDto
{
    public int TotalAlarms { get; set; }
    public int ActiveAlarms { get; set; }
    public int AcknowledgedAlarms { get; set; }
    public int ResolvedAlarms { get; set; }
    public int CriticalAlarms { get; set; }
    public int WarningAlarms { get; set; }
    public int InformationAlarms { get; set; }
}
