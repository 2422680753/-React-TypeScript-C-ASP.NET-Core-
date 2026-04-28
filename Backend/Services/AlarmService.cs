using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IoTMonitoringPlatform.Services;

public class AlarmService : IAlarmService
{
    private readonly AppDbContext _context;

    public AlarmService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AlarmDto> GetByIdAsync(Guid id)
    {
        var alarm = await _context.Alarms
            .Include(a => a.Device)
            .Include(a => a.Rule)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alarm == null)
            throw new KeyNotFoundException($"Alarm with id {id} not found");

        return MapToAlarmDto(alarm);
    }

    public async Task<List<AlarmDto>> GetAllAsync(string? status = null, string? level = null, Guid? deviceId = null, int? limit = null)
    {
        var query = _context.Alarms
            .Include(a => a.Device)
            .Include(a => a.Rule)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AlarmStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        if (!string.IsNullOrEmpty(level) && Enum.TryParse<AlarmLevel>(level, true, out var parsedLevel))
        {
            query = query.Where(a => a.Level == parsedLevel);
        }

        if (deviceId.HasValue)
        {
            query = query.Where(a => a.DeviceId == deviceId);
        }

        query = query.OrderByDescending(a => a.TriggeredAt);

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        var alarms = await query.ToListAsync();
        return alarms.Select(MapToAlarmDto).ToList();
    }

    public async Task<AlarmRuleDto> CreateRuleAsync(CreateAlarmRuleDto dto)
    {
        var rule = new AlarmRule
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            DeviceId = dto.DeviceId,
            GroupId = dto.GroupId,
            Metric = dto.Metric,
            Operator = Enum.Parse<ComparisonOperator>(dto.Operator, true),
            Threshold = dto.Threshold,
            WarningThreshold = dto.WarningThreshold,
            CriticalThreshold = dto.CriticalThreshold,
            DurationSeconds = dto.DurationSeconds,
            ConsecutiveOccurrences = dto.ConsecutiveOccurrences,
            AlarmLevel = Enum.Parse<AlarmLevel>(dto.AlarmLevel, true),
            IsEnabled = dto.IsEnabled,
            IsNotificationEnabled = dto.IsNotificationEnabled,
            NotificationChannels = dto.NotificationChannels,
            CooldownMinutes = dto.CooldownMinutes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AlarmRules.Add(rule);
        await _context.SaveChangesAsync();

        return MapToAlarmRuleDto(rule);
    }

    public async Task<AlarmRuleDto> UpdateRuleAsync(Guid id, UpdateAlarmRuleDto dto)
    {
        var rule = await _context.AlarmRules
            .Include(r => r.Device)
            .Include(r => r.Group)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rule == null)
            throw new KeyNotFoundException($"Alarm rule with id {id} not found");

        if (!string.IsNullOrEmpty(dto.Name))
            rule.Name = dto.Name;

        if (dto.Description != null)
            rule.Description = dto.Description;

        if (!string.IsNullOrEmpty(dto.Metric))
            rule.Metric = dto.Metric;

        if (!string.IsNullOrEmpty(dto.Operator) && Enum.TryParse<ComparisonOperator>(dto.Operator, true, out var parsedOperator))
            rule.Operator = parsedOperator;

        if (dto.Threshold.HasValue)
            rule.Threshold = dto.Threshold.Value;

        if (dto.WarningThreshold.HasValue)
            rule.WarningThreshold = dto.WarningThreshold;

        if (dto.CriticalThreshold.HasValue)
            rule.CriticalThreshold = dto.CriticalThreshold;

        if (dto.DurationSeconds.HasValue)
            rule.DurationSeconds = dto.DurationSeconds.Value;

        if (dto.ConsecutiveOccurrences.HasValue)
            rule.ConsecutiveOccurrences = dto.ConsecutiveOccurrences.Value;

        if (!string.IsNullOrEmpty(dto.AlarmLevel) && Enum.TryParse<AlarmLevel>(dto.AlarmLevel, true, out var parsedLevel))
            rule.AlarmLevel = parsedLevel;

        if (dto.IsEnabled.HasValue)
            rule.IsEnabled = dto.IsEnabled.Value;

        if (dto.IsNotificationEnabled.HasValue)
            rule.IsNotificationEnabled = dto.IsNotificationEnabled.Value;

        if (dto.NotificationChannels != null)
            rule.NotificationChannels = dto.NotificationChannels;

        if (dto.CooldownMinutes.HasValue)
            rule.CooldownMinutes = dto.CooldownMinutes.Value;

        rule.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToAlarmRuleDto(rule);
    }

    public async Task DeleteRuleAsync(Guid id)
    {
        var rule = await _context.AlarmRules.FindAsync(id);

        if (rule == null)
            throw new KeyNotFoundException($"Alarm rule with id {id} not found");

        _context.AlarmRules.Remove(rule);
        await _context.SaveChangesAsync();
    }

    public async Task<AlarmRuleDto> GetRuleByIdAsync(Guid id)
    {
        var rule = await _context.AlarmRules
            .Include(r => r.Device)
            .Include(r => r.Group)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rule == null)
            throw new KeyNotFoundException($"Alarm rule with id {id} not found");

        return MapToAlarmRuleDto(rule);
    }

    public async Task<List<AlarmRuleDto>> GetAllRulesAsync(bool? isEnabled = null, Guid? deviceId = null)
    {
        var query = _context.AlarmRules
            .Include(r => r.Device)
            .Include(r => r.Group)
            .AsQueryable();

        if (isEnabled.HasValue)
        {
            query = query.Where(r => r.IsEnabled == isEnabled.Value);
        }

        if (deviceId.HasValue)
        {
            query = query.Where(r => r.DeviceId == deviceId || r.GroupId == null);
        }

        var rules = await query
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

        return rules.Select(MapToAlarmRuleDto).ToList();
    }

    public async Task AcknowledgeAsync(Guid alarmId, string? userId, AcknowledgeAlarmDto dto)
    {
        var alarm = await _context.Alarms.FindAsync(alarmId);

        if (alarm == null)
            throw new KeyNotFoundException($"Alarm with id {alarmId} not found");

        if (alarm.Status != AlarmStatus.Active)
            throw new InvalidOperationException("Only active alarms can be acknowledged");

        alarm.Status = AlarmStatus.Acknowledged;
        alarm.AcknowledgedAt = DateTime.UtcNow;
        alarm.AcknowledgedBy = userId;

        await _context.SaveChangesAsync();
    }

    public async Task ResolveAsync(Guid alarmId, string? userId, ResolveAlarmDto dto)
    {
        var alarm = await _context.Alarms.FindAsync(alarmId);

        if (alarm == null)
            throw new KeyNotFoundException($"Alarm with id {alarmId} not found");

        if (alarm.Status == AlarmStatus.Resolved || alarm.Status == AlarmStatus.Cleared)
            throw new InvalidOperationException("Alarm is already resolved or cleared");

        alarm.Status = AlarmStatus.Resolved;
        alarm.ResolvedAt = DateTime.UtcNow;
        alarm.ResolvedBy = userId;
        alarm.ResolutionNotes = dto.ResolutionNotes;

        await _context.SaveChangesAsync();
    }

    public async Task<AlarmStatisticsDto> GetStatisticsAsync()
    {
        var totalAlarms = await _context.Alarms.CountAsync();
        var activeAlarms = await _context.Alarms.CountAsync(a => a.Status == AlarmStatus.Active);
        var acknowledgedAlarms = await _context.Alarms.CountAsync(a => a.Status == AlarmStatus.Acknowledged);
        var resolvedAlarms = await _context.Alarms.CountAsync(a => a.Status == AlarmStatus.Resolved);
        var criticalAlarms = await _context.Alarms.CountAsync(a => a.Level == AlarmLevel.Critical);
        var warningAlarms = await _context.Alarms.CountAsync(a => a.Level == AlarmLevel.Warning);
        var informationAlarms = await _context.Alarms.CountAsync(a => a.Level == AlarmLevel.Information);

        return new AlarmStatisticsDto
        {
            TotalAlarms = totalAlarms,
            ActiveAlarms = activeAlarms,
            AcknowledgedAlarms = acknowledgedAlarms,
            ResolvedAlarms = resolvedAlarms,
            CriticalAlarms = criticalAlarms,
            WarningAlarms = warningAlarms,
            InformationAlarms = informationAlarms
        };
    }

    public async Task CheckAndTriggerAlarmsAsync(Guid deviceId, string metric, double value)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            return;

        var rules = await _context.AlarmRules
            .Where(r => r.IsEnabled &&
                       (r.DeviceId == deviceId || r.DeviceId == null) &&
                       r.Metric == metric)
            .ToListAsync();

        foreach (var rule in rules)
        {
            var shouldTrigger = EvaluateRule(rule, value);

            if (shouldTrigger)
            {
                if (rule.LastTriggeredAt.HasValue)
                {
                    var cooldownEnd = rule.LastTriggeredAt.Value.AddMinutes(rule.CooldownMinutes);
                    if (DateTime.UtcNow < cooldownEnd)
                    {
                        continue;
                    }
                }

                if (rule.ConsecutiveOccurrences > 1)
                {
                    var recentData = await _context.DeviceData
                        .Where(d => d.DeviceId == deviceId &&
                                   d.Metric == metric &&
                                   d.Timestamp >= DateTime.UtcNow.AddSeconds(-rule.DurationSeconds))
                        .OrderByDescending(d => d.Timestamp)
                        .Take(rule.ConsecutiveOccurrences)
                        .ToListAsync();

                    if (recentData.Count < rule.ConsecutiveOccurrences)
                        continue;

                    var allMatch = recentData.All(d => EvaluateRule(rule, d.Value));
                    if (!allMatch)
                        continue;
                }

                var alarmLevel = rule.AlarmLevel;

                if (rule.CriticalThreshold.HasValue && value >= rule.CriticalThreshold.Value)
                {
                    alarmLevel = AlarmLevel.Critical;
                }
                else if (rule.WarningThreshold.HasValue && value >= rule.WarningThreshold.Value)
                {
                    alarmLevel = AlarmLevel.Warning;
                }

                var title = $"{device.Name} - {metric} {rule.Operator} {rule.Threshold}";
                var description = $"Device {device.Name} (Serial: {device.SerialNumber}) reported {metric} = {value}, which {GetOperatorDescription(rule.Operator)} threshold of {rule.Threshold}.";

                await CreateAlarmAsync(device, rule, title, description, alarmLevel, value, metric);

                rule.LastTriggeredAt = DateTime.UtcNow;
                rule.UpdatedAt = DateTime.UtcNow;

                if (alarmLevel == AlarmLevel.Critical || alarmLevel == AlarmLevel.Emergency)
                {
                    device.Status = DeviceStatus.Error;
                }
                else if (alarmLevel == AlarmLevel.Warning)
                {
                    device.Status = DeviceStatus.Warning;
                }

                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task<Alarm> CreateAlarmAsync(Device device, AlarmRule? rule, string title, string? description, AlarmLevel level, double? triggeredValue, string? triggeredMetric)
    {
        var alarm = new Alarm
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            RuleId = rule?.Id,
            Title = title,
            Description = description,
            Level = level,
            Status = AlarmStatus.Active,
            TriggeredAt = DateTime.UtcNow,
            TriggeredValue = triggeredValue,
            TriggeredMetric = triggeredMetric
        };

        _context.Alarms.Add(alarm);
        await _context.SaveChangesAsync();

        return alarm;
    }

    private static bool EvaluateRule(AlarmRule rule, double value)
    {
        return rule.Operator switch
        {
            ComparisonOperator.GreaterThan => value > rule.Threshold,
            ComparisonOperator.LessThan => value < rule.Threshold,
            ComparisonOperator.GreaterThanOrEqual => value >= rule.Threshold,
            ComparisonOperator.LessThanOrEqual => value <= rule.Threshold,
            ComparisonOperator.Equal => value == rule.Threshold,
            ComparisonOperator.NotEqual => value != rule.Threshold,
            ComparisonOperator.Between => value >= rule.Threshold && value <= (rule.WarningThreshold ?? rule.Threshold + 1),
            ComparisonOperator.Outside => value < rule.Threshold || value > (rule.WarningThreshold ?? rule.Threshold + 1),
            _ => false
        };
    }

    private static string GetOperatorDescription(ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.GreaterThan => "exceeds",
            ComparisonOperator.LessThan => "is below",
            ComparisonOperator.GreaterThanOrEqual => "is at or above",
            ComparisonOperator.LessThanOrEqual => "is at or below",
            ComparisonOperator.Equal => "equals",
            ComparisonOperator.NotEqual => "does not equal",
            ComparisonOperator.Between => "is between",
            ComparisonOperator.Outside => "is outside",
            _ => "violates"
        };
    }

    private static AlarmDto MapToAlarmDto(Alarm alarm)
    {
        return new AlarmDto
        {
            Id = alarm.Id,
            DeviceId = alarm.DeviceId,
            DeviceName = alarm.Device?.Name ?? string.Empty,
            RuleId = alarm.RuleId,
            Title = alarm.Title,
            Description = alarm.Description,
            Level = alarm.Level.ToString(),
            Status = alarm.Status.ToString(),
            TriggeredAt = alarm.TriggeredAt,
            AcknowledgedAt = alarm.AcknowledgedAt,
            ResolvedAt = alarm.ResolvedAt,
            AcknowledgedBy = alarm.AcknowledgedBy,
            ResolvedBy = alarm.ResolvedBy,
            ResolutionNotes = alarm.ResolutionNotes,
            TriggeredValue = alarm.TriggeredValue,
            TriggeredMetric = alarm.TriggeredMetric
        };
    }

    private static AlarmRuleDto MapToAlarmRuleDto(AlarmRule rule)
    {
        return new AlarmRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            DeviceId = rule.DeviceId,
            DeviceName = rule.Device?.Name,
            GroupId = rule.GroupId,
            GroupName = rule.Group?.Name,
            Metric = rule.Metric,
            Operator = rule.Operator.ToString(),
            Threshold = rule.Threshold,
            WarningThreshold = rule.WarningThreshold,
            CriticalThreshold = rule.CriticalThreshold,
            DurationSeconds = rule.DurationSeconds,
            ConsecutiveOccurrences = rule.ConsecutiveOccurrences,
            AlarmLevel = rule.AlarmLevel.ToString(),
            IsEnabled = rule.IsEnabled,
            IsNotificationEnabled = rule.IsNotificationEnabled,
            NotificationChannels = rule.NotificationChannels,
            CooldownMinutes = rule.CooldownMinutes,
            LastTriggeredAt = rule.LastTriggeredAt,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
            CreatedBy = rule.CreatedBy
        };
    }
}
