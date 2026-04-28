using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Hubs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace IoTMonitoringPlatform.Services;

public class EnhancedAlarmGovernanceService : IEnhancedAlarmGovernanceService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<MonitoringHub, IMonitoringHubClient> _hubContext;
    private readonly ILogger<EnhancedAlarmGovernanceService> _logger;
    
    private readonly ConcurrentDictionary<string, DeduplicationWindow> _deduplicationWindows;
    private readonly ConcurrentDictionary<string, SlidingWindowCounter> _slidingWindows;
    private readonly ConcurrentDictionary<string, RateLimitCounter> _rateLimitCounters;
    private readonly ConcurrentDictionary<string, ConsecutiveTriggerState> _consecutiveStates;
    
    private readonly object _queueLock = new();
    private bool _isProcessing = false;
    
    private readonly int _deduplicationWindowSeconds;
    private readonly int _slidingWindowSeconds;
    private readonly int _maxQueueSize;
    private readonly int _batchProcessingSize;
    private readonly int _maxAlarmsPerMinute;
    private readonly int _maxAlarmsPerHour;
    private readonly int _consecutiveTriggerThreshold;

    public EnhancedAlarmGovernanceService(
        AppDbContext context,
        IHubContext<MonitoringHub, IMonitoringHubClient> hubContext,
        ILogger<EnhancedAlarmGovernanceService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
        
        _deduplicationWindows = new ConcurrentDictionary<string, DeduplicationWindow>();
        _slidingWindows = new ConcurrentDictionary<string, SlidingWindowCounter>();
        _rateLimitCounters = new ConcurrentDictionary<string, RateLimitCounter>();
        _consecutiveStates = new ConcurrentDictionary<string, ConsecutiveTriggerState>();
        
        _deduplicationWindowSeconds = 120;
        _slidingWindowSeconds = 60;
        _maxQueueSize = 50000;
        _batchProcessingSize = 50;
        _maxAlarmsPerMinute = 200;
        _maxAlarmsPerHour = 1000;
        _consecutiveTriggerThreshold = 3;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Enhanced Alarm Governance Service...");

        var pendingItems = await _context.AlarmQueueItems
            .Where(q => q.Status == QueueItemStatus.Pending || q.Status == QueueItemStatus.Processing)
            .ToListAsync();

        _logger.LogInformation("Recovered {Count} pending/processing alarm queue items from database", pendingItems.Count);

        foreach (var item in pendingItems)
        {
            if (item.Status == QueueItemStatus.Processing)
            {
                item.Status = QueueItemStatus.Pending;
                item.RetryCount++;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Enhanced Alarm Governance Service initialized successfully");
    }

    public async Task<AlarmCheckResult> CheckAlarmAsync(
        Guid deviceId,
        string metric,
        double value,
        AlarmLevel level,
        string title,
        string description)
    {
        var result = new AlarmCheckResult
        {
            ShouldEnqueue = false,
            Reason = AlarmCheckReason.Passed
        };

        var key = GenerateKey(deviceId, metric, level);

        if (await CheckDeduplicationAsync(deviceId, metric, value, level))
        {
            result.ShouldEnqueue = false;
            result.Reason = AlarmCheckReason.Deduplicated;
            return result;
        }

        if (await CheckSuppressionAsync(deviceId, metric, level))
        {
            result.ShouldEnqueue = false;
            result.Reason = AlarmCheckReason.Suppressed;
            return result;
        }

        if (!await CheckRateLimitAsync(deviceId, metric))
        {
            result.ShouldEnqueue = false;
            result.Reason = AlarmCheckReason.RateLimited;
            return result;
        }

        if (await CheckConsecutiveThresholdAsync(deviceId, metric, value, true))
        {
            result.ShouldEnqueue = false;
            result.Reason = AlarmCheckReason.WaitingForConsecutive;
            return result;
        }

        if (!await CheckSlidingWindowAsync(deviceId, metric))
        {
            result.ShouldEnqueue = false;
            result.Reason = AlarmCheckReason.SlidingWindowThrottled;
            return result;
        }

        var queueItem = new AlarmQueueItem
        {
            DeviceId = deviceId,
            Title = title,
            Description = description,
            Level = level,
            TriggeredValue = value,
            TriggeredMetric = metric,
            Priority = CalculatePriority(level)
        };

        var enqueued = await EnqueueAlarmAsync(queueItem);

        if (!enqueued)
        {
            result.ShouldEnqueue = false;
            result.Reason = AlarmCheckReason.QueueFull;
            return result;
        }

        RecordDeduplication(deviceId, metric, value, level);
        result.ShouldEnqueue = true;
        result.QueueItemId = queueItem.Id;

        return result;
    }

    private void RecordDeduplication(Guid deviceId, string metric, double value, AlarmLevel level)
    {
        var key = GenerateKey(deviceId, metric, level);

        var window = new DeduplicationWindow
        {
            DeviceId = deviceId,
            Metric = metric,
            Level = level,
            FirstTriggeredAt = DateTime.UtcNow,
            LastTriggeredAt = DateTime.UtcNow,
            LastValue = value,
            OccurrenceCount = 1
        };

        _deduplicationWindows[key] = window;
    }

    private async Task<bool> CheckDeduplicationAsync(Guid deviceId, string metric, double value, AlarmLevel level)
    {
        var key = GenerateKey(deviceId, metric, level);

        if (_deduplicationWindows.TryGetValue(key, out var window))
        {
            var windowEnd = window.FirstTriggeredAt.AddSeconds(_deduplicationWindowSeconds);

            if (DateTime.UtcNow < windowEnd)
            {
                var valueTolerance = Math.Abs(value * 0.1);
                if (Math.Abs(value - window.LastValue) <= valueTolerance)
                {
                    window.OccurrenceCount++;
                    window.LastTriggeredAt = DateTime.UtcNow;
                    window.LastValue = value;

                    _logger.LogDebug(
                        "Alarm deduplicated: Device={DeviceId}, Metric={Metric}, Count={Count}",
                        deviceId, metric, window.OccurrenceCount);

                    return true;
                }
            }
            else
            {
                _deduplicationWindows.TryRemove(key, out _);
            }
        }

        return false;
    }

    private async Task<bool> CheckSuppressionAsync(Guid deviceId, string metric, AlarmLevel level)
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

            _logger.LogDebug(
                "Alarm suppressed: Device={DeviceId}, Metric={Metric}, Suppressions={Count}",
                deviceId, metric, activeSuppressions.Count);

            return true;
        }

        return false;
    }

    private async Task<bool> CheckRateLimitAsync(Guid deviceId, string metric)
    {
        var key = GenerateKey(deviceId, metric);
        var now = DateTime.UtcNow;

        if (!_rateLimitCounters.TryGetValue(key, out var counter))
        {
            counter = new RateLimitCounter
            {
                MinuteStart = now,
                HourStart = now,
                MinuteCount = 0,
                HourCount = 0
            };
            _rateLimitCounters[key] = counter;
        }

        if (now >= counter.MinuteStart.AddMinutes(1))
        {
            counter.MinuteCount = 0;
            counter.MinuteStart = now;
        }

        if (now >= counter.HourStart.AddHours(1))
        {
            counter.HourCount = 0;
            counter.HourStart = now;
        }

        if (counter.MinuteCount >= _maxAlarmsPerMinute ||
            counter.HourCount >= _maxAlarmsPerHour)
        {
            _logger.LogWarning(
                "Rate limit exceeded: Device={DeviceId}, Metric={Metric}, Minute={MinuteCount}/{MaxMinute}, Hour={HourCount}/{MaxHour}",
                deviceId, metric, counter.MinuteCount, _maxAlarmsPerMinute, counter.HourCount, _maxAlarmsPerHour);

            await UpdateDatabaseRateLimitAsync(deviceId, metric, true);
            return false;
        }

        counter.MinuteCount++;
        counter.HourCount++;

        await UpdateDatabaseRateLimitAsync(deviceId, metric, false);
        return true;
    }

    private async Task UpdateDatabaseRateLimitAsync(Guid deviceId, string metric, bool isDropped)
    {
        var key = $"{deviceId}_{metric}";
        var rateLimit = await _context.AlarmRateLimits
            .FirstOrDefaultAsync(r => r.RuleKey == key);

        if (rateLimit == null)
        {
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
        }

        if (isDropped)
        {
            rateLimit.DroppedCount++;
        }
        else
        {
            rateLimit.CurrentMinuteCount++;
            rateLimit.CurrentHourCount++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<bool> CheckConsecutiveThresholdAsync(Guid deviceId, string metric, double value, bool isTriggered)
    {
        var key = GenerateKey(deviceId, metric);

        if (!_consecutiveStates.TryGetValue(key, out var state))
        {
            state = new ConsecutiveTriggerState
            {
                DeviceId = deviceId,
                Metric = metric,
                ConsecutiveCount = 0,
                LastCheckTime = DateTime.UtcNow
            };
            _consecutiveStates[key] = state;
        }

        if (isTriggered)
        {
            state.ConsecutiveCount++;
            state.LastCheckTime = DateTime.UtcNow;

            _logger.LogDebug(
                "Consecutive trigger check: Device={DeviceId}, Metric={Metric}, Count={Count}/{Threshold}",
                deviceId, metric, state.ConsecutiveCount, _consecutiveTriggerThreshold);

            if (state.ConsecutiveCount >= _consecutiveTriggerThreshold)
            {
                state.ConsecutiveCount = 0;
                return false;
            }
        }
        else
        {
            state.ConsecutiveCount = 0;
            state.LastCheckTime = DateTime.UtcNow;
        }

        return true;
    }

    private async Task<bool> CheckSlidingWindowAsync(Guid deviceId, string metric)
    {
        var key = GenerateKey(deviceId, metric);
        var now = DateTime.UtcNow;

        if (!_slidingWindows.TryGetValue(key, out var window))
        {
            window = new SlidingWindowCounter
            {
                DeviceId = deviceId,
                Metric = metric,
                Timestamps = new List<DateTime>()
            };
            _slidingWindows[key] = window;
        }

        var cutoff = now.AddSeconds(-_slidingWindowSeconds);
        window.Timestamps.RemoveAll(t => t < cutoff);

        var maxPerWindow = 10;

        if (window.Timestamps.Count >= maxPerWindow)
        {
            _logger.LogDebug(
                "Sliding window throttled: Device={DeviceId}, Metric={Metric}, Count={Count}/{Max}",
                deviceId, metric, window.Timestamps.Count, maxPerWindow);
            return false;
        }

        window.Timestamps.Add(now);
        return true;
    }

    private async Task<bool> EnqueueAlarmAsync(AlarmQueueItem item)
    {
        var queueCount = await _context.AlarmQueueItems
            .CountAsync(q => q.Status == QueueItemStatus.Pending);

        if (queueCount >= _maxQueueSize)
        {
            var oldest = await _context.AlarmQueueItems
                .Where(q => q.Status == QueueItemStatus.Pending)
                .OrderBy(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            if (oldest != null)
            {
                oldest.Status = QueueItemStatus.Dropped;
                await _context.SaveChangesAsync();

                _logger.LogWarning(
                    "Queue full, dropped oldest item: Device={DeviceId}, Title={Title}",
                    oldest.DeviceId, oldest.Title);
            }

            return false;
        }

        item.Id = Guid.NewGuid();
        item.CreatedAt = DateTime.UtcNow;
        item.Status = QueueItemStatus.Pending;

        _context.AlarmQueueItems.Add(item);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Alarm enqueued: Device={DeviceId}, Metric={Metric}, Level={Level}, QueueCount={QueueCount}",
            item.DeviceId, item.TriggeredMetric, item.Level, queueCount + 1);

        _ = ProcessQueueAsync();

        return true;
    }

    public async Task ProcessQueueAsync()
    {
        if (_isProcessing)
            return;

        lock (_queueLock)
        {
            if (_isProcessing)
                return;
            _isProcessing = true;
        }

        try
        {
            while (true)
            {
                var pendingItems = await _context.AlarmQueueItems
                    .Where(q => q.Status == QueueItemStatus.Pending)
                    .OrderByDescending(q => q.Priority)
                    .ThenBy(q => q.CreatedAt)
                    .Take(_batchProcessingSize)
                    .ToListAsync();

                if (!pendingItems.Any())
                    break;

                _logger.LogInformation("Processing batch of {Count} alarms", pendingItems.Count);

                var deviceIds = pendingItems.Select(i => i.DeviceId).Distinct().ToList();
                var devices = await _context.Devices
                    .Where(d => deviceIds.Contains(d.Id))
                    .ToDictionaryAsync(d => d.Id);

                var alarmsToCreate = new List<Alarm>();
                var alarmDtosToBroadcast = new List<AlarmDto>();

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

                        alarmsToCreate.Add(alarm);

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

                        alarmDtosToBroadcast.Add(alarmDto);

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

                        _logger.LogError(ex, "Failed to process alarm queue item: {ItemId}", item.Id);
                    }
                }

                if (alarmsToCreate.Any())
                {
                    _context.Alarms.AddRange(alarmsToCreate);
                }

                await _context.SaveChangesAsync();

                foreach (var alarmDto in alarmDtosToBroadcast)
                {
                    try
                    {
                        await _hubContext.BroadcastAlarm(alarmDto);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to broadcast alarm: {AlarmId}", alarmDto.Id);
                    }
                }

                _logger.LogInformation(
                    "Batch processed: Created={Created}, Failed={Failed}",
                    alarmDtosToBroadcast.Count,
                    pendingItems.Count(i => i.Status == QueueItemStatus.Failed));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing alarm queue");
        }
        finally
        {
            lock (_queueLock)
            {
                _isProcessing = false;
            }
        }
    }

    public async Task<AlarmGovernanceStatsDto> GetStatsAsync()
    {
        var queueCount = await _context.AlarmQueueItems
            .CountAsync(q => q.Status == QueueItemStatus.Pending);

        var processedCount = await _context.AlarmQueueItems
            .CountAsync(q => q.Status == QueueItemStatus.Processed);

        var failedCount = await _context.AlarmQueueItems
            .CountAsync(q => q.Status == QueueItemStatus.Failed);

        var droppedCount = await _context.AlarmQueueItems
            .CountAsync(q => q.Status == QueueItemStatus.Dropped);

        var totalDroppedRateLimit = await _context.AlarmRateLimits
            .SumAsync(r => r.DroppedCount);

        var activeSuppressions = await _context.AlarmSuppressions
            .CountAsync(s => s.IsActive);

        var activeAggregations = await _context.AlarmAggregations
            .CountAsync(a => !a.IsResolved);

        return new AlarmGovernanceStatsDto
        {
            QueueSize = queueCount,
            ProcessedCount = processedCount,
            DroppedCount = droppedCount + totalDroppedRateLimit,
            ActiveSuppressions = activeSuppressions,
            ActiveAggregations = activeAggregations,
            DeduplicatedCount = _deduplicationWindows.Values.Sum(w => w.OccurrenceCount),
            SuppressedCount = 0
        };
    }

    public async Task CleanupExpiredItemsAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var processedQueueItems = await _context.AlarmQueueItems
            .Where(q => (q.Status == QueueItemStatus.Processed ||
                         q.Status == QueueItemStatus.Dropped) &&
                        q.ProcessedAt.HasValue &&
                        q.ProcessedAt < cutoff)
            .ToListAsync();

        if (processedQueueItems.Any())
        {
            _context.AlarmQueueItems.RemoveRange(processedQueueItems);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} expired queue items", processedQueueItems.Count);
        }

        var now = DateTime.UtcNow;
        var expiredDeduplicationKeys = _deduplicationWindows
            .Where(kvp => kvp.Value.LastTriggeredAt < now.AddSeconds(-_deduplicationWindowSeconds * 2))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredDeduplicationKeys)
        {
            _deduplicationWindows.TryRemove(key, out _);
        }

        var expiredSlidingWindowKeys = _slidingWindows
            .Where(kvp => !kvp.Value.Timestamps.Any())
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredSlidingWindowKeys)
        {
            _slidingWindows.TryRemove(key, out _);
        }

        var expiredConsecutiveKeys = _consecutiveStates
            .Where(kvp => kvp.Value.LastCheckTime < now.AddMinutes(-30))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredConsecutiveKeys)
        {
            _consecutiveStates.TryRemove(key, out _);
        }
    }

    private static string GenerateKey(Guid deviceId, string metric)
    {
        return $"{deviceId}_{metric}";
    }

    private static string GenerateKey(Guid deviceId, string metric, AlarmLevel level)
    {
        return $"{deviceId}_{metric}_{(int)level}";
    }

    private static int CalculatePriority(AlarmLevel level)
    {
        return level switch
        {
            AlarmLevel.Emergency => 400,
            AlarmLevel.Critical => 300,
            AlarmLevel.Warning => 200,
            AlarmLevel.Information => 100,
            _ => 100
        };
    }
}

