using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace IoTMonitoringPlatform.Services;

public class ControlService : IControlService
{
    private readonly AppDbContext _context;

    public ControlService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControlCommandDto> SendCommandAsync(CreateControlCommandDto dto, string? userId)
    {
        var device = await _context.Devices.FindAsync(dto.DeviceId);
        if (device == null)
            throw new KeyNotFoundException($"Device with id {dto.DeviceId} not found");

        if (device.Status == DeviceStatus.Offline)
            throw new InvalidOperationException("Cannot send command to offline device");

        var command = new ControlCommand
        {
            Id = Guid.NewGuid(),
            DeviceId = dto.DeviceId,
            Command = dto.Command,
            Parameters = dto.Parameters != null ? JsonConvert.SerializeObject(dto.Parameters) : null,
            Status = CommandStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            MaxRetries = dto.MaxRetries ?? 3,
            RetryCount = 0,
            TimeoutSeconds = dto.TimeoutSeconds ?? 30,
            Priority = Enum.Parse<CommandPriority>(dto.Priority, true)
        };

        _context.ControlCommands.Add(command);
        await _context.SaveChangesAsync();

        await QueueCommandForExecution(command);

        return MapToControlCommandDto(command, device.Name);
    }

    public async Task<List<ControlCommandDto>> SendBatchCommandAsync(BatchControlCommandDto dto, string? userId)
    {
        var results = new List<ControlCommandDto>();
        var devices = await _context.Devices
            .Where(d => dto.DeviceIds.Contains(d.Id))
            .ToListAsync();

        var deviceMap = devices.ToDictionary(d => d.Id);

        foreach (var deviceId in dto.DeviceIds)
        {
            try
            {
                var createDto = new CreateControlCommandDto
                {
                    DeviceId = deviceId,
                    Command = dto.Command,
                    Parameters = dto.Parameters,
                    MaxRetries = dto.MaxRetries,
                    TimeoutSeconds = dto.TimeoutSeconds,
                    Priority = dto.Priority
                };

                var result = await SendCommandAsync(createDto, userId);
                results.Add(result);
            }
            catch (Exception ex)
            {
                var deviceName = deviceMap.TryGetValue(deviceId, out var dev) ? dev.Name : "Unknown";
                results.Add(new ControlCommandDto
                {
                    DeviceId = deviceId,
                    DeviceName = deviceName,
                    Command = dto.Command,
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return results;
    }

    public async Task<ControlCommandDto> GetByIdAsync(Guid id)
    {
        var command = await _context.ControlCommands
            .Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (command == null)
            throw new KeyNotFoundException($"Control command with id {id} not found");

        return MapToControlCommandDto(command, command.Device?.Name ?? string.Empty);
    }

    public async Task<List<ControlCommandDto>> GetByDeviceAsync(Guid deviceId, int? limit = null)
    {
        var query = _context.ControlCommands
            .Include(c => c.Device)
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.CreatedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<ControlCommand>)query.Take(limit.Value);
        }

        var commands = await query.ToListAsync();
        return commands.Select(c => MapToControlCommandDto(c, c.Device?.Name ?? string.Empty)).ToList();
    }

    public async Task<List<ControlCommandDto>> GetAllAsync(string? status = null, Guid? deviceId = null, int? limit = null)
    {
        var query = _context.ControlCommands
            .Include(c => c.Device)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<CommandStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(c => c.Status == parsedStatus);
        }

        if (deviceId.HasValue)
        {
            query = query.Where(c => c.DeviceId == deviceId);
        }

        query = query.OrderByDescending(c => c.CreatedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<ControlCommand>)query.Take(limit.Value);
        }

        var commands = await query.ToListAsync();
        return commands.Select(c => MapToControlCommandDto(c, c.Device?.Name ?? string.Empty)).ToList();
    }

