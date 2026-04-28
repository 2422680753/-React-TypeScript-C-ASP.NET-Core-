using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Hubs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace IoTMonitoringPlatform.Services;

public class DeviceStateService : IDeviceStateService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<MonitoringHub, IMonitoringHubClient> _hubContext;
    private readonly ConcurrentDictionary<Guid, DeviceStateCache> _stateCache;
    private readonly ConcurrentDictionary<Guid, object> _stateLocks;

    public DeviceStateService(
        AppDbContext context,
        IHubContext<MonitoringHub, IMonitoringHubClient> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
        _stateCache = new ConcurrentDictionary<Guid, DeviceStateCache>();
        _stateLocks = new ConcurrentDictionary<Guid, object>();
    }

    public async Task UpdateDeviceStatusAsync(Guid deviceId, DeviceStatus newStatus, string? reason = null, string? triggeredBy = null)
    {
        var lockObj = _stateLocks.GetOrAdd(deviceId, _ => new object());
        
        lock (lockObj)
        {
            if (!_stateCache.TryGetValue(deviceId, out var cache))
            {
                cache = new DeviceStateCache();
                _stateCache[deviceId] = cache;
            }

            if (cache.CurrentStatus == newStatus)
                return;
        }

        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            throw new KeyNotFoundException($"Device {deviceId} not found");

        var oldStatus = device.Status;

        if (oldStatus == newStatus)
            return;

        var history = new DeviceStateHistory
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FromStatus = oldStatus,
            ToStatus = newStatus,
            Timestamp = DateTime.UtcNow,
            Reason = reason,
            TriggeredBy = triggeredBy
        };

        _context.DeviceStateHistories.Add(history);

        device.Status = newStatus;
        device.UpdatedAt = DateTime.UtcNow;

        if (newStatus == DeviceStatus.Online)
        {
            device.LastOnlineTime = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        lock (lockObj)
        {
            if (_stateCache.TryGetValue(deviceId, out var cache))
            {
                cache.CurrentStatus = newStatus;
                cache.LastUpdateTime = DateTime.UtcNow;
                cache.Version++;
            }
        }

        var statusDto = new DeviceStatusDto
        {
            DeviceId = deviceId,
            Status = newStatus.ToString(),
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.BroadcastDeviceStatusChange(statusDto);
    }

    public async Task<DeviceStateDto> GetDeviceStateAsync(Guid deviceId)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            throw new KeyNotFoundException($"Device {deviceId} not found");

        var latestData = await _context.DeviceData
            .Where(d => d.DeviceId == deviceId)
            .OrderByDescending(d => d.Timestamp)
            .GroupBy(d => d.Metric)
            .Select(g => g.First())
            .ToListAsync();

        var metrics = latestData.ToDictionary(d => d.Metric, d => d.Value);

        return new DeviceStateDto
        {
            DeviceId = deviceId,
            DeviceName = device.Name,
            Status = device.Status,
            LastOnlineTime = device.LastOnlineTime ?? DateTime.MinValue,
            UpdatedAt = device.UpdatedAt,
            Metrics = metrics.Any() ? metrics : null
        };
    }

    public async Task<List<DeviceStateDto>> GetAllDeviceStatesAsync(string? statusFilter = null)
    {
        var query = _context.Devices.AsQueryable();

        if (!string.IsNullOrEmpty(statusFilter))
        {
            if (Enum.TryParse<DeviceStatus>(statusFilter, out var status))
            {
                query = query.Where(d => d.Status == status);
            }
        }

        var devices = await query.ToListAsync();
        var states = new List<DeviceStateDto>();

        foreach (var device in devices)
        {
            var latestData = await _context.DeviceData
                .Where(d => d.DeviceId == device.Id)
                .OrderByDescending(d => d.Timestamp)
                .GroupBy(d => d.Metric)
                .Select(g => g.First())
                .ToListAsync();

            var metrics = latestData.ToDictionary(d => d.Metric, d => d.Value);

            states.Add(new DeviceStateDto
            {
                DeviceId = device.Id,
                DeviceName = device.Name,
                Status = device.Status,
                LastOnlineTime = device.LastOnlineTime ?? DateTime.MinValue,
                UpdatedAt = device.UpdatedAt,
                Metrics = metrics.Any() ? metrics : null
            });
        }

        return states;
    }

    public async Task<StateSyncResponseDto> GetStateSyncSnapshotAsync(StateSyncRequestDto request)
    {
        var query = _context.Devices.AsQueryable();

        if (request.DeviceIds != null && request.DeviceIds.Any())
        {
            query = query.Where(d => request.DeviceIds.Contains(d.Id));
        }

        if (request.LastSyncTime.HasValue)
        {
            query = query.Where(d => d.UpdatedAt > request.LastSyncTime.Value);
        }

        var devices = await query.ToListAsync();
        var deviceIds = devices.Select(d => d.Id).ToList();

        var allMetrics = request.IncludeMetrics
            ? await _context.DeviceData
                .Where(d => deviceIds.Contains(d.DeviceId))
                .GroupBy(d => new { d.DeviceId, d.Metric })
                .Select(g => new
                {
                    g.Key.DeviceId,
                    g.Key.Metric,
                    g.First().Value
                })
                .ToListAsync()
            : new List<dynamic>();

        var metricsDict = allMetrics
            .GroupBy(m => m.DeviceId)
            .ToDictionary(
                g => (Guid)g.Key,
                g => g.ToDictionary(m => (string)m.Metric, m => (double)m.Value)
            );

        var deviceStates = devices.Select(device =>
        {
            metricsDict.TryGetValue(device.Id, out var metrics);

            return new DeviceStateDto
            {
                DeviceId = device.Id,
                DeviceName = device.Name,
                Status = device.Status,
                LastOnlineTime = device.LastOnlineTime ?? DateTime.MinValue,
                UpdatedAt = device.UpdatedAt,
                Metrics = metrics
            };
        }).ToList();

        var syncId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var checksumData = string.Join("|", deviceStates
            .OrderBy(d => d.DeviceId)
            .Select(d => $"{d.DeviceId}:{d.Status}:{d.UpdatedAt.Ticks}"));

        var checksum = ComputeChecksum(checksumData);

        var isFullSync = !request.LastSyncTime.HasValue && request.DeviceIds == null;

        return new StateSyncResponseDto
        {
            SyncId = syncId,
            SyncTime = now,
            DeviceCount = deviceStates.Count,
            Devices = deviceStates,
            Checksum = checksum,
            IsFullSync = isFullSync
        };
    }

    public async Task<StateConsistencyCheckDto> CheckStateConsistencyAsync(Guid deviceId)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            throw new KeyNotFoundException($"Device {deviceId} not found");

        var result = new StateConsistencyCheckDto
        {
            DeviceId = deviceId,
            CheckTime = DateTime.UtcNow,
            IsConsistent = true
        };

        if (_stateCache.TryGetValue(deviceId, out var cache))
        {
            var dbStatus = device.Status;
            var cacheStatus = cache.CurrentStatus;

            if (dbStatus != cacheStatus)
            {
                result.IsConsistent = false;
                result.ExpectedStatus = dbStatus.ToString();
                result.ActualStatus = cacheStatus.ToString();
                result.MismatchReason = "Cache status mismatch";

                cache.CurrentStatus = dbStatus;
                cache.LastUpdateTime = DateTime.UtcNow;
                result.AutoResolved = true;

                var statusDto = new DeviceStatusDto
                {
                    DeviceId = deviceId,
                    Status = dbStatus.ToString(),
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.BroadcastDeviceStatusChange(statusDto);
            }
        }

        return result;
    }

    public async Task<bool> SyncStateToFrontendAsync(Guid deviceId)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            return false;

        var latestData = await _context.DeviceData
            .Where(d => d.DeviceId == deviceId)
            .OrderByDescending(d => d.Timestamp)
            .GroupBy(d => d.Metric)
            .Select(g => g.First())
            .ToListAsync();

        var metrics = latestData.ToDictionary(d => d.Metric, d => d.Value);
        var units = latestData.ToDictionary(d => d.Metric, d => d.Unit);

        var realTimeData = new RealTimeDataDto
        {
            DeviceId = deviceId,
            DeviceName = device.Name,
            Metrics = metrics,
            Units = units,
            Timestamp = DateTime.UtcNow,
            Status = device.Status.ToString()
        };

        await _hubContext.BroadcastDeviceData(realTimeData);

        var statusDto = new DeviceStatusDto
        {
            DeviceId = deviceId,
            Status = device.Status.ToString(),
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.BroadcastDeviceStatusChange(statusDto);

        return true;
    }

    public async Task SyncAllStatesToFrontendAsync()
    {
        var devices = await _context.Devices.ToListAsync();

        foreach (var device in devices)
        {
            await SyncStateToFrontendAsync(device.Id);
        }
    }

    public async Task HandleDeviceOnlineAsync(DeviceOnlineEventDto eventDto)
    {
        var device = await _context.Devices.FindAsync(eventDto.DeviceId);
        if (device == null)
            return;

        var oldStatus = device.Status;

        if (oldStatus != DeviceStatus.Online)
        {
            var history = new DeviceStateHistory
            {
                Id = Guid.NewGuid(),
                DeviceId = eventDto.DeviceId,
                FromStatus = oldStatus,
                ToStatus = DeviceStatus.Online,
                Timestamp = DateTime.UtcNow,
                Reason = "Device online",
                TriggeredBy = "Heartbeat"
            };

            _context.DeviceStateHistories.Add(history);

            device.Status = DeviceStatus.Online;
            device.LastOnlineTime = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var statusDto = new DeviceStatusDto
            {
                DeviceId = eventDto.DeviceId,
                Status = DeviceStatus.Online.ToString(),
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.BroadcastDeviceStatusChange(statusDto);
        }
    }

    public async Task HandleDeviceOfflineAsync(DeviceOfflineEventDto eventDto)
    {
        var device = await _context.Devices.FindAsync(eventDto.DeviceId);
        if (device == null)
            return;

        var oldStatus = device.Status;

        if (oldStatus != DeviceStatus.Offline)
        {
            var reason = eventDto.Reason switch
            {
                OfflineReason.Normal => "Device went offline normally",
                OfflineReason.NetworkDisconnect => "Network disconnection",
                OfflineReason.Timeout => "Heartbeat timeout",
                OfflineReason.Manual => "Manual shutdown",
                OfflineReason.Error => "Error occurred",
                OfflineReason.Maintenance => "Maintenance mode",
                _ => "Device went offline"
            };

            var history = new DeviceStateHistory
            {
                Id = Guid.NewGuid(),
                DeviceId = eventDto.DeviceId,
                FromStatus = oldStatus,
                ToStatus = DeviceStatus.Offline,
                Timestamp = DateTime.UtcNow,
                Reason = reason,
                TriggeredBy = "System"
            };

            _context.DeviceStateHistories.Add(history);

            device.Status = DeviceStatus.Offline;
            device.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var statusDto = new DeviceStatusDto
            {
                DeviceId = eventDto.DeviceId,
                Status = DeviceStatus.Offline.ToString(),
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.BroadcastDeviceStatusChange(statusDto);
        }
    }

    public async Task<List<DeviceStateHistory>> GetStateHistoryAsync(Guid deviceId, int limit = 100)
    {
        return await _context.DeviceStateHistories
            .Where(h => h.DeviceId == deviceId)
            .OrderByDescending(h => h.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    private static string ComputeChecksum(string data)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(bytes);
    }
}

public class DeviceStateCache
{
    public DeviceStatus CurrentStatus { get; set; } = DeviceStatus.Offline;
    public DateTime LastUpdateTime { get; set; } = DateTime.MinValue;
    public long Version { get; set; } = 0;
    public Dictionary<string, double> Metrics { get; set; } = new();
}
