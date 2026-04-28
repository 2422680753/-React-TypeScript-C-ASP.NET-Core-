using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Hubs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace IoTMonitoringPlatform.Services;

public class HeartbeatService : IHeartbeatService
{
    private readonly AppDbContext _context;
    private readonly IDeviceStateService _deviceStateService;
    private readonly IHubContext<MonitoringHub, IMonitoringHubClient> _hubContext;
    private readonly ConcurrentDictionary<Guid, DateTime> _lastHeartbeatTimes;
    private readonly ConcurrentDictionary<string, ConnectionInfo> _activeConnections;
    private readonly int _heartbeatIntervalMs = 30000;
    private readonly int _heartbeatTimeoutMs = 90000;

    public HeartbeatService(
        AppDbContext context,
        IDeviceStateService deviceStateService,
        IHubContext<MonitoringHub, IMonitoringHubClient> hubContext)
    {
        _context = context;
        _deviceStateService = deviceStateService;
        _hubContext = hubContext;
        _lastHeartbeatTimes = new ConcurrentDictionary<Guid, DateTime>();
        _activeConnections = new ConcurrentDictionary<string, ConnectionInfo>();
    }

    public async Task<HeartbeatResponseDto> ProcessHeartbeatAsync(HeartbeatDto heartbeat)
    {
        var device = await _context.Devices.FindAsync(heartbeat.DeviceId);
        if (device == null)
        {
            return new HeartbeatResponseDto
            {
                Success = false,
                ServerTime = DateTime.UtcNow,
                IntervalMs = _heartbeatIntervalMs,
                TimeoutMs = _heartbeatTimeoutMs,
                Message = "Device not found"
            };
        }

        var now = DateTime.UtcNow;
        _lastHeartbeatTimes[heartbeat.DeviceId] = now;

        var heartbeatRecord = new DeviceHeartbeat
        {
            Id = Guid.NewGuid(),
            DeviceId = heartbeat.DeviceId,
            Timestamp = now,
            IpAddress = heartbeat.IpAddress,
            LatencyMs = heartbeat.LatencyMs,
            Status = HeartbeatStatus.Normal
        };

        _context.DeviceHeartbeats.Add(heartbeatRecord);

        if (device.Status != DeviceStatus.Online && device.Status != DeviceStatus.Warning && device.Status != DeviceStatus.Error)
        {
            await _deviceStateService.HandleDeviceOnlineAsync(new DeviceOnlineEventDto
            {
                DeviceId = heartbeat.DeviceId,
                DeviceName = device.Name,
                OnlineTime = now,
                IpAddress = heartbeat.IpAddress,
                Metadata = heartbeat.Metadata
            });
        }
        else
        {
            device.LastOnlineTime = now;
            device.UpdatedAt = now;
        }

        if (heartbeat.Metrics != null && heartbeat.Metrics.Any())
        {
            foreach (var metric in heartbeat.Metrics)
            {
                var dataPoint = new DeviceData
                {
                    Id = Guid.NewGuid(),
                    DeviceId = heartbeat.DeviceId,
                    Metric = metric.Key,
                    Value = metric.Value,
                    Timestamp = now,
                    Quality = DataQuality.Good
                };
                _context.DeviceData.Add(dataPoint);
            }
        }

        await _context.SaveChangesAsync();

        return new HeartbeatResponseDto
        {
            Success = true,
            ServerTime = now,
            IntervalMs = _heartbeatIntervalMs,
            TimeoutMs = _heartbeatTimeoutMs,
            Message = "Heartbeat processed successfully"
        };
    }

    public async Task<HeartbeatResponseDto> ProcessBatchHeartbeatsAsync(BatchHeartbeatDto batch)
    {
        var now = DateTime.UtcNow;
        var responses = new List<HeartbeatResponseDto>();

        foreach (var heartbeat in batch.Heartbeats)
        {
            var response = await ProcessHeartbeatAsync(heartbeat);
            responses.Add(response);
        }

        return new HeartbeatResponseDto
        {
            Success = responses.All(r => r.Success),
            ServerTime = now,
            IntervalMs = _heartbeatIntervalMs,
            TimeoutMs = _heartbeatTimeoutMs,
            Message = $"Processed {responses.Count} heartbeats"
        };
    }