public class DeduplicationWindow
{
    public Guid DeviceId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public AlarmLevel Level { get; set; }
    public DateTime FirstTriggeredAt { get; set; }
    public DateTime LastTriggeredAt { get; set; }
    public double LastValue { get; set; }
    public int OccurrenceCount { get; set; }
}

public class SlidingWindowCounter
{
    public Guid DeviceId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public List<DateTime> Timestamps { get; set; } = new();
}

public class RateLimitCounter
{
    public DateTime MinuteStart { get; set; }
    public DateTime HourStart { get; set; }
    public int MinuteCount { get; set; }
    public int HourCount { get; set; }
}

public class ConsecutiveTriggerState
{
    public Guid DeviceId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public int ConsecutiveCount { get; set; }
    public DateTime LastCheckTime { get; set; }
}

public enum AlarmCheckReason
{
    Passed,
    Deduplicated,
    Suppressed,
    RateLimited,
    WaitingForConsecutive,
    SlidingWindowThrottled,
    QueueFull
}

public class AlarmCheckResult
{
    public bool ShouldEnqueue { get; set; }
    public AlarmCheckReason Reason { get; set; }
    public Guid QueueItemId { get; set; }
}

public interface IEnhancedAlarmGovernanceService
{
    Task InitializeAsync();
    Task<AlarmCheckResult> CheckAlarmAsync(
        Guid deviceId,
        string metric,
        double value,
        AlarmLevel level,
        string title,
        string description);
    Task ProcessQueueAsync();
    Task<AlarmGovernanceStatsDto> GetStatsAsync();
    Task CleanupExpiredItemsAsync();
}
