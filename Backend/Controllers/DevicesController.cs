using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Hubs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace IoTMonitoringPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IHubContext<MonitoringHub, IMonitoringHubClient> _hubContext;

    public DevicesController(IDeviceService deviceService, IHubContext<MonitoringHub, IMonitoringHubClient> hubContext)
    {
        _deviceService = deviceService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> GetAll([FromQuery] string? status = null, [FromQuery] string? deviceType = null, [FromQuery] Guid? groupId = null)
    {
        var devices = await _deviceService.GetAllAsync(status, deviceType, groupId);
        return Ok(devices);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DeviceDto>> GetById(Guid id)
    {
        try
        {
            var device = await _deviceService.GetByIdAsync(id);
            return Ok(device);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<DeviceDto>> Create([FromBody] CreateDeviceDto dto)
    {
        try
        {
            var device = await _deviceService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<DeviceDto>> Update(Guid id, [FromBody] UpdateDeviceDto dto)
    {
        try
        {
            var device = await _deviceService.UpdateAsync(id, dto);
            return Ok(device);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _deviceService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<DeviceStatisticsDto>> GetStatistics()
    {
        var statistics = await _deviceService.GetStatisticsAsync();
        return Ok(statistics);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Operator,Admin,SuperAdmin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] DeviceStatusUpdateDto dto)
    {
        try
        {
            var status = Enum.Parse<DeviceStatus>(dto.Status, true);
            await _deviceService.UpdateStatusAsync(id, status);

            var statusDto = new DeviceStatusDto
            {
                DeviceId = id,
                Status = dto.Status,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.BroadcastDeviceStatusChange(statusDto);

            return Ok(statusDto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException)
        {
            return BadRequest($"Invalid status: {dto.Status}");
        }
    }

    [HttpGet("group/{groupId}")]
    public async Task<ActionResult<List<DeviceDto>>> GetByGroup(Guid groupId)
    {
        var devices = await _deviceService.GetByGroupAsync(groupId);
        return Ok(devices);
    }

    [HttpGet("serial/{serialNumber}")]
    public async Task<ActionResult<DeviceDto>> GetBySerialNumber(string serialNumber)
    {
        var device = await _deviceService.GetBySerialNumberAsync(serialNumber);
        if (device == null)
            return NotFound($"Device with serial number {serialNumber} not found");

        return Ok(device);
    }
}

public class DeviceStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
}
