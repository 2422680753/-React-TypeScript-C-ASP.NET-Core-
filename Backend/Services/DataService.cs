using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace IoTMonitoringPlatform.Services;

public class DataService : IDataService
{
    private readonly AppDbContext _context;
    private readonly IAlarmService _alarmService;

    public DataService(AppDbContext context, IAlarmService alarmService)
    {
        _context = context;
        _alarmService = alarmService;
    }

    public async Task AddDataAsync(CreateDeviceDataDto dto)
    {
        var device = await _context.Devices.FindAsync(dto.DeviceId);
        if (device == null)
            throw new KeyNotFoundException($"Device with id {dto.DeviceId} not found");

        var dataPoint = new DeviceData
        {
            Id = Guid.NewGuid(),
            DeviceId = dto.DeviceId,
            Metric = dto.Metric,
            Value = dto.Value,
            Unit = dto.Unit,
            Timestamp = dto.Timestamp ?? DateTime.UtcNow,
            Metadata = dto.Metadata,
            Quality = DataQuality.Good
        };

        _context.DeviceData.Add(dataPoint);

        if (device.Status != DeviceStatus.Online)
        {
            device.Status = DeviceStatus.Online;
            device.LastOnlineTime = DateTime.UtcNow;
        }

        device.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _alarmService.CheckAndTriggerAlarmsAsync(dto.DeviceId, dto.Metric, dto.Value);
    }

    public async Task AddBatchDataAsync(BatchDeviceDataDto dto)
    {
        var device = await _context.Devices.FindAsync(dto.DeviceId);
        if (device == null)
            throw new KeyNotFoundException($"Device with id {dto.DeviceId} not found");

        var timestamp = dto.Timestamp ?? DateTime.UtcNow;
        var dataPoints = new List<DeviceData>();

        foreach (var metric in dto.Metrics)
        {
            dataPoints.Add(new DeviceData
            {
                Id = Guid.NewGuid(),
                DeviceId = dto.DeviceId,
                Metric = metric.Metric,
                Value = metric.Value,
                Unit = metric.Unit,
                Timestamp = timestamp,
                Quality = DataQuality.Good
            });
        }

        _context.DeviceData.AddRange(dataPoints);

        if (device.Status != DeviceStatus.Online)
        {
            device.Status = DeviceStatus.Online;
            device.LastOnlineTime = DateTime.UtcNow;
        }

        device.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        foreach (var metric in dto.Metrics)
        {
            await _alarmService.CheckAndTriggerAlarmsAsync(dto.DeviceId, metric.Metric, metric.Value);
        }
    }

    public async Task<List<DeviceDataDto>> GetHistoricalDataAsync(HistoricalDataRequestDto request)
    {
        var query = _context.DeviceData
            .Include(d => d.Device)
            .Where(d => d.DeviceId == request.DeviceId &&
                       d.Timestamp >= request.StartTime &&
                       d.Timestamp <= request.EndTime);

        if (!string.IsNullOrEmpty(request.Metric))
        {
            query = query.Where(d => d.Metric == request.Metric);
        }

        if (request.Limit.HasValue)
        {
            query = query.Take(request.Limit.Value);
        }

        var data = await query
            .OrderByDescending(d => d.Timestamp)
            .ToListAsync();

        return data.Select(d => new DeviceDataDto
        {
            Id = d.Id,
            DeviceId = d.DeviceId,
            DeviceName = d.Device?.Name ?? string.Empty,
            Metric = d.Metric,
            Value = d.Value,
            Unit = d.Unit,
            Timestamp = d.Timestamp,
            Quality = d.Quality.ToString()
        }).ToList();
    }

    public async Task<List<AggregatedDataDto>> GetAggregatedDataAsync(HistoricalDataRequestDto request)
    {
        var query = _context.DeviceData
            .Where(d => d.DeviceId == request.DeviceId &&
                       d.Timestamp >= request.StartTime &&
                       d.Timestamp <= request.EndTime);

        if (!string.IsNullOrEmpty(request.Metric))
        {
            query = query.Where(d => d.Metric == request.Metric);
        }

        var intervalSeconds = request.IntervalSeconds ?? 300;
        var data = await query
            .GroupBy(d => new
            {
                d.Metric,
                TimeBucket = EF.Property<long>(d, "Timestamp") / intervalSeconds
            })
            .Select(g => new
            {
                g.Key.Metric,
                Min = g.Min(d => d.Value),
                Max = g.Max(d => d.Value),
                Avg = g.Average(d => d.Value),
                Sum = g.Sum(d => d.Value),
                Count = g.Count(),
                StartTime = g.Min(d => d.Timestamp),
                EndTime = g.Max(d => d.Timestamp)
            })
            .OrderBy(x => x.StartTime)
            .ToListAsync();

        return data.Select(d => new AggregatedDataDto
        {
            Metric = d.Metric,
            Min = d.Min,
            Max = d.Max,
            Avg = d.Avg,
            Sum = d.Sum,
            Count = d.Count,
            StartTime = d.StartTime,
            EndTime = d.EndTime
        }).ToList();
    }

    public async Task<RealTimeDataDto> GetLatestDataAsync(Guid deviceId)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            throw new KeyNotFoundException($"Device with id {deviceId} not found");

        var latestData = await _context.DeviceData
            .Where(d => d.DeviceId == deviceId)
            .GroupBy(d => d.Metric)
            .Select(g => g.OrderByDescending(d => d.Timestamp).First())
            .ToListAsync();

        var metrics = new Dictionary<string, double>();
        var units = new Dictionary<string, string?>();

        foreach (var data in latestData)
        {
            metrics[data.Metric] = data.Value;
            units[data.Metric] = data.Unit;
        }

        return new RealTimeDataDto
        {
            DeviceId = deviceId,
            DeviceName = device.Name,
            Metrics = metrics,
            Units = units,
            Timestamp = latestData.Any() ? latestData.Max(d => d.Timestamp) : DateTime.UtcNow,
            Status = device.Status.ToString()
        };
    }

    public async Task<List<RealTimeDataDto>> GetLatestDataForDevicesAsync(List<Guid> deviceIds)
    {
        var result = new List<RealTimeDataDto>();

        foreach (var deviceId in deviceIds)
        {
            try
            {
                var data = await GetLatestDataAsync(deviceId);
                result.Add(data);
            }
            catch (KeyNotFoundException)
            {
                // Skip devices that don't exist
            }
        }

        return result;
    }

    public async Task DeleteOldDataAsync(int retentionDays)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        var oldData = await _context.DeviceData
            .Where(d => d.Timestamp < cutoffDate)
            .ToListAsync();

        _context.DeviceData.RemoveRange(oldData);
        await _context.SaveChangesAsync();
    }
}
