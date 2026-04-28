using IoTMonitoringPlatform.DTOs;

namespace IoTMonitoringPlatform.Services.Interfaces;

public interface IControlService
{
    Task<ControlCommandDto> SendCommandAsync(CreateControlCommandDto dto, string? userId);
    Task<List<ControlCommandDto>> SendBatchCommandAsync(BatchControlCommandDto dto, string? userId);
    Task<ControlCommandDto> GetByIdAsync(Guid id);
    Task<List<ControlCommandDto>> GetByDeviceAsync(Guid deviceId, int? limit = null);
    Task<List<ControlCommandDto>> GetAllAsync(string? status = null, Guid? deviceId = null, int? limit = null);
    Task UpdateCommandStatusAsync(Guid commandId, CommandStatus status, string? result = null, string? errorMessage = null);
    Task CancelCommandAsync(Guid commandId);
    Task<CommandResultDto> GetCommandResultAsync(Guid commandId);
}
