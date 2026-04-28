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
[Authorize]
public class AlarmsController : ControllerBase
{
    private readonly IAlarmService _alarmService;
    private readonly IHubContext<MonitoringHub, IMonitoringHubClient> _hubContext;

    public AlarmsController(
        IAlarmService alarmService,
        IHubContext<MonitoringHub, IMonitoringHubClient> hubContext)
    {
        _alarmService = alarmService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<AlarmDto>>> GetAll([FromQuery] string? status = null, [FromQuery] string? level = null, [FromQuery] Guid? deviceId = null, [FromQuery] int? limit = 100)
    {
        var alarms = await _alarmService.GetAllAsync(status, level, deviceId, limit);
        return Ok(alarms);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AlarmDto>> GetById(Guid id)
    {
        try
        {
            var alarm = await _alarmService.GetByIdAsync(id);
            return Ok(alarm);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeAlarmDto dto)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _alarmService.AcknowledgeAsync(id, userId, dto);

            var alarm = await _alarmService.GetByIdAsync(id);
            await _hubContext.BroadcastAlarmUpdate(alarm);

            return Ok(alarm);
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

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveAlarmDto dto)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _alarmService.ResolveAsync(id, userId, dto);

            var alarm = await _alarmService.GetByIdAsync(id);
            await _hubContext.BroadcastAlarmUpdate(alarm);

            return Ok(alarm);
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

    [HttpGet("statistics")]
    public async Task<ActionResult<AlarmStatisticsDto>> GetStatistics()
    {
        var statistics = await _alarmService.GetStatisticsAsync();
        return Ok(statistics);
    }

    [HttpGet("rules")]
    public async Task<ActionResult<List<AlarmRuleDto>>> GetAllRules([FromQuery] bool? isEnabled = null, [FromQuery] Guid? deviceId = null)
    {
        var rules = await _alarmService.GetAllRulesAsync(isEnabled, deviceId);
        return Ok(rules);
    }

    [HttpGet("rules/{id}")]
    public async Task<ActionResult<AlarmRuleDto>> GetRuleById(Guid id)
    {
        try
        {
            var rule = await _alarmService.GetRuleByIdAsync(id);
            return Ok(rule);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("rules")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<AlarmRuleDto>> CreateRule([FromBody] CreateAlarmRuleDto dto)
    {
        var rule = await _alarmService.CreateRuleAsync(dto);
        return CreatedAtAction(nameof(GetRuleById), new { id = rule.Id }, rule);
    }

    [HttpPut("rules/{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<AlarmRuleDto>> UpdateRule(Guid id, [FromBody] UpdateAlarmRuleDto dto)
    {
        try
        {
            var rule = await _alarmService.UpdateRuleAsync(id, dto);
            return Ok(rule);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("rules/{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        try
        {
            await _alarmService.DeleteRuleAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
