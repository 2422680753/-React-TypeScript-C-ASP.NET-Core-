using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace IoTMonitoringPlatform.Hubs;

public class MonitoringHub : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionToUser = new();

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

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToDevice(Guid deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Device_{deviceId}");
    }

    public async Task UnsubscribeFromDevice(Guid deviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Device_{deviceId}");
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
    Task ReceiveCommandResult(CommandResultDto result);
    Task ReceiveStatisticsUpdate(DeviceStatisticsDto statistics);
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

    public static async Task BroadcastCommandResult(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, CommandResultDto result, Guid deviceId)
    {
        await hubContext.Clients.Group($"Device_{deviceId}").ReceiveCommandResult(result);
    }

    public static async Task BroadcastStatisticsUpdate(this IHubContext<MonitoringHub, IMonitoringHubClient> hubContext, DeviceStatisticsDto statistics)
    {
        await hubContext.Clients.Group("AllDevices").ReceiveStatisticsUpdate(statistics);
    }
}
