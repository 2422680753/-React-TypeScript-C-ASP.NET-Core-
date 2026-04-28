using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;

namespace IoTMonitoringPlatform.Services.Interfaces;

public interface IAlarmService
{
    Task<AlarmDto> GetByIdAsync(Guid id);
    Task<List<AlarmDto>> GetAllAsync(string? status = null, string? level = null, Guid? deviceId = null, int? limit = null);
    Task<AlarmRuleDto> CreateRuleAsync(CreateAlarmRuleDto dto);
    Task<AlarmRuleDto> UpdateRuleAsync(Guid id, UpdateAlarmRuleDto dto);
    Task DeleteRuleAsync(Guid id);
    Task<AlarmRuleDto> GetRuleByIdAsync(Guid id);
    Task<List<AlarmRuleDto>> GetAllRulesAsync(bool? isEnabled = null, Guid? deviceId = null);
    Task AcknowledgeAsync(Guid alarmId, string? userId, AcknowledgeAlarmDto dto);
    Task ResolveAsync(Guid alarmId, string? userId, ResolveAlarmDto dto);
    Task<AlarmStatisticsDto> GetStatisticsAsync();
    Task CheckAndTriggerAlarmsAsync(Guid deviceId, string metric, double value);
    Task<Alarm> CreateAlarmAsync(Device device, AlarmRule? rule, string title, string? description, AlarmLevel level, double? triggeredValue, string? triggeredMetric);
}
