using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;

namespace IoTMonitoringPlatform.Services.Interfaces;

public interface IAlarmGovernanceService
{
    Task<bool> ShouldDeduplicateAsync(Guid deviceId, string metric, double value, AlarmLevel level);
    
    Task<AlarmDeduplication> RecordDeduplicationAsync(Guid deviceId, string metric, double value, AlarmLevel level);
    
    Task<bool> IsSuppressedAsync(Guid deviceId, string metric, AlarmLevel level);
    
    Task<AlarmSuppression> CreateSuppressionAsync(AlarmSuppression suppression);
    
    Task<List<AlarmSuppression>> GetActiveSuppressionsAsync();
    
    Task<AlarmAggregation> CreateAggregationAsync(Alarm alarm, string correlationId);
    
    Task<AlarmAggregation?> GetAggregationAsync(string correlationId);
    
    Task<bool> CheckRateLimitAsync(Guid deviceId, string metric);
    
    Task<AlarmRateLimit> GetOrCreateRateLimitAsync(Guid deviceId, string metric);
    
    Task<bool> EnqueueAlarmAsync(AlarmQueueItem item);
    
    Task<AlarmQueueItem?> DequeueAlarmAsync();
    
    Task ProcessQueuedAlarmsAsync(int batchSize = 10);
    
    Task<AlarmBatch> CreateBatchAsync(IEnumerable<Alarm> alarms);
    
    Task ProcessBatchAsync(AlarmBatch batch);
    
    Task CleanupExpiredItemsAsync();
    
    Task<AlarmGovernanceStats> GetStatsAsync();
}

public class AlarmGovernanceStats
{
    public int ActiveSuppressions { get; set; }
    public int ActiveAggregations { get; set; }
    public int QueueSize { get; set; }
    public int ProcessedCount { get; set; }
    public int DroppedCount { get; set; }
    public int DeduplicatedCount { get; set; }
    public int SuppressedCount { get; set; }
}
