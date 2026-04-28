using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text;

namespace IoTMonitoringPlatform.Hubs;

public class MonitoringHub : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionToUser = new();
    private readonly IDeviceStateService _deviceStateService;
    private readonly IHeartbeatService _heartbeatService;
    private readonly ILogger<MonitoringHub> _logger;

    public MonitoringHub(
        IDeviceStateService deviceStateService,
        IHeartbeatService heartbeatService,
        ILogger<MonitoringHub> logger)
    {
        _deviceStateService = deviceStateService;
        _heartbeatService = heartbeatService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? Context.ConnectionId;
        UserConnections.AddOrUpdate(userId,
            _ => new HashSet<string> { Context.ConnectionId },
            (_, connections) =>
            {
                connections.Add(Context.ConnectionId);
                return connections;
            });
        ConnectionToUser.TryAdd(Context.ConnectionId, userId);

        _logger.LogInformation("Client connected: {ConnectionId}, User: {UserId}", Context.ConnectionId, userId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionToUser.TryRemove(Context.ConnectionId, out var userId))
        {
            if (UserConnections.TryGetValue(userId, out var connections))
            {
                connections.Remove(Context.ConnectionId);
                if (connections.Count == 0)
                {
                    UserConnections.TryRemove(userId, out _);
                }
            }
        }

        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<StateSyncResponseDto> GetStateSync(StateSyncRequestDto request)
    {
        _logger.LogInformation("State sync requested: FullSnapshot={FullSnapshot}, LastVersion={LastVersion}",
            request.RequestFullSnapshot, request.LastKnownVersion);

        return await _deviceStateService.GetStateSyncSnapshotAsync(request);
    }

    public async Task RequestStateSync(StateSyncRequestDto request)
    {
        var response = await _deviceStateService.GetStateSyncSnapshotAsync(request);
        await Clients.Caller.ReceiveStateSync(response);
    }

    public async Task<HeartbeatResponseDto> SendHeartbeat(HeartbeatDto heartbeat)
    {
        try
        {
            var response = await _heartbeatService.ProcessHeartbeatAsync(heartbeat);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat from device {DeviceId}", heartbeat.DeviceId);
            throw;
        }
    }

    public async Task SubscribeToDevice(Guid deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Device_{deviceId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to device {DeviceId}", Context.ConnectionId, deviceId);
    }

    public async Task UnsubscribeFromDevice(Guid deviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Device_{deviceId}");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from device {DeviceId}", Context.ConnectionId, deviceId);
    }

    public async Task SubscribeToGroup(Guid groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Group_{groupId}");
    }

    public async Task UnsubscribeFromGroup(Guid groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Group_{groupId}");
    }

    public async Task SubscribeToAlarms()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Alarms");
    }

    public async Task UnsubscribeFromAlarms()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Alarms");
    }

    public async Task SubscribeToAllDevices()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "AllDevices");
    }

    public async Task UnsubscribeFromAllDevices()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AllDevices");
    }
}

public interface IMonitoringHubClient
{
    Task ReceiveDeviceData(RealTimeDataDto data);
    Task ReceiveDeviceStatusChange(DeviceStatusDto status);
    Task ReceiveAlarm(AlarmDto alarm);
    Task ReceiveAlarmUpdate(AlarmDto alarm);
    Task ReceiveAlarmAggregation(AlarmAggregationDto aggregation);
    Task ReceiveCommandResult(CommandResultDto result);
    Task ReceiveStatisticsUpdate(DeviceStatisticsDto statistics);
    Task ReceiveStateSync(StateSyncResponseDto sync);
    Task ReceiveDeviceOnline(DeviceOnlineEventDto onlineEvent);
    Task ReceiveDeviceOffline(DeviceOfflineEventDto offlineEvent);
    Task ReceiveHeartbeatAck(HeartbeatResponseDto response);
}

public static class MonitoringHubExtensions
{
    public static async Task BroadcastDeviceData(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, RealTimeDataDto data)
    {
        await hubContext.Clients.Group($"Device_{data.DeviceId}").ReceiveDeviceData(data);
        await hubContext.Clients.Group("AllDevices").ReceiveDeviceData(data);
    }

    public static async Task BroadcastDeviceStatusChange(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, DeviceStatusDto status)
    {
        await hubContext.Clients.Group($"Device_{status.DeviceId}").ReceiveDeviceStatusChange(status);
        await hubContext.Clients.Group("AllDevices").ReceiveDeviceStatusChange(status);
    }

    public static async Task BroadcastDeviceOnline(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, DeviceOnlineEventDto onlineEvent)
    {
        await hubContext.Clients.Group($"Device_{onlineEvent.DeviceId}").ReceiveDeviceOnline(onlineEvent);
        await hubContext.Clients.Group("AllDevices").ReceiveDeviceOnline(onlineEvent);
    }

    public static async Task BroadcastDeviceOffline(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, DeviceOfflineEventDto offlineEvent)
    {
        await hubContext.Clients.Group($"Device_{offlineEvent.DeviceId}").ReceiveDeviceOffline(offlineEvent);
        await hubContext.Clients.Group("AllDevices").ReceiveDeviceOffline(offlineEvent);
    }

    public static async Task BroadcastAlarm(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, AlarmDto alarm)
    {
        await hubContext.Clients.Group("Alarms").ReceiveAlarm(alarm);
        if (alarm.DeviceId != Guid.Empty)
        {
            await hubContext.Clients.Group($"Device_{alarm.DeviceId}").ReceiveAlarm(alarm);
        }
    }

    public static async Task BroadcastAlarmUpdate(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, AlarmDto alarm)
    {
        await hubContext.Clients.Group("Alarms").ReceiveAlarmUpdate(alarm);
        if (alarm.DeviceId != Guid.Empty)
        {
            await hubContext.Clients.Group($"Device_{alarm.DeviceId}").ReceiveAlarmUpdate(alarm);
        }
    }

    public static async Task BroadcastAlarmAggregation(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, AlarmAggregationDto aggregation)
    {
        await hubContext.Clients.Group("Alarms").ReceiveAlarmAggregation(aggregation);
    }

    public static async Task BroadcastCommandResult(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, CommandResultDto result, Guid deviceId)
    {
        await hubContext.Clients.Group($"Device_{deviceId}").ReceiveCommandResult(result);
    }

    public static async Task BroadcastStatisticsUpdate(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, DeviceStatisticsDto statistics)
    {
        await hubContext.Clients.Group("AllDevices").ReceiveStatisticsUpdate(statistics);
    }

    public static async Task BroadcastStateSync(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, StateSyncResponseDto sync, string? connectionId = null)
    {
        if (!string.IsNullOrEmpty(connectionId))
        {
            await hubContext.Clients.Client(connectionId).ReceiveStateSync(sync);
        }
        else
        {
            await hubContext.Clients.Group("AllDevices").ReceiveStateSync(sync);
        }
    }
}
