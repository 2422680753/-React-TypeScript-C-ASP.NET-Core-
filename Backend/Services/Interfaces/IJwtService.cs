using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;

namespace IoTMonitoringPlatform.Services.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    string GenerateRefreshToken();
    Guid? ValidateToken(string token);
    AuthResponseDto GenerateAuthResponse(User user);
}
