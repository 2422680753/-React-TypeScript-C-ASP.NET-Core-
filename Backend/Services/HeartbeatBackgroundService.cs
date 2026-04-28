using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoTMonitoringPlatform.Services;

public class HeartbeatBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HeartbeatBackgroundService> _logger;
    private readonly int _checkIntervalSeconds = 30;
    private readonly int _alarmProcessingIntervalSeconds = 5;
    private readonly int _cleanupIntervalMinutes = 60;

    public HeartbeatBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<HeartbeatBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heartbeat Background Service starting...");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_checkIntervalSeconds));
        var lastAlarmProcessTime = DateTime.UtcNow;
        var lastCleanupTime = DateTime.UtcNow;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessHeartbeatCheckAsync();

                var now = DateTime.UtcNow;
                if ((now - lastAlarmProcessTime).TotalSeconds >= _alarmProcessingIntervalSeconds)
                {
                    await ProcessAlarmQueueAsync();
                    lastAlarmProcessTime = now;
                }

                if ((now - lastCleanupTime).TotalMinutes >= _cleanupIntervalMinutes)
                {
                    await CleanupExpiredItemsAsync();
                    lastCleanupTime = now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Heartbeat Background Service");
            }
        }

        _logger.LogInformation("Heartbeat Background Service stopping...");
    }

    private async Task ProcessHeartbeatCheckAsync()
    {
        using var scope = _serviceProvider.CreateScope();

        var heartbeatService = scope.ServiceProvider.GetRequiredService<IHeartbeatService>();

        await heartbeatService.CheckDeviceTimeoutsAsync();

        var stats = await heartbeatService.GetHeartbeatStatsAsync();

        _logger.LogInformation(
            "Heartbeat check completed. Connected devices: {Connected}, Timeouts detected: {Timeouts}",
            stats.ConnectedDevices,
            stats.TimeoutsDetected);
    }

    private async Task ProcessAlarmQueueAsync()
    {
        using var scope = _serviceProvider.CreateScope();

        var alarmGovernance = scope.ServiceProvider.GetRequiredService<IAlarmGovernanceService>();

        try
        {
            await alarmGovernance.ProcessQueuedAlarmsAsync(batchSize: 20);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing alarm queue");
        }
    }

    private async Task CleanupExpiredItemsAsync()
    {
        using var scope = _serviceProvider.CreateScope();

        var alarmGovernance = scope.ServiceProvider.GetRequiredService<IAlarmGovernanceService>();

        try
        {
            await alarmGovernance.CleanupExpiredItemsAsync();

            var stats = await alarmGovernance.GetStatsAsync();

            _logger.LogInformation(
                "Cleanup completed. Active suppressions: {Suppressions}, Queue size: {QueueSize}, Dropped: {Dropped}",
                stats.ActiveSuppressions,
                stats.QueueSize,
                stats.DroppedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }
}
