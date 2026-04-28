using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace IoTMonitoringPlatform.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _context;

    public ReportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceStatisticsReportDto> GetDeviceStatisticsReportAsync()
    {
        var totalDevices = await _context.Devices.CountAsync();
        var onlineDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Online);
        var offlineDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Offline);
        var warningDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Warning);
        var errorDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Error);
        var maintenanceDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Maintenance);

        var byDeviceType = await _context.Devices
            .GroupBy(d => d.DeviceType)
            .Select(g => new DeviceTypeStatisticsDto
            {
                DeviceType = g.Key,
                Count = g.Count(),
                OnlineCount = g.Count(d => d.Status == DeviceStatus.Online),
                OfflineCount = g.Count(d => d.Status == DeviceStatus.Offline)
            })
            .ToListAsync();

        var byGroup = await _context.Devices
            .Include(d => d.Group)
            .Where(d => d.GroupId != null)
            .GroupBy(d => new { d.GroupId, d.Group!.Name })
            .Select(g => new DeviceGroupStatisticsDto
            {
                GroupId = g.Key.GroupId!.Value,
                GroupName = g.Key.Name,
                DeviceCount = g.Count(),
                OnlineCount = g.Count(d => d.Status == DeviceStatus.Online),
                OfflineCount = g.Count(d => d.Status == DeviceStatus.Offline)
            })
            .ToListAsync();

        return new DeviceStatisticsReportDto
        {
            TotalDevices = totalDevices,
            OnlineDevices = onlineDevices,
            OfflineDevices = offlineDevices,
            WarningDevices = warningDevices,
            ErrorDevices = errorDevices,
            MaintenanceDevices = maintenanceDevices,
            OnlinePercentage = totalDevices > 0 ? Math.Round((double)onlineDevices / totalDevices * 100, 2) : 0,
            OfflinePercentage = totalDevices > 0 ? Math.Round((double)offlineDevices / totalDevices * 100, 2) : 0,
            WarningPercentage = totalDevices > 0 ? Math.Round((double)warningDevices / totalDevices * 100, 2) : 0,
            ErrorPercentage = totalDevices > 0 ? Math.Round((double)errorDevices / totalDevices * 100, 2) : 0,
            ByDeviceType = byDeviceType,
            ByGroup = byGroup
        };
    }

    public async Task<List<DeviceAvailabilityReportDto>> GetDeviceAvailabilityReportAsync(ReportRequestDto request)
    {
        var query = _context.Devices.AsQueryable();

        if (request.DeviceIds != null && request.DeviceIds.Any())
        {
            query = query.Where(d => request.DeviceIds.Contains(d.Id));
        }

        var devices = await query
            .Include(d => d.DataPoints)
            .ToListAsync();

        var reports = new List<DeviceAvailabilityReportDto>();

        foreach (var device in devices)
        {
            var onlineData = await _context.DeviceData
                .Where(d => d.DeviceId == device.Id &&
                           d.Timestamp >= request.StartTime &&
                           d.Timestamp <= request.EndTime)
                .OrderBy(d => d.Timestamp)
                .ToListAsync();

            var totalDuration = request.EndTime - request.StartTime;
            var onlineDuration = TimeSpan.Zero;

            if (onlineData.Any())
            {
                var firstData = onlineData.First().Timestamp;
                var lastData = onlineData.Last().Timestamp;
                onlineDuration = lastData - firstData;
            }

            var offlineDuration = totalDuration - onlineDuration;

            var onlineCount = onlineData
                .GroupBy(d => d.Timestamp.Date)
                .Count();

            reports.Add(new DeviceAvailabilityReportDto
            {
                DeviceId = device.Id,
                DeviceName = device.Name,
                DeviceType = device.DeviceType,
                AvailabilityPercentage = totalDuration.TotalSeconds > 0
                    ? Math.Round(onlineDuration.TotalSeconds / totalDuration.TotalSeconds * 100, 2)
                    : 0,
                OnlineDuration = onlineDuration,
                OfflineDuration = offlineDuration,
                OnlineCount = onlineCount,
                OfflineCount = Math.Max(0, (int)totalDuration.TotalDays - onlineCount + 1),
                StartTime = request.StartTime,
                EndTime = request.EndTime
            });
        }

        return reports;
    }

    public async Task<AlarmReportDto> GetAlarmReportAsync(ReportRequestDto request)
    {
        var query = _context.Alarms
            .Include(a => a.Device)
            .Where(a => a.TriggeredAt >= request.StartTime && a.TriggeredAt <= request.EndTime);

        if (request.DeviceIds != null && request.DeviceIds.Any())
        {
            query = query.Where(a => request.DeviceIds.Contains(a.DeviceId));
        }

        var alarms = await query.ToListAsync();

        var totalCount = alarms.Count;
        var activeCount = alarms.Count(a => a.Status == AlarmStatus.Active);
        var resolvedCount = alarms.Count(a => a.Status == AlarmStatus.Resolved);

        var resolvedAlarms = alarms.Where(a => a.ResolvedAt.HasValue).ToList();
        var averageResolutionTime = resolvedAlarms.Any()
            ? TimeSpan.FromTicks((long)resolvedAlarms.Average(a => (a.ResolvedAt!.Value - a.TriggeredAt).Ticks))
            : TimeSpan.Zero;

        var topAlarmDevices = alarms
            .GroupBy(a => new { a.DeviceId, a.Device!.Name })
            .Select(g => new TopAlarmDeviceDto
            {
                DeviceId = g.Key.DeviceId,
                DeviceName = g.Key.Name,
                AlarmCount = g.Count(),
                CriticalCount = g.Count(a => a.Level == AlarmLevel.Critical),
                WarningCount = g.Count(a => a.Level == AlarmLevel.Warning)
            })
            .OrderByDescending(d => d.AlarmCount)
            .Take(10)
            .ToList();

        var trend = alarms
            .GroupBy(a => a.TriggeredAt.Date)
            .Select(g => new AlarmTrendDto
            {
                Date = g.Key,
                TotalAlarms = g.Count(),
                CriticalAlarms = g.Count(a => a.Level == AlarmLevel.Critical),
                WarningAlarms = g.Count(a => a.Level == AlarmLevel.Warning),
                InformationAlarms = g.Count(a => a.Level == AlarmLevel.Information)
            })
            .OrderBy(t => t.Date)
            .ToList();

        return new AlarmReportDto
        {
            AlarmLevel = "All",
            TotalCount = totalCount,
            ActiveCount = activeCount,
            ResolvedCount = resolvedCount,
            AverageResolutionTime = averageResolutionTime,
            TopAlarmDevices = topAlarmDevices,
            Trend = trend,
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };
    }

    public async Task<List<DataQualityReportDto>> GetDataQualityReportAsync(ReportRequestDto request)
    {
        var query = _context.DeviceData
            .Include(d => d.Device)
            .Where(d => d.Timestamp >= request.StartTime && d.Timestamp <= request.EndTime);

        if (request.DeviceIds != null && request.DeviceIds.Any())
        {
            query = query.Where(d => request.DeviceIds.Contains(d.DeviceId));
        }

        if (request.Metrics != null && request.Metrics.Any())
        {
            query = query.Where(d => request.Metrics.Contains(d.Metric));
        }

        var dataPoints = await query.ToListAsync();

        var reports = dataPoints
            .GroupBy(d => new { d.DeviceId, d.Device!.Name, d.Metric })
            .Select(g => new DataQualityReportDto
            {
                DeviceId = g.Key.DeviceId,
                DeviceName = g.Key.Name,
                Metric = g.Key.Metric,
                TotalDataPoints = g.Count(),
                GoodDataPoints = g.Count(d => d.Quality == DataQuality.Good),
                BadDataPoints = g.Count(d => d.Quality == DataQuality.Bad),
                UncertainDataPoints = g.Count(d => d.Quality == DataQuality.Uncertain),
                GoodPercentage = Math.Round((double)g.Count(d => d.Quality == DataQuality.Good) / g.Count() * 100, 2),
                BadPercentage = Math.Round((double)g.Count(d => d.Quality == DataQuality.Bad) / g.Count() * 100, 2),
                StartTime = request.StartTime,
                EndTime = request.EndTime
            })
            .ToList();

        return reports;
    }

    public async Task<CommandExecutionReportDto> GetCommandExecutionReportAsync(ReportRequestDto request)
    {
        var query = _context.ControlCommands
            .Include(c => c.Device)
            .Where(c => c.CreatedAt >= request.StartTime && c.CreatedAt <= request.EndTime);

        if (request.DeviceIds != null && request.DeviceIds.Any())
        {
            query = query.Where(c => request.DeviceIds.Contains(c.DeviceId));
        }

        var commands = await query.ToListAsync();

        var totalCommands = commands.Count;
        var successfulCommands = commands.Count(c => c.Status == CommandStatus.Executed);
        var failedCommands = commands.Count(c => c.Status == CommandStatus.Failed || c.Status == CommandStatus.TimedOut);
        var pendingCommands = commands.Count(c => c.Status == CommandStatus.Pending || c.Status == CommandStatus.Queued || c.Status == CommandStatus.Sent || c.Status == CommandStatus.Executing);

        var executedCommands = commands.Where(c => c.ExecutedAt.HasValue && c.SentAt.HasValue).ToList();
        var averageExecutionTime = executedCommands.Any()
            ? TimeSpan.FromTicks((long)executedCommands.Average(c => (c.ExecutedAt!.Value - c.SentAt!.Value).Ticks))
            : TimeSpan.Zero;

        var byCommandType = commands
            .GroupBy(c => c.Command)
            .Select(g => new CommandTypeStatisticsDto
            {
                Command = g.Key,
                Count = g.Count(),
                SuccessCount = g.Count(c => c.Status == CommandStatus.Executed),
                FailedCount = g.Count(c => c.Status == CommandStatus.Failed || c.Status == CommandStatus.TimedOut)
            })
            .ToList();

        var trend = commands
            .GroupBy(c => c.CreatedAt.Date)
            .Select(g => new CommandTrendDto
            {
                Date = g.Key,
                TotalCommands = g.Count(),
                SuccessfulCommands = g.Count(c => c.Status == CommandStatus.Executed),
                FailedCommands = g.Count(c => c.Status == CommandStatus.Failed || c.Status == CommandStatus.TimedOut)
            })
            .OrderBy(t => t.Date)
            .ToList();

        return new CommandExecutionReportDto
        {
            TotalCommands = totalCommands,
            SuccessfulCommands = successfulCommands,
            FailedCommands = failedCommands,
            PendingCommands = pendingCommands,
            SuccessRate = totalCommands > 0 ? Math.Round((double)successfulCommands / totalCommands * 100, 2) : 0,
            AverageExecutionTime = averageExecutionTime,
            ByCommandType = byCommandType,
            Trend = trend,
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };
    }

    public async Task<byte[]> ExportReportToCsvAsync(string reportType, ReportRequestDto request)
    {
        var sb = new StringBuilder();

        switch (reportType.ToLower())
        {
            case "alarm":
                var alarmReport = await GetAlarmReportAsync(request);
                sb.AppendLine("Date,Total Alarms,Critical,Warning,Information");
                foreach (var item in alarmReport.Trend)
                {
                    sb.AppendLine($"{item.Date:yyyy-MM-dd},{item.TotalAlarms},{item.CriticalAlarms},{item.WarningAlarms},{item.InformationAlarms}");
                }
                break;

            case "device":
                var deviceReports = await GetDeviceAvailabilityReportAsync(request);
                sb.AppendLine("Device ID,Device Name,Device Type,Availability(%),Online Duration(hours),Offline Duration(hours)");
                foreach (var item in deviceReports)
                {
                    sb.AppendLine($"{item.DeviceId},{item.DeviceName},{item.DeviceType},{item.AvailabilityPercentage},{item.OnlineDuration.TotalHours:F2},{item.OfflineDuration.TotalHours:F2}");
                }
                break;

            default:
                throw new ArgumentException($"Unknown report type: {reportType}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public Task<byte[]> ExportReportToPdfAsync(string reportType, ReportRequestDto request)
    {
        throw new NotImplementedException("PDF export requires additional libraries like iTextSharp");
    }
}
