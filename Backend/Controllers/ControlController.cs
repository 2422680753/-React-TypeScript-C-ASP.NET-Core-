using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Hubs;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace IoTMonitoringPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Operator,Admin,SuperAdmin")]
public class ControlController : ControllerBase
{
    private readonly IControlService _controlService;
    private readonly IHubContext<MonitoringHub, IMonitoringHubClient> _hubContext;

    public ControlController(
        IControlService controlService,
        IHubContext<MonitoringHub, IMonitoringHubClient> hubContext)
    {
        _controlService = controlService;
        _hubContext = hubContext;
    }

    [HttpPost("send")]
    public async Task<ActionResult<ControlCommandDto>> SendCommand([FromBody] CreateControlCommandDto dto)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var command = await _controlService.SendCommandAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = command.Id }, command);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("send-batch")]
    public async Task<ActionResult<List<ControlCommandDto>>> SendBatchCommand([FromBody] BatchControlCommandDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var commands = await _controlService.SendBatchCommandAsync(dto, userId);
        return Ok(commands);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ControlCommandDto>> GetById(Guid id)
    {
        try
        {
            var command = await _controlService.GetByIdAsync(id);
            return Ok(command);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<ControlCommandDto>>> GetAll([FromQuery] string? status = null, [FromQuery] Guid? deviceId = null, [FromQuery] int? limit = 100)
    {
        var commands = await _controlService.GetAllAsync(status, deviceId, limit);
        return Ok(commands);
    }

    [HttpGet("device/{deviceId}")]
    public async Task<ActionResult<List<ControlCommandDto>>> GetByDevice(Guid deviceId, [FromQuery] int? limit = 50)
    {
        var commands = await _controlService.GetByDeviceAsync(deviceId, limit);
        return Ok(commands);
    }

    [HttpGet("result/{id}")]
    public async Task<ActionResult<CommandResultDto>> GetCommandResult(Guid id)
    {
        try
        {
            var result = await _controlService.GetCommandResultAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("cancel/{id}")]
    public async Task<IActionResult> CancelCommand(Guid id)
    {
        try
        {
            await _controlService.CancelCommandAsync(id);
            var command = await _controlService.GetByIdAsync(id);
            return Ok(command);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