    public async Task CheckDeviceTimeoutsAsync()
    {
        var now = DateTime.UtcNow;
        var timeoutThreshold = now.AddMilliseconds(-_heartbeatTimeoutMs);

        var devices = await _context.Devices
            .Where(d => d.Status == DeviceStatus.Online || d.Status == DeviceStatus.Warning || d.Status == DeviceStatus.Error)
            .ToListAsync();

        foreach (var device in devices)
        {
            var lastHeartbeat = _lastHeartbeatTimes.TryGetValue(device.Id, out var lastTime)
                ? lastTime
                : device.LastOnlineTime ?? DateTime.MinValue;

            if (lastHeartbeat < timeoutThreshold)
            {
                var missedCount = await GetMissedHeartbeatsAsync(device.Id);

                await _deviceStateService.HandleDeviceOfflineAsync(new DeviceOfflineEventDto
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    OfflineTime = now,
                    Reason = OfflineReason.Timeout,
                    MissedHeartbeats = missedCount
                });
            }
        }
    }

    public async Task<ConnectionStatusDto> GetConnectionStatusAsync(Guid deviceId)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            throw new KeyNotFoundException($"Device {deviceId} not found");

        var missedCount = await GetMissedHeartbeatsAsync(deviceId);
        var isConnected = device.Status == DeviceStatus.Online ||
                          device.Status == DeviceStatus.Warning ||
                          device.Status == DeviceStatus.Error;

        var connection = _activeConnections.Values
            .FirstOrDefault(c => c.DeviceId == deviceId);

        return new ConnectionStatusDto
        {
            DeviceId = deviceId,
            IsConnected = isConnected,
            ConnectionId = connection?.ConnectionId,
            ConnectedAt = connection?.ConnectedAt,
            LastActivityAt = _lastHeartbeatTimes.TryGetValue(deviceId, out var lastTime) ? lastTime : device.LastOnlineTime,
            ConnectionType = connection?.ConnectionType ?? ConnectionType.Other,
            MissedHeartbeats = missedCount
        };
    }

    public async Task<List<ConnectionStatusDto>> GetAllConnectionStatusesAsync()
    {
        var devices = await _context.Devices.ToListAsync();
        var statuses = new List<ConnectionStatusDto>();

        foreach (var device in devices)
        {
            try
            {
                var status = await GetConnectionStatusAsync(device.Id);
                statuses.Add(status);
            }
            catch
            {
            }
        }

        return statuses;
    }

    public async Task<int> GetMissedHeartbeatsAsync(Guid deviceId)
    {
        var now = DateTime.UtcNow;
        var lastHeartbeat = _lastHeartbeatTimes.TryGetValue(deviceId, out var lastTime)
            ? lastTime
            : DateTime.MinValue;

        if (lastHeartbeat == DateTime.MinValue)
            return 0;

        var elapsed = now - lastHeartbeat;
        var missed = (int)Math.Floor(elapsed.TotalMilliseconds / _heartbeatIntervalMs);

        return Math.Max(0, missed);
    }

    public async Task RegisterDeviceConnectionAsync(Guid deviceId, string connectionId, ConnectionType type, string? ipAddress = null)
    {
        var connection = new ConnectionInfo
        {
            DeviceId = deviceId,
            ConnectionId = connectionId,
            ConnectionType = type,
            ConnectedAt = DateTime.UtcNow,
            IpAddress = ipAddress
        };

        _activeConnections[connectionId] = connection;

        var dbConnection = new DeviceConnection
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ConnectionId = connectionId,
            ConnectionType = type,
            IpAddress = ipAddress,
            IsActive = true
        };

        _context.DeviceConnections.Add(dbConnection);
        await _context.SaveChangesAsync();
    }

    public async Task UnregisterDeviceConnectionAsync(string connectionId)
    {
        if (_activeConnections.TryRemove(connectionId, out var connection))
        {
            var dbConnection = await _context.DeviceConnections
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

            if (dbConnection != null)
            {
                dbConnection.IsActive = false;
                dbConnection.DisconnectedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task<List<Guid>> GetOfflineDevicesAsync(int timeoutMinutes = 5)
    {
        var timeoutThreshold = DateTime.UtcNow.AddMinutes(-timeoutMinutes);

        var offlineDevices = await _context.Devices
            .Where(d => d.Status == DeviceStatus.Offline ||
                        (d.LastOnlineTime.HasValue && d.LastOnlineTime < timeoutThreshold))
            .Select(d => d.Id)
            .ToListAsync();

        return offlineDevices;
    }
}

public class ConnectionInfo
{
    public Guid DeviceId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public ConnectionType ConnectionType { get; set; }
    public DateTime ConnectedAt { get; set; }
    public string? IpAddress { get; set; }
}
