using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoTMonitoringPlatform.Services;

public class EnhancedAlarmBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnhancedAlarmBackgroundService> _logger;
    
    private readonly int _queueProcessingIntervalSeconds = 3;
    private readonly int _heartbeatCheckIntervalSeconds = 30;
    private readonly int _cleanupIntervalMinutes = 60;

    public EnhancedAlarmBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<EnhancedAlarmBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Enhanced Alarm Background Service starting...");

        var lastHeartbeatCheck = DateTime.UtcNow;
        var lastCleanup = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAlarmQueueAsync();

                var now = DateTime.UtcNow;

                if ((now - lastHeartbeatCheck).TotalSeconds >= _heartbeatCheckIntervalSeconds)
                {
                    await CheckDeviceHeartbeatsAsync();
                    lastHeartbeatCheck = now;
                }

                if ((now - lastCleanup).TotalMinutes >= _cleanupIntervalMinutes)
                {
                    await CleanupExpiredItemsAsync();
                    lastCleanup = now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Enhanced Alarm Background Service");
            }

            await Task.Delay(TimeSpan.FromSeconds(_queueProcessingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Enhanced Alarm Background Service stopping...");
    }

    private async Task ProcessAlarmQueueAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        
        var alarmGovernance = scope.ServiceProvider.GetRequiredService<IEnhancedAlarmGovernanceService>();

        await alarmGovernance.ProcessQueueAsync();
    }

    private async Task CheckDeviceHeartbeatsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        
        var heartbeatService = scope.ServiceProvider.GetRequiredService<IHeartbeatService>();

        try
        {
            await heartbeatService.CheckDeviceTimeoutsAsync();

            var stats = await heartbeatService.GetHeartbeatStatsAsync();

            _logger.LogInformation(
                "Heartbeat check completed. Connected devices: {Connected}, Timeouts detected: {Timeouts}",
                stats.ConnectedDevices,
                stats.TimeoutsDetected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking device heartbeats");
        }
    }

    private async Task CleanupExpiredItemsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        
        var alarmGovernance = scope.ServiceProvider.GetRequiredService<IEnhancedAlarmGovernanceService>();

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
