using Microsoft.AspNetCore.Mvc;
using NexusHome.IoT.Core.Domain;
using NexusHome.IoT.Core.Services.Interfaces;

namespace NexusHome.IoT.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly ISmartDeviceManager _deviceManager;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(ISmartDeviceManager deviceManager, ILogger<DevicesController> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all smart devices
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceStatusDto>>> GetAllDevices()
    {
        try
        {
            var devices = await _deviceManager.GetAllDevicesAsync();
            var deviceDtos = devices.Select(d => new DeviceStatusDto(
                d.DeviceId,
                d.Name,
                d.DeviceType,
                d.IsOnline,
                d.LastSeen,
                string.IsNullOrEmpty(d.Properties) ? null : 
                    System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(d.Properties)
            ));

            return Ok(deviceDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving devices");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get device by ID
    /// </summary>
    [HttpGet("{deviceId}")]
    public async Task<ActionResult<DeviceStatusDto>> GetDevice(string deviceId)
    {
        try
        {
            var device = await _deviceManager.GetDeviceByIdAsync(deviceId);
            if (device == null)
                return NotFound($"Device {deviceId} not found");

            var deviceDto = new DeviceStatusDto(
                device.DeviceId,
                device.Name,
                device.DeviceType,
                device.IsOnline,
                device.LastSeen,
                string.IsNullOrEmpty(device.Properties) ? null :
                    System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(device.Properties)
            );

            return Ok(deviceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving device {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Toggle device state
    /// </summary>
    [HttpPost("{deviceId}/toggle")]
    public async Task<ActionResult> ToggleDevice(string deviceId)
    {
        try
        {
            var result = await _deviceManager.ToggleDeviceAsync(deviceId);
            if (!result)
                return NotFound($"Device {deviceId} not found");

            return Ok(new { Message = "Device toggle command sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling device {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Submit device telemetry data
    /// </summary>
    [HttpPost("telemetry")]
    public async Task<ActionResult> SubmitTelemetry([FromBody] DeviceTelemetryRequest request)
    {
        try
        {
            await _deviceManager.ProcessTelemetryDataAsync(request);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry for device {DeviceId}", request.DeviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get energy data for a device
    /// </summary>
    [HttpGet("{deviceId}/energy")]
    public async Task<ActionResult<IEnumerable<EnergyDataDto>>> GetEnergyData(
        string deviceId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var energyData = await _deviceManager.GetEnergyDataAsync(deviceId, from, to);
            var energyDtos = energyData.Select(e => new EnergyDataDto(
                e.DeviceId,
                e.PowerConsumption,
                e.Cost,
                e.Timestamp
            ));

            return Ok(energyDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving energy data for device {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }
}