    public async Task UpdateCommandStatusAsync(Guid commandId, CommandStatus status, string? result = null, string? errorMessage = null)
    {
        var command = await _context.ControlCommands.FindAsync(commandId);

        if (command == null)
            throw new KeyNotFoundException($"Control command with id {commandId} not found");

        command.Status = status;

        if (status == CommandStatus.Sent)
        {
            command.SentAt = DateTime.UtcNow;
        }
        else if (status == CommandStatus.Executed)
        {
            command.ExecutedAt = DateTime.UtcNow;
            command.Result = result;
        }
        else if (status == CommandStatus.Failed || status == CommandStatus.TimedOut)
        {
            command.FailedAt = DateTime.UtcNow;
            command.ErrorMessage = errorMessage;
        }

        await _context.SaveChangesAsync();
    }

    public async Task CancelCommandAsync(Guid commandId)
    {
        var command = await _context.ControlCommands.FindAsync(commandId);

        if (command == null)
            throw new KeyNotFoundException($"Control command with id {commandId} not found");

        if (command.Status != CommandStatus.Pending && command.Status != CommandStatus.Queued)
            throw new InvalidOperationException("Cannot cancel command that is already executing or completed");

        command.Status = CommandStatus.Cancelled;
        await _context.SaveChangesAsync();
    }

    public async Task<CommandResultDto> GetCommandResultAsync(Guid commandId)
    {
        var command = await _context.ControlCommands.FindAsync(commandId);

        if (command == null)
            throw new KeyNotFoundException($"Control command with id {commandId} not found");

        return new CommandResultDto
        {
            CommandId = commandId,
            Success = command.Status == CommandStatus.Executed,
            Result = command.Result,
            ErrorMessage = command.ErrorMessage,
            Timestamp = command.ExecutedAt ?? command.FailedAt ?? command.CreatedAt
        };
    }

    private async Task QueueCommandForExecution(ControlCommand command)
    {
        command.Status = CommandStatus.Queued;
        await _context.SaveChangesAsync();

        await Task.Run(async () =>
        {
            try
            {
                await UpdateCommandStatusAsync(command.Id, CommandStatus.Sent);

                await Task.Delay(1000);

                var success = new Random().Next(100) < 90;

                if (success)
                {
                    await UpdateCommandStatusAsync(command.Id, CommandStatus.Executed,
                        $"Command {command.Command} executed successfully at {DateTime.UtcNow}");
                }
                else
                {
                    if (command.RetryCount < command.MaxRetries)
                    {
                        command.RetryCount++;
                        await _context.SaveChangesAsync();
                        await Task.Delay(500);
                        await UpdateCommandStatusAsync(command.Id, CommandStatus.Executed,
                            $"Command {command.Command} executed successfully after {command.RetryCount} retries");
                    }
                    else
                    {
                        await UpdateCommandStatusAsync(command.Id, CommandStatus.Failed,
                            null, $"Command {command.Command} failed after {command.MaxRetries} retries");
                    }
                }
            }
            catch (Exception ex)
            {
                await UpdateCommandStatusAsync(command.Id, CommandStatus.Failed, null, ex.Message);
            }
        });
    }

    private static ControlCommandDto MapToControlCommandDto(ControlCommand command, string deviceName)
    {
        return new ControlCommandDto
        {
            Id = command.Id,
            DeviceId = command.DeviceId,
            DeviceName = deviceName,
            Command = command.Command,
            Parameters = command.Parameters,
            Status = command.Status.ToString(),
            CreatedAt = command.CreatedAt,
            SentAt = command.SentAt,
            ExecutedAt = command.ExecutedAt,
            FailedAt = command.FailedAt,
            CreatedBy = command.CreatedBy,
            Result = command.Result,
            ErrorMessage = command.ErrorMessage,
            RetryCount = command.RetryCount,
            MaxRetries = command.MaxRetries,
            TimeoutSeconds = command.TimeoutSeconds,
            Priority = command.Priority.ToString()
        };
    }
}
