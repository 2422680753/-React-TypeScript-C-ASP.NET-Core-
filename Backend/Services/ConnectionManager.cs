using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace IoTMonitoringPlatform.Services;

public class ConnectionManager : IConnectionManager
{
    private readonly AppDbContext _context;
    private readonly ConcurrentDictionary<string, ConnectionRecord> _connections;
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _deviceToConnections;

    public ConnectionManager(AppDbContext context)
    {
        _context = context;
        _connections = new ConcurrentDictionary<string, ConnectionRecord>();
        _deviceToConnections = new ConcurrentDictionary<Guid, HashSet<string>>();
    }

    public async Task AddConnectionAsync(Guid deviceId, string connectionId, ConnectionType type)
    {
        var record = new ConnectionRecord
        {
            DeviceId = deviceId,
            ConnectionId = connectionId,
            ConnectionType = type,
            ConnectedAt = DateTime.UtcNow
        };

        _connections[connectionId] = record;

        _deviceToConnections.AddOrUpdate(
            deviceId,
            _ => new HashSet<string> { connectionId },
            (_, connections) =>
            {
                connections.Add(connectionId);
                return connections;
            });

        var dbConnection = new DeviceConnection
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ConnectionId = connectionId,
            ConnectionType = type,
            IsActive = true
        };

        _context.DeviceConnections.Add(dbConnection);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var record))
        {
            if (_deviceToConnections.TryGetValue(record.DeviceId, out var connections))
            {
                connections.Remove(connectionId);

                if (connections.Count == 0)
                {
                    _deviceToConnections.TryRemove(record.DeviceId, out _);
                }
            }

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

    public Task<Guid?> GetDeviceIdByConnectionIdAsync(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var record))
        {
            return Task.FromResult<Guid?>(record.DeviceId);
        }

        return Task.FromResult<Guid?>(null);
    }

    public Task<List<string>> GetConnectionIdsByDeviceIdAsync(Guid deviceId)
    {
        if (_deviceToConnections.TryGetValue(deviceId, out var connections))
        {
            return Task.FromResult(connections.ToList());
        }

        return Task.FromResult(new List<string>());
    }

    public Task<bool> IsDeviceConnectedAsync(Guid deviceId)
    {
        var result = _deviceToConnections.TryGetValue(deviceId, out var connections)
                     && connections.Count > 0;

        return Task.FromResult(result);
    }

    public Task<int> GetConnectedDeviceCountAsync()
    {
        var count = _deviceToConnections.Count;
        return Task.FromResult(count);
    }

    public Task<List<Guid>> GetConnectedDeviceIdsAsync()
    {
        var deviceIds = _deviceToConnections.Keys.ToList();
        return Task.FromResult(deviceIds);
    }

    public Task<Dictionary<Guid, List<string>>> GetAllConnectionsAsync()
    {
        var result = _deviceToConnections.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList()
        );

        return Task.FromResult(result);
    }
}

public class ConnectionRecord
{
    public Guid DeviceId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public ConnectionType ConnectionType { get; set; }
    public DateTime ConnectedAt { get; set; }
    public string? IpAddress { get; set; }
}
