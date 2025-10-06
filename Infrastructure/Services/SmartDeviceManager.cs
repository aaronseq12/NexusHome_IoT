using Microsoft.EntityFrameworkCore;
using NexusHome.IoT.Core.Domain;
using NexusHome.IoT.Core.Services.Interfaces;
using NexusHome.IoT.Infrastructure.Data;

namespace NexusHome.IoT.Infrastructure.Services;

public class SmartDeviceManager : ISmartDeviceManager
{
    private readonly SmartHomeDbContext _context;
    private readonly ILogger<SmartDeviceManager> _logger;
    private readonly IMqttClientService _mqttService;

    public SmartDeviceManager(
        SmartHomeDbContext context,
        ILogger<SmartDeviceManager> logger,
        IMqttClientService mqttService)
    {
        _context = context;
        _logger = logger;
        _mqttService = mqttService;
    }

    public async Task<IEnumerable<SmartDevice>> GetAllDevicesAsync()
    {
        return await _context.SmartDevices
            .Include(d => d.Room)
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<SmartDevice?> GetDeviceByIdAsync(string deviceId)
    {
        return await _context.SmartDevices
            .Include(d => d.Room)
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.IsActive);
    }

    public async Task<SmartDevice> AddDeviceAsync(SmartDevice device)
    {
        device.CreatedAt = DateTime.UtcNow;
        device.IsActive = true;
        
        _context.SmartDevices.Add(device);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Device {DeviceId} ({Name}) added successfully", device.DeviceId, device.Name);
        
        // Publish device addition event
        await _mqttService.PublishAsync($"nexushome/devices/{device.DeviceId}/status", 
            System.Text.Json.JsonSerializer.Serialize(new { Status = "Added", Timestamp = DateTime.UtcNow }));
        
        return device;
    }

    public async Task<SmartDevice> UpdateDeviceAsync(SmartDevice device)
    {
        var existingDevice = await _context.SmartDevices
            .FirstOrDefaultAsync(d => d.DeviceId == device.DeviceId);
        
        if (existingDevice == null)
            throw new ArgumentException($"Device {device.DeviceId} not found");
        
        existingDevice.Name = device.Name;
        existingDevice.Location = device.Location;
        existingDevice.RoomId = device.RoomId;
        existingDevice.Properties = device.Properties;
        existingDevice.Configuration = device.Configuration;
        existingDevice.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Device {DeviceId} updated successfully", device.DeviceId);
        
        return existingDevice;
    }

    public async Task<bool> DeleteDeviceAsync(string deviceId)
    {
        var device = await _context.SmartDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        
        if (device == null)
            return false;
        
        device.IsActive = false;
        device.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Device {DeviceId} marked as inactive", deviceId);
        
        return true;
    }

    public async Task<bool> ToggleDeviceAsync(string deviceId)
    {
        var device = await GetDeviceByIdAsync(deviceId);
        if (device == null)
            return false;
        
        // Toggle device state via MQTT command
        var command = new { Command = "toggle", Timestamp = DateTime.UtcNow };
        await _mqttService.PublishAsync($"nexushome/commands/{deviceId}", 
            System.Text.Json.JsonSerializer.Serialize(command));
        
        _logger.LogInformation("Toggle command sent to device {DeviceId}", deviceId);
        
        return true;
    }

    public async Task ProcessTelemetryDataAsync(DeviceTelemetryRequest request)
    {
        var device = await GetDeviceByIdAsync(request.DeviceId);
        if (device == null)
        {
            _logger.LogWarning("Telemetry received for unknown device {DeviceId}", request.DeviceId);
            return;
        }
        
        // Update device last seen
        device.LastSeen = request.Timestamp;
        device.IsOnline = true;
        
        // Store energy reading if power data is available
        if (request.SensorData.TryGetValue("power", out var powerValue) && powerValue != null)
        {
            var energyReading = new EnergyReading
            {
                DeviceId = request.DeviceId,
                PowerConsumption = Convert.ToDecimal(powerValue),
                Voltage = request.SensorData.TryGetValue("voltage", out var v) ? Convert.ToDecimal(v) : 0,
                Current = request.SensorData.TryGetValue("current", out var c) ? Convert.ToDecimal(c) : 0,
                Cost = Convert.ToDecimal(powerValue) * 0.12m / 1000m, // Simple cost calculation
                Timestamp = request.Timestamp
            };
            
            _context.EnergyReadings.Add(energyReading);
        }
        
        await _context.SaveChangesAsync();
        
        _logger.LogDebug("Telemetry processed for device {DeviceId}", request.DeviceId);
    }

    public async Task<IEnumerable<EnergyReading>> GetEnergyDataAsync(string deviceId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.EnergyReadings.Where(e => e.DeviceId == deviceId);
        
        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);
        
        return await query
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
    }
}
