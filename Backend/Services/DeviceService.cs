using AutoMapper;
using IoTMonitoringPlatform.Data;
using IoTMonitoringPlatform.DTOs;
using IoTMonitoringPlatform.Models;
using IoTMonitoringPlatform.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IoTMonitoringPlatform.Services;

public class DeviceService : IDeviceService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public DeviceService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<DeviceDto> GetByIdAsync(Guid id)
    {
        var device = await _context.Devices
            .Include(d => d.Group)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
            throw new KeyNotFoundException($"Device with id {id} not found");

        return MapToDeviceDto(device);
    }

    public async Task<List<DeviceDto>> GetAllAsync(string? status = null, string? deviceType = null, Guid? groupId = null)
    {
        var query = _context.Devices
            .Include(d => d.Group)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DeviceStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(d => d.Status == parsedStatus);
        }

        if (!string.IsNullOrEmpty(deviceType))
        {
            query = query.Where(d => d.DeviceType == deviceType);
        }

        if (groupId.HasValue)
        {
            query = query.Where(d => d.GroupId == groupId);
        }

        var devices = await query.OrderByDescending(d => d.UpdatedAt).ToListAsync();
        return devices.Select(MapToDeviceDto).ToList();
    }

    public async Task<DeviceDto> CreateAsync(CreateDeviceDto dto)
    {
        var existingDevice = await _context.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == dto.SerialNumber);

        if (existingDevice != null)
            throw new InvalidOperationException($"Device with serial number {dto.SerialNumber} already exists");

        var device = _mapper.Map<Device>(dto);
        device.Id = Guid.NewGuid();
        device.CreatedAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;
        device.Status = DeviceStatus.Offline;

        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        return MapToDeviceDto(device);
    }

    public async Task<DeviceDto> UpdateAsync(Guid id, UpdateDeviceDto dto)
    {
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
            throw new KeyNotFoundException($"Device with id {id} not found");

        if (!string.IsNullOrEmpty(dto.Name))
            device.Name = dto.Name;

        if (dto.Description != null)
            device.Description = dto.Description;

        if (dto.Location != null)
            device.Location = dto.Location;

        if (dto.Latitude.HasValue)
            device.Latitude = dto.Latitude;

        if (dto.Longitude.HasValue)
            device.Longitude = dto.Longitude;

        if (dto.FirmwareVersion != null)
            device.FirmwareVersion = dto.FirmwareVersion;

        if (dto.GroupId.HasValue)
            device.GroupId = dto.GroupId;

        if (dto.IsActive.HasValue)
            device.IsActive = dto.IsActive.Value;

        device.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDeviceDto(device);
    }

    public async Task DeleteAsync(Guid id)
    {
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
            throw new KeyNotFoundException($"Device with id {id} not found");

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid deviceId, DeviceStatus status)
    {
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device == null)
            throw new KeyNotFoundException($"Device with id {deviceId} not found");

        device.Status = status;
        device.UpdatedAt = DateTime.UtcNow;

        if (status == DeviceStatus.Online)
        {
            device.LastOnlineTime = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<DeviceStatisticsDto> GetStatisticsAsync()
    {
        var totalDevices = await _context.Devices.CountAsync();
        var onlineDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Online);
        var offlineDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Offline);
        var warningDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Warning);
        var errorDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Error);
        var maintenanceDevices = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Maintenance);

        return new DeviceStatisticsDto
        {
            TotalDevices = totalDevices,
            OnlineDevices = onlineDevices,
            OfflineDevices = offlineDevices,
            WarningDevices = warningDevices,
            ErrorDevices = errorDevices,
            MaintenanceDevices = maintenanceDevices
        };
    }

    public async Task<List<DeviceDto>> GetByGroupAsync(Guid groupId)
    {
        var devices = await _context.Devices
            .Include(d => d.Group)
            .Where(d => d.GroupId == groupId)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();

        return devices.Select(MapToDeviceDto).ToList();
    }

    public async Task<DeviceDto?> GetBySerialNumberAsync(string serialNumber)
    {
        var device = await _context.Devices
            .Include(d => d.Group)
            .FirstOrDefaultAsync(d => d.SerialNumber == serialNumber);

        return device != null ? MapToDeviceDto(device) : null;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Devices.AnyAsync(d => d.Id == id);
    }

    public async Task UpdateLastOnlineTimeAsync(Guid deviceId)
    {
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device == null)
            throw new KeyNotFoundException($"Device with id {deviceId} not found");

        device.LastOnlineTime = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    private static DeviceDto MapToDeviceDto(Device device)
    {
        return new DeviceDto
        {
            Id = device.Id,
            Name = device.Name,
            DeviceType = device.DeviceType,
            SerialNumber = device.SerialNumber,
            Description = device.Description,
            Location = device.Location,
            Latitude = device.Latitude,
            Longitude = device.Longitude,
            Status = device.Status.ToString(),
            LastOnlineTime = device.LastOnlineTime,
            CreatedAt = device.CreatedAt,
            UpdatedAt = device.UpdatedAt,
            IsActive = device.IsActive,
            Manufacturer = device.Manufacturer,
            FirmwareVersion = device.FirmwareVersion,
            HardwareVersion = device.HardwareVersion,
            GroupId = device.GroupId,
            GroupName = device.Group?.Name
        };
    }
}
