using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Hubs;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace IoTMonitoringPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly IDeviceService _deviceService;
    private readonly IHubContext<MonitoringHub, IMonitoringHubClient> _hubContext;

    public DataController(
        IDataService dataService,
        IDeviceService deviceService,
        IHubContext<MonitoringHub, IMonitoringHubClient> hubContext)
    {
        _dataService = dataService;
        _deviceService = deviceService;
        _hubContext = hubContext;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> AddData([FromBody] CreateDeviceDataDto dto)
    {
        try
        {
            await _dataService.AddDataAsync(dto);

            var latestData = await _dataService.GetLatestDataAsync(dto.DeviceId);
            await _hubContext.BroadcastDeviceData(latestData);

            return Ok(new { Success = true, Message = "Data added successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("batch")]
    [AllowAnonymous]
    public async Task<IActionResult> AddBatchData([FromBody] BatchDeviceDataDto dto)
    {
        try
        {
            await _dataService.AddBatchDataAsync(dto);

            var latestData = await _dataService.GetLatestDataAsync(dto.DeviceId);
            await _hubContext.BroadcastDeviceData(latestData);

            return Ok(new { Success = true, Message = $"Added {dto.Metrics.Count} data points" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("historical")]
    [Authorize]
    public async Task<ActionResult<List<DeviceDataDto>>> GetHistoricalData([FromQuery] Guid deviceId, [FromQuery] DateTime startTime, [FromQuery] DateTime endTime, [FromQuery] string? metric = null, [FromQuery] int? limit = 1000)
    {
        var request = new HistoricalDataRequestDto
        {
            DeviceId = deviceId,
            Metric = metric,
            StartTime = startTime,
            EndTime = endTime,
            Limit = limit
        };

        var data = await _dataService.GetHistoricalDataAsync(request);
        return Ok(data);
    }

    [HttpGet("aggregated")]
    [Authorize]
    public async Task<ActionResult<List<AggregatedDataDto>>> GetAggregatedData([FromQuery] Guid deviceId, [FromQuery] DateTime startTime, [FromQuery] DateTime endTime, [FromQuery] string? metric = null, [FromQuery] int intervalSeconds = 300)
    {
        var request = new HistoricalDataRequestDto
        {
            DeviceId = deviceId,
            Metric = metric,
            StartTime = startTime,
            EndTime = endTime,
            IntervalSeconds = intervalSeconds
        };

        var data = await _dataService.GetAggregatedDataAsync(request);
        return Ok(data);
    }

    [HttpGet("latest/{deviceId}")]
    [Authorize]
    public async Task<ActionResult<RealTimeDataDto>> GetLatestData(Guid deviceId)
    {
        try
        {
            var data = await _dataService.GetLatestDataAsync(deviceId);
            return Ok(data);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("latest-batch")]
    [Authorize]
    public async Task<ActionResult<List<RealTimeDataDto>>> GetLatestDataForDevices([FromBody] List<Guid> deviceIds)
    {
        var data = await _dataService.GetLatestDataForDevicesAsync(deviceIds);
        return Ok(data);
    }

    [HttpDelete("cleanup")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> CleanupOldData([FromQuery] int retentionDays = 30)
    {
        await _dataService.DeleteOldDataAsync(retentionDays);
        return Ok(new { Success = true, Message = $"Data older than {retentionDays} days deleted" });
    }
}
