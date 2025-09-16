using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NexusHome.IoT.Models;
using NexusHome.IoT.Data;
using NexusHome.IoT.Services;
using NexusHome.IoT.AI;
using NexusHome.IoT.Energy;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using NexusHome.IoT.Hubs;
using NexusHome.IoT.DTOs;

namespace NexusHome.IoT.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EnergyController : ControllerBase
    {
        private readonly IEnergyService _energyService;
        private readonly IEnergyOptimizationService _optimizationService;
        private readonly IHubContext<EnergyMonitoringHub> _energyHubContext;
        private readonly ILogger<EnergyController> _logger;

        public EnergyController(
            IEnergyService energyService,
            IEnergyOptimizationService optimizationService,
            IHubContext<EnergyMonitoringHub> energyHubContext,
            ILogger<EnergyController> logger)
        {
            _energyService = energyService;
            _optimizationService = optimizationService;
            _energyHubContext = energyHubContext;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(EnergyDashboardDto), 200)]
        public async Task<IActionResult> GetEnergyDashboard()
        {
            _logger.LogInformation("Fetching energy dashboard data.");
            var dashboard = await _energyService.GetEnergyDashboardAsync();
            return Ok(dashboard);
        }

        [HttpGet("consumption")]
        [ProducesResponseType(typeof(List<EnergyConsumptionDto>), 200)]
        public async Task<IActionResult> GetEnergyConsumption(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? deviceId)
        {
            _logger.LogInformation("Fetching energy consumption data for device {DeviceId} from {StartDate} to {EndDate}", deviceId, startDate, endDate);
            var consumption = await _energyService.GetEnergyConsumptionAsync(startDate, endDate, deviceId);
            return Ok(consumption);
        }

        [HttpGet("forecast")]
        [ProducesResponseType(typeof(EnergyForecast), 200)]
        public async Task<IActionResult> GetEnergyForecast([FromQuery] int forecastDays = 7)
        {
             _logger.LogInformation("Generating energy forecast for {ForecastDays} days.", forecastDays);
            var forecast = await _optimizationService.ForecastEnergyDemandAsync(DateTime.UtcNow, forecastDays);
            return Ok(forecast);
        }

        [HttpGet("optimization-plan")]
        [ProducesResponseType(typeof(OptimizationResult), 200)]
        public async Task<IActionResult> GetEnergyOptimizationPlan()
        {
            _logger.LogInformation("Generating a new energy optimization plan.");
            var optimization = await _optimizationService.OptimizeEnergyUsageAsync(DateTime.UtcNow, DateTime.UtcNow.AddHours(24));
            return Ok(optimization);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _deviceService;
        private readonly IMqttService _mqttService;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(
            IDeviceService deviceService,
            IMqttService mqttService,
            ILogger<DevicesController> logger)
        {
            _deviceService = deviceService;
            _mqttService = mqttService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<DeviceDto>), 200)]
        public async Task<IActionResult> GetAllDevices()
        {
            _logger.LogInformation("Fetching all devices.");
            var devices = await _deviceService.GetAllDevicesAsync();
            return Ok(devices);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DeviceDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetDeviceById(int id)
        {
            _logger.LogInformation("Fetching device with ID: {DeviceId}", id);
            var device = await _deviceService.GetDeviceByIdAsync(id);
            if (device == null)
            {
                _logger.LogWarning("Device with ID: {DeviceId} not found.", id);
                return NotFound();
            }
            return Ok(device);
        }

        [HttpPost("{id}/command")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> SendDeviceCommand(int id, [FromBody] DeviceCommand command)
        {
            _logger.LogInformation("Sending command '{CommandName}' to device ID: {DeviceId}", command.Command, id);
            var device = await _deviceService.GetDeviceByIdAsync(id);
            if (device == null)
            {
                _logger.LogWarning("Command failed: Device with ID: {DeviceId} not found.", id);
                return NotFound("Device not found");
            }

            await _mqttService.SendDeviceCommandAsync(device.DeviceId, command);
            return Ok(new { message = "Command sent successfully." });
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MaintenanceController : ControllerBase
    {
        private readonly IPredictiveMaintenanceService _predictiveService;
        private readonly ILogger<MaintenanceController> _logger;

        public MaintenanceController(
            IPredictiveMaintenanceService predictiveService,
            ILogger<MaintenanceController> logger)
        {
            _predictiveService = predictiveService;
            _logger = logger;
        }

        [HttpGet("predictions")]
        [ProducesResponseType(typeof(List<MaintenancePrediction>), 200)]
        public async Task<IActionResult> GetMaintenancePredictions()
        {
            _logger.LogInformation("Fetching all maintenance predictions.");
            var predictions = await _predictiveService.GetMaintenancePredictionsAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            return Ok(predictions);
        }

        [HttpGet("predictions/{deviceId}")]
        [ProducesResponseType(typeof(MaintenancePrediction), 200)]
        public async Task<IActionResult> GetDeviceMaintenancePrediction(int deviceId)
        {
            _logger.LogInformation("Fetching maintenance prediction for device ID: {DeviceId}", deviceId);
            var prediction = await _predictiveService.PredictMaintenanceNeedAsync(deviceId);
            return Ok(prediction);
        }
    }
}
