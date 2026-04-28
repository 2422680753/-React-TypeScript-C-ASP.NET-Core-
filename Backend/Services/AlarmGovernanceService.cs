using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Hubs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace IoTMonitoringPlatform.Services;

public class AlarmGovernanceService : IAlarmGovernanceService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<MonitoringHub, IMonitoringHubClient> _hubContext;
    private readonly ConcurrentDictionary<string, DeduplicationCache> _deduplicationCache;
    private readonly ConcurrentQueue<AlarmQueueItem> _memoryQueue;
    private readonly object _queueLock = new();
    private readonly int _deduplicationWindowSeconds = 60;
    private readonly int _maxQueueSize = 10000;
    private readonly int _maxAlarmsPerMinute = 100;
    private readonly int _maxAlarmsPerHour = 500;

    public AlarmGovernanceService(
        AppDbContext context,
        IHubContext<MonitoringHub, IMonitoringHubClient> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
        _deduplicationCache = new ConcurrentDictionary<string, DeduplicationCache>();
        _memoryQueue = new ConcurrentQueue<AlarmQueueItem>();
    }

    public async Task<bool> ShouldDeduplicateAsync(Guid deviceId, string metric, double value, AlarmLevel level)
    {
        var key = GenerateDeduplicationKey(deviceId, metric, level);

        if (_deduplicationCache.TryGetValue(key, out var cache))
        {
            var windowEnd = cache.FirstTriggeredAt.AddSeconds(_deduplicationWindowSeconds);

            if (DateTime.UtcNow < windowEnd)
            {
                var valueTolerance = Math.Abs(value * 0.05);
                if (Math.Abs(value - cache.LastValue) <= valueTolerance)
                {
                    cache.OccurrenceCount++;
                    cache.LastTriggeredAt = DateTime.UtcNow;
                    cache.LastValue = value;

                    if (cache.OccurrenceCount % 10 == 0)
                    {
                        await UpdateDeduplicationRecordAsync(cache);
                    }

                    return true;
                }
            }
            else
            {
                _deduplicationCache.TryRemove(key, out _);
            }
        }

        return false;
    }

    public async Task<AlarmDeduplication> RecordDeduplicationAsync(Guid deviceId, string metric, double value, AlarmLevel level)
    {
        var key = GenerateDeduplicationKey(deviceId, metric, level);

        var cache = new DeduplicationCache
        {
            DeviceId = deviceId,
            Metric = metric,
            Level = level,
            FirstTriggeredAt = DateTime.UtcNow,
            LastTriggeredAt = DateTime.UtcNow,
            LastValue = value,
            OccurrenceCount = 1
        };

        _deduplicationCache[key] = cache;

        var record = new AlarmDeduplication
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Metric = metric,
            Operator = ComparisonOperator.GreaterThan,
            Threshold = 0,
            TriggeredValue = value,
            OccurrenceCount = 1,
            FirstTriggeredAt = DateTime.UtcNow,
            LastTriggeredAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.AlarmDeduplications.Add(record);
        await _context.SaveChangesAsync();

        return record;
    }

    public async Task<bool> IsSuppressedAsync(Guid deviceId, string metric, AlarmLevel level)
    {
        var now = DateTime.UtcNow;

        var activeSuppressions = await _context.AlarmSuppressions
            .Where(s => s.IsActive &&
                       s.StartTime <= now &&
                       s.EndTime >= now &&
                       (s.DeviceId == null || s.DeviceId == deviceId) &&
                       (s.Metric == null || s.Metric == metric) &&
                       (s.MinLevel == null || s.MinLevel <= level))
            .ToListAsync();

        if (activeSuppressions.Any())
        {
            foreach (var suppression in activeSuppressions)
            {
                suppression.SuppressedCount++;
            }
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<AlarmSuppression> CreateSuppressionAsync(AlarmSuppression suppression)
    {
        suppression.Id = Guid.NewGuid();
        suppression.CreatedAt = DateTime.UtcNow;
        suppression.SuppressedCount = 0;
        suppression.IsActive = true;

        _context.AlarmSuppressions.Add(suppression);
        await _context.SaveChangesAsync();

        return suppression;
    }

    public async Task<List<AlarmSuppression>> GetActiveSuppressionsAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.AlarmSuppressions
            .Where(s => s.IsActive &&
                       s.StartTime <= now &&
                       s.EndTime >= now)
            .OrderBy(s => s.EndTime)
            .ToListAsync();
    }

    public async Task<AlarmAggregation> CreateAggregationAsync(Alarm alarm, string correlationId)
    {
        var aggregation = await _context.AlarmAggregations
            .FirstOrDefaultAsync(a => a.CorrelationId == correlationId);

        if (aggregation != null)
        {
            aggregation.AlarmCount++;
            aggregation.LastOccurredAt = DateTime.UtcNow;

            if (alarm.Level > aggregation.HighestLevel)
            {
                aggregation.HighestLevel = alarm.Level;
            }

            alarm.AggregationId = aggregation.Id;

            await _context.SaveChangesAsync();

            return aggregation;
        }

        aggregation = new AlarmAggregation
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            DeviceId = alarm.DeviceId,
            GroupKey = $"{alarm.DeviceId}_{alarm.TriggeredMetric}",
            HighestLevel = alarm.Level,
            AlarmCount = 1,
            AggregatedTitle = alarm.Title,
            AggregatedDescription = alarm.Description,
            FirstOccurredAt = DateTime.UtcNow,
            LastOccurredAt = DateTime.UtcNow,
            IsResolved = false
        };

        _context.AlarmAggregations.Add(aggregation);
        alarm.AggregationId = aggregation.Id;

        await _context.SaveChangesAsync();

        return aggregation;
    }

    public async Task<AlarmAggregation?> GetAggregationAsync(string correlationId)
    {
        return await _context.AlarmAggregations
            .Include(a => a.Alarms)
            .FirstOrDefaultAsync(a => a.CorrelationId == correlationId);
    }

    public async Task<bool> CheckRateLimitAsync(Guid deviceId, string metric)
    {
        var rateLimit = await GetOrCreateRateLimitAsync(deviceId, metric);

        var now = DateTime.UtcNow;

        if (now >= rateLimit.MinuteResetAt)
        {
            rateLimit.CurrentMinuteCount = 0;
            rateLimit.MinuteResetAt = now.AddMinutes(1);
        }

        if (now >= rateLimit.HourResetAt)
        {
            rateLimit.CurrentHourCount = 0;
            rateLimit.HourResetAt = now.AddHours(1);
        }

        if (rateLimit.CurrentMinuteCount >= rateLimit.MaxAlarmsPerMinute ||
            rateLimit.CurrentHourCount >= rateLimit.MaxAlarmsPerHour)
        {
            rateLimit.DroppedCount++;
            await _context.SaveChangesAsync();
            return false;
        }

        rateLimit.CurrentMinuteCount++;
        rateLimit.CurrentHourCount++;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<AlarmRateLimit> GetOrCreateRateLimitAsync(Guid deviceId, string metric)
    {
        var key = $"{deviceId}_{metric}";

        var rateLimit = await _context.AlarmRateLimits
            .FirstOrDefaultAsync(r => r.DeviceId == deviceId && r.Metric == metric);

        if (rateLimit != null)
            return rateLimit;

        rateLimit = new AlarmRateLimit
        {
            Id = Guid.NewGuid(),
            RuleKey = key,
            DeviceId = deviceId,
            Metric = metric,
            MaxAlarmsPerMinute = _maxAlarmsPerMinute,
            MaxAlarmsPerHour = _maxAlarmsPerHour,
            CurrentMinuteCount = 0,
            CurrentHourCount = 0,
            MinuteResetAt = DateTime.UtcNow.AddMinutes(1),
            HourResetAt = DateTime.UtcNow.AddHours(1),
            DroppedCount = 0,
            IsEnabled = true
        };

        _context.AlarmRateLimits.Add(rateLimit);
        await _context.SaveChangesAsync();

        return rateLimit;
    }

    public async Task<bool> EnqueueAlarmAsync(AlarmQueueItem item)
    {
        if (_memoryQueue.Count >= _maxQueueSize)
        {
            var oldest = await _context.AlarmQueueItems
                .Where(q => q.Status == QueueItemStatus.Pending)
                .OrderBy(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            if (oldest != null)
            {
                oldest.Status = QueueItemStatus.Dropped;
                await _context.SaveChangesAsync();
            }

            return false;
        }

        item.Id = Guid.NewGuid();
        item.CreatedAt = DateTime.UtcNow;
        item.Status = QueueItemStatus.Pending;
        item.Priority = (int)item.Level;

        _memoryQueue.Enqueue(item);

        _context.AlarmQueueItems.Add(item);
        await _context.SaveChangesAsync();

        return true;
    }

    public Task<AlarmQueueItem?> DequeueAlarmAsync()
    {
        if (_memoryQueue.TryDequeue(out var item))
        {
            return Task.FromResult<AlarmQueueItem?>(item);
        }

        return Task.FromResult<AlarmQueueItem?>(null);
    }

    public async Task ProcessQueuedAlarmsAsync(int batchSize = 10)
    {
        var pendingItems = await _context.AlarmQueueItems
            .Where(q => q.Status == QueueItemStatus.Pending)
            .OrderByDescending(q => q.Priority)
            .ThenBy(q => q.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

        if (!pendingItems.Any())
            return;

        var devices = await _context.Devices
            .Where(d => pendingItems.Select(i => i.DeviceId).Contains(d.Id))
            .ToDictionaryAsync(d => d.Id);

        var rules = await _context.AlarmRules
            .Where(r => pendingItems.Select(i => i.RuleId).Where(id => id.HasValue).Select(id => id!.Value).Contains(r.Id))
            .ToDictionaryAsync(r => r.Id);

        var alarms = new List<Alarm>();
        var alarmDtos = new List<AlarmDto>();

        foreach (var item in pendingItems)
        {
            try
            {
                item.Status = QueueItemStatus.Processing;

                if (!devices.TryGetValue(item.DeviceId, out var device))
                {
                    item.Status = QueueItemStatus.Failed;
                    item.ErrorMessage = "Device not found";
                    continue;
                }

                var alarm = new Alarm
                {
                    Id = Guid.NewGuid(),
                    DeviceId = item.DeviceId,
                    RuleId = item.RuleId,
                    Title = item.Title,
                    Description = item.Description,
                    Level = item.Level,
                    Status = AlarmStatus.Active,
                    TriggeredAt = DateTime.UtcNow,
                    TriggeredValue = item.TriggeredValue,
                    TriggeredMetric = item.TriggeredMetric
                };

                alarms.Add(alarm);

                var alarmDto = new AlarmDto
                {
                    Id = alarm.Id,
                    DeviceId = alarm.DeviceId,
                    DeviceName = device.Name,
                    RuleId = alarm.RuleId,
                    Title = alarm.Title,
                    Description = alarm.Description,
                    Level = alarm.Level.ToString(),
                    Status = alarm.Status.ToString(),
                    TriggeredAt = alarm.TriggeredAt,
                    TriggeredValue = alarm.TriggeredValue,
                    TriggeredMetric = alarm.TriggeredMetric
                };

                alarmDtos.Add(alarmDto);

                if (item.Level == AlarmLevel.Critical || item.Level == AlarmLevel.Emergency)
                {
                    device.Status = DeviceStatus.Error;
                }
                else if (item.Level == AlarmLevel.Warning)
                {
                    device.Status = DeviceStatus.Warning;
                }

                item.Status = QueueItemStatus.Processed;
                item.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                item.Status = QueueItemStatus.Failed;
                item.ErrorMessage = ex.Message;
                item.RetryCount++;
            }
        }

        if (alarms.Any())
        {
            _context.Alarms.AddRange(alarms);
        }

        await _context.SaveChangesAsync();

        foreach (var alarmDto in alarmDtos)
        {
            await _hubContext.BroadcastAlarm(alarmDto);
        }
    }

    public async Task<AlarmBatch> CreateBatchAsync(IEnumerable<Alarm> alarms)
    {
        var batch = new AlarmBatch
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ItemCount = alarms.Count(),
            Status = BatchStatus.Pending,
            Alarms = alarms.ToList()
        };

        _context.AlarmBatches.Add(batch);
        await _context.SaveChangesAsync();

        return batch;
    }

    public async Task ProcessBatchAsync(AlarmBatch batch)
    {
        batch.Status = BatchStatus.Processing;
        await _context.SaveChangesAsync();

        try
        {
            var alarmDtos = new List<AlarmDto>();

            foreach (var alarm in batch.Alarms)
            {
                var device = await _context.Devices.FindAsync(alarm.DeviceId);

                if (device != null)
                {
                    var alarmDto = new AlarmDto
                    {
                        Id = alarm.Id,
                        DeviceId = alarm.DeviceId,
                        DeviceName = device.Name,
                        Title = alarm.Title,
                        Description = alarm.Description,
                        Level = alarm.Level.ToString(),
                        Status = alarm.Status.ToString(),
                        TriggeredAt = alarm.TriggeredAt
                    };

                    alarmDtos.Add(alarmDto);
                }
            }

            foreach (var alarmDto in alarmDtos)
            {
                await _hubContext.BroadcastAlarm(alarmDto);
            }

            batch.Status = BatchStatus.Completed;
            batch.ProcessedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            batch.Status = BatchStatus.Failed;
            throw;
        }

        await _context.SaveChangesAsync();
    }

    public async Task CleanupExpiredItemsAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var expiredDeduplications = await _context.AlarmDeduplications
            .Where(d => d.ExpiresAt.HasValue && d.ExpiresAt < cutoff)
            .ToListAsync();

        _context.AlarmDeduplications.RemoveRange(expiredDeduplications);

        var processedQueueItems = await _context.AlarmQueueItems
            .Where(q => (q.Status == QueueItemStatus.Processed ||
                         q.Status == QueueItemStatus.Dropped ||
                         q.Status == QueueItemStatus.Failed) &&
                        q.ProcessedAt.HasValue &&
                        q.ProcessedAt < cutoff)
            .ToListAsync();

        _context.AlarmQueueItems.RemoveRange(processedQueueItems);

        var expiredSuppressions = await _context.AlarmSuppressions
            .Where(s => s.EndTime < cutoff)
            .ToListAsync();

        foreach (var suppression in expiredSuppressions)
        {
            suppression.IsActive = false;
        }

        var now = DateTime.UtcNow;
        var expiredKeys = _deduplicationCache
            .Where(kvp => kvp.Value.LastTriggeredAt < now.AddSeconds(-_deduplicationWindowSeconds * 2))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _deduplicationCache.TryRemove(key, out _);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<AlarmGovernanceStats> GetStatsAsync()
    {
        var activeSuppressions = await _context.AlarmSuppressions
            .CountAsync(s => s.IsActive);

        var activeAggregations = await _context.AlarmAggregations
            .CountAsync(a => !a.IsResolved);

        var queueSize = await _context.AlarmQueueItems
            .CountAsync(q => q.Status == QueueItemStatus.Pending);

        var processedCount = await _context.AlarmQueueItems
            .CountAsync(q => q.Status == QueueItemStatus.Processed);

        var droppedCount = await _context.AlarmQueueItems
            .CountAsync(q => q.Status == QueueItemStatus.Dropped);

        var totalDroppedRateLimit = await _context.AlarmRateLimits
            .SumAsync(r => r.DroppedCount);

        return new AlarmGovernanceStats
        {
            ActiveSuppressions = activeSuppressions,
            ActiveAggregations = activeAggregations,
            QueueSize = queueSize,
            ProcessedCount = processedCount,
            DroppedCount = droppedCount + totalDroppedRateLimit,
            DeduplicatedCount = 0,
            SuppressedCount = 0
        };
    }

    private static string GenerateDeduplicationKey(Guid deviceId, string metric, AlarmLevel level)
    {
        return $"{deviceId}_{metric}_{level}";
    }

    private async Task UpdateDeduplicationRecordAsync(DeduplicationCache cache)
    {
        var record = await _context.AlarmDeduplications
            .OrderByDescending(d => d.LastTriggeredAt)
            .FirstOrDefaultAsync(d => d.DeviceId == cache.DeviceId &&
                                      d.Metric == cache.Metric);

        if (record != null)
        {
            record.OccurrenceCount = cache.OccurrenceCount;
            record.LastTriggeredAt = cache.LastTriggeredAt;
            record.TriggeredValue = cache.LastValue;
            await _context.SaveChangesAsync();
        }
    }
}

public class DeduplicationCache
{
    public Guid DeviceId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public AlarmLevel Level { get; set; }
    public DateTime FirstTriggeredAt { get; set; }
    public DateTime LastTriggeredAt { get; set; }
    public double LastValue { get; set; }
    public int OccurrenceCount { get; set; }
}
