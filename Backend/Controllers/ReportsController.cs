using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTMonitoringPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("device-statistics")]
    public async Task<ActionResult<DeviceStatisticsReportDto>> GetDeviceStatistics()
    {
        var report = await _reportService.GetDeviceStatisticsReportAsync();
        return Ok(report);
    }

    [HttpGet("device-availability")]
    public async Task<ActionResult<List<DeviceAvailabilityReportDto>>> GetDeviceAvailability([FromQuery] DateTime startTime, [FromQuery] DateTime endTime, [FromQuery] List<Guid>? deviceIds = null)
    {
        var request = new ReportRequestDto
        {
            StartTime = startTime,
            EndTime = endTime,
            DeviceIds = deviceIds
        };

        var report = await _reportService.GetDeviceAvailabilityReportAsync(request);
        return Ok(report);
    }

    [HttpGet("alarm")]
    public async Task<ActionResult<AlarmReportDto>> GetAlarmReport([FromQuery] DateTime startTime, [FromQuery] DateTime endTime, [FromQuery] List<Guid>? deviceIds = null)
    {
        var request = new ReportRequestDto
        {
            StartTime = startTime,
            EndTime = endTime,
            DeviceIds = deviceIds
        };

        var report = await _reportService.GetAlarmReportAsync(request);
        return Ok(report);
    }

    [HttpGet("data-quality")]
    public async Task<ActionResult<List<DataQualityReportDto>>> GetDataQualityReport([FromQuery] DateTime startTime, [FromQuery] DateTime endTime, [FromQuery] List<Guid>? deviceIds = null, [FromQuery] List<string>? metrics = null)
    {
        var request = new ReportRequestDto
        {
            StartTime = startTime,
            EndTime = endTime,
            DeviceIds = deviceIds,
            Metrics = metrics
        };

        var report = await _reportService.GetDataQualityReportAsync(request);
        return Ok(report);
    }

    [HttpGet("command-execution")]
    public async Task<ActionResult<CommandExecutionReportDto>> GetCommandExecutionReport([FromQuery] DateTime startTime, [FromQuery] DateTime endTime, [FromQuery] List<Guid>? deviceIds = null)
    {
        var request = new ReportRequestDto
        {
            StartTime = startTime,
            EndTime = endTime,
            DeviceIds = deviceIds
        };

        var report = await _reportService.GetCommandExecutionReportAsync(request);
        return Ok(report);
    }

    [HttpGet("export/csv")]
    [Authorize(Roles = "Operator,Admin,SuperAdmin")]
    public async Task<IActionResult> ExportCsv([FromQuery] string reportType, [FromQuery] DateTime startTime, [FromQuery] DateTime endTime)
    {
        try
        {
            var request = new ReportRequestDto
            {
                StartTime = startTime,
                EndTime = endTime
            };

            var csvBytes = await _reportService.ExportReportToCsvAsync(reportType, request);
            var fileName = $"{reportType}_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(csvBytes, "text/csv", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
