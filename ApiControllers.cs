using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NexusHome.Models;
using NexusHome.Data;
using NexusHome.Services;
using NexusHome.IoT;
using NexusHome.AI;
using NexusHome.Energy;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace NexusHome.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EnergyController : ControllerBase
    {
        private readonly NexusHomeDbContext _context;
        private readonly IEnergyService _energyService;
        private readonly IEnergyOptimizationService _optimizationService;
        private readonly IPredictiveMaintenanceService _predictiveService;
        private readonly IHubContext<EnergyMonitoringHub> _energyHubContext;
        private readonly ILogger<EnergyController> _logger;

        public EnergyController(
            NexusHomeDbContext context,
            IEnergyService energyService,
            IEnergyOptimizationService optimizationService,
            IPredictiveMaintenanceService predictiveService,
            IHubContext<EnergyMonitoringHub> energyHubContext,
            ILogger<EnergyController> logger)
        {
            _context = context;
            _energyService = energyService;
            _optimizationService = optimizationService;
            _predictiveService = predictiveService;
            _energyHubContext = energyHubContext;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<EnergyDashboardDto>> GetEnergyDashboard()
        {
            try
            {
                var dashboard = await _energyService.GetEnergyDashboardAsync();
                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting energy dashboard");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("consumption")]
        public async Task<ActionResult<List<EnergyConsumptionDto>>> GetEnergyConsumption(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? deviceId = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow.AddDays(-7);
                endDate ??= DateTime.UtcNow;

                var consumption = await _energyService.GetEnergyConsumptionAsync(startDate.Value, endDate.Value, deviceId);
                return Ok(consumption);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting energy consumption data");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("consumption/real-time")]
        public async Task<ActionResult<object>> GetRealTimeConsumption()
        {
            try
            {
                var realTimeData = await _energyService.GetRealTimeEnergyDataAsync();
                return Ok(realTimeData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting real-time energy data");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("forecast")]
        public async Task<ActionResult<EnergyForecast>> GetEnergyForecast(
            [FromQuery] DateTime startDate,
            [FromQuery] int forecastDays = 7)
        {
            try
            {
                var forecast = await _optimizationService.ForecastEnergyDemandAsync(startDate, forecastDays);
                return Ok(forecast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating energy forecast");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("optimization")]
        public async Task<ActionResult<OptimizationResult>> GetEnergyOptimization(
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null)
        {
            try
            {
                startTime ??= DateTime.UtcNow;
                endTime ??= DateTime.UtcNow.AddHours(24);

                var optimization = await _optimizationService.OptimizeEnergyUsageAsync(startTime.Value, endTime.Value);
                return Ok(optimization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting energy optimization");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("optimization/execute")]
        public async Task<ActionResult> ExecuteOptimizationPlan([FromBody] OptimizationPlan plan)
        {
            try
            {
                await _optimizationService.ExecuteOptimizationPlanAsync(plan);
                return Ok(new { message = "Optimization plan executed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing optimization plan");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("load-shifting")]
        public async Task<ActionResult<LoadShiftingRecommendation>> GetLoadShiftingRecommendations()
        {
            try
            {
                var recommendations = await _optimizationService.GenerateLoadShiftingRecommendationsAsync();
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting load shifting recommendations");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("battery-optimization")]
        public async Task<ActionResult<BatteryOptimizationPlan>> GetBatteryOptimization()
        {
            try
            {
                var optimization = await _optimizationService.OptimizeBatteryUsageAsync();
                return Ok(optimization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting battery optimization");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("solar-optimization")]
        public async Task<ActionResult<SolarOptimizationPlan>> GetSolarOptimization()
        {
            try
            {
                var optimization = await _optimizationService.OptimizeSolarEnergyUsageAsync();
                return Ok(optimization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting solar optimization");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("cost-optimization")]
        public async Task<ActionResult<CostOptimizationResult>> GetCostOptimization()
        {
            try
            {
                var optimization = await _optimizationService.OptimizeEnergyCostsAsync();
                return Ok(optimization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost optimization");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("demand-response")]
        public async Task<ActionResult<DemandResponseResult>> HandleDemandResponse([FromBody] DemandResponseEvent demandEvent)
        {
            try
            {
                var result = await _optimizationService.HandleDemandResponseEventAsync(demandEvent);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling demand response event");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("alerts")]
        public async Task<ActionResult<List<DeviceAlert>>> GetEnergyAlerts()
        {
            try
            {
                var alerts = await _context.DeviceAlerts
                    .Where(a => a.Type == AlertType.EnergyAnomaliy || a.Type == AlertType.HighConsumption)
                    .Where(a => !a.IsResolved)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(50)
                    .ToListAsync();

                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting energy alerts");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("alerts/{alertId}/resolve")]
        public async Task<ActionResult> ResolveAlert(int alertId, [FromBody] string resolutionNotes = "")
        {
            try
            {
                var alert = await _context.DeviceAlerts.FindAsync(alertId);
                if (alert == null)
                {
                    return NotFound("Alert not found");
                }

                alert.IsResolved = true;
                alert.ResolvedAt = DateTime.UtcNow;
                alert.ResolutionNotes = resolutionNotes;

                await _context.SaveChangesAsync();

                await _energyHubContext.Clients.All.SendAsync("AlertResolved", alert);

                return Ok(new { message = "Alert resolved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly NexusHomeDbContext _context;
        private readonly IDeviceService _deviceService;
        private readonly IMqttService _mqttService;
        private readonly IMatterService _matterService;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(
            NexusHomeDbContext context,
            IDeviceService deviceService,
            IMqttService mqttService,
            IMatterService matterService,
            ILogger<DevicesController> logger)
        {
            _context = context;
            _deviceService = deviceService;
            _mqttService = mqttService;
            _matterService = matterService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<DeviceDto>>> GetDevices()
        {
            try
            {
                var devices = await _deviceService.GetAllDevicesAsync();
                return Ok(devices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting devices");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DeviceDto>> GetDevice(int id)
        {
            try
            {
                var device = await _deviceService.GetDeviceByIdAsync(id);
                if (device == null)
                {
                    return NotFound("Device not found");
                }

                return Ok(device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device {DeviceId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<DeviceDto>> CreateDevice([FromBody] CreateDeviceRequest request)
        {
            try
            {
                var device = new Device
                {
                    DeviceId = request.DeviceId,
                    Name = request.Name,
                    Description = request.Description,
                    Type = request.Type,
                    Manufacturer = request.Manufacturer,
                    Model = request.Model,
                    FirmwareVersion = request.FirmwareVersion,
                    Protocol = request.Protocol,
                    Status = DeviceStatus.Inactive,
                    Location = request.Location,
                    Room = request.Room,
                    PowerRating = request.PowerRating,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Configuration = request.Configuration
                };

                _context.Devices.Add(device);
                await _context.SaveChangesAsync();

                var deviceDto = await _deviceService.GetDeviceByIdAsync(device.Id);
                return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, deviceDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating device");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<DeviceDto>> UpdateDevice(int id, [FromBody] UpdateDeviceRequest request)
        {
            try
            {
                var device = await _context.Devices.FindAsync(id);
                if (device == null)
                {
                    return NotFound("Device not found");
                }

                device.Name = request.Name ?? device.Name;
                device.Description = request.Description ?? device.Description;
                device.Location = request.Location ?? device.Location;
                device.Room = request.Room ?? device.Room;
                device.Configuration = request.Configuration ?? device.Configuration;
                device.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var deviceDto = await _deviceService.GetDeviceByIdAsync(device.Id);
                return Ok(deviceDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device {DeviceId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteDevice(int id)
        {
            try
            {
                var device = await _context.Devices.FindAsync(id);
                if (device == null)
                {
                    return NotFound("Device not found");
                }

                _context.Devices.Remove(device);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Device deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting device {DeviceId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/command")]
        public async Task<ActionResult> SendDeviceCommand(int id, [FromBody] DeviceCommand command)
        {
            try
            {
                var device = await _context.Devices.FindAsync(id);
                if (device == null)
                {
                    return NotFound("Device not found");
                }

                await _mqttService.SendDeviceCommandAsync(device.DeviceId, command);

                return Ok(new { message = "Command sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending command to device {DeviceId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("matter/discover")]
        public async Task<ActionResult<List<MatterDevice>>> DiscoverMatterDevices()
        {
            try
            {
                var devices = await _matterService.DiscoverDevicesAsync();
                return Ok(devices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering Matter devices");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("matter/commission")]
        public async Task<ActionResult<MatterCommissioningResult>> CommissionMatterDevice([FromBody] MatterCommissionRequest request)
        {
            try
            {
                var result = await _matterService.CommissionDeviceAsync(request.SetupCode, request.Discriminator);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error commissioning Matter device");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/energy-consumption")]
        public async Task<ActionResult<List<EnergyConsumptionDto>>> GetDeviceEnergyConsumption(
            int id,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow.AddDays(-7);
                endDate ??= DateTime.UtcNow;

                var consumption = await _context.EnergyConsumptions
                    .Where(e => e.DeviceId == id)
                    .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .OrderBy(e => e.Timestamp)
                    .Select(e => new EnergyConsumptionDto
                    {
                        Id = e.Id,
                        DeviceId = e.DeviceId,
                        DeviceName = e.Device.Name,
                        PowerConsumption = e.PowerConsumption,
                        Cost = e.Cost,
                        Timestamp = e.Timestamp,
                        Source = e.Source
                    })
                    .ToListAsync();

                return Ok(consumption);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting energy consumption for device {DeviceId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/status")]
        public async Task<ActionResult<object>> GetDeviceStatus(int id)
        {
            try
            {
                var device = await _context.Devices.FindAsync(id);
                if (device == null)
                {
                    return NotFound("Device not found");
                }

                // Get latest status information
                var status = new
                {
                    device.Id,
                    device.DeviceId,
                    device.Name,
                    device.Status,
                    device.IsOnline,
                    device.LastSeen,
                    device.CurrentPowerConsumption,
                    Configuration = device.Configuration != null ? 
                        System.Text.Json.JsonSerializer.Deserialize<object>(device.Configuration) : null,
                    HealthScore = await CalculateDeviceHealthScore(device),
                    RecentAlerts = await GetRecentDeviceAlerts(device.Id)
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device status for {DeviceId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<decimal> CalculateDeviceHealthScore(Device device)
        {
            // Simple health score calculation based on uptime and alerts
            var daysSinceLastSeen = (DateTime.UtcNow - device.LastSeen).TotalDays;
            var recentAlertCount = await _context.DeviceAlerts
                .Where(a => a.DeviceId == device.Id)
                .Where(a => a.Timestamp >= DateTime.UtcNow.AddDays(-7))
                .CountAsync();

            var baseScore = device.IsOnline ? 100 : 0;
            var uptimeScore = Math.Max(0, 100 - (daysSinceLastSeen * 5));
            var alertPenalty = recentAlertCount * 10;

            return Math.Max(0, Math.Min(100, (decimal)(baseScore * 0.5 + uptimeScore * 0.4 - alertPenalty * 0.1)));
        }

        private async Task<List<DeviceAlert>> GetRecentDeviceAlerts(int deviceId)
        {
            return await _context.DeviceAlerts
                .Where(a => a.DeviceId == deviceId)
                .Where(a => a.Timestamp >= DateTime.UtcNow.AddDays(-7))
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToListAsync();
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MaintenanceController : ControllerBase
    {
        private readonly NexusHomeDbContext _context;
        private readonly IPredictiveMaintenanceService _predictiveService;
        private readonly ILogger<MaintenanceController> _logger;

        public MaintenanceController(
            NexusHomeDbContext context,
            IPredictiveMaintenanceService predictiveService,
            ILogger<MaintenanceController> logger)
        {
            _context = context;
            _predictiveService = predictiveService;
            _logger = logger;
        }

        [HttpGet("predictions")]
        public async Task<ActionResult<List<MaintenancePrediction>>> GetMaintenancePredictions(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow;
                endDate ??= DateTime.UtcNow.AddDays(30);

                var predictions = await _predictiveService.GetMaintenancePredictionsAsync(startDate.Value, endDate.Value);
                return Ok(predictions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting maintenance predictions");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("predictions/{deviceId}")]
        public async Task<ActionResult<MaintenancePrediction>> GetDeviceMaintenancePrediction(int deviceId)
        {
            try
            {
                var prediction = await _predictiveService.PredictMaintenanceNeedAsync(deviceId);
                return Ok(prediction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting maintenance prediction for device {DeviceId}", deviceId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("records")]
        public async Task<ActionResult<List<DeviceMaintenanceRecord>>> GetMaintenanceRecords(
            [FromQuery] int? deviceId = null,
            [FromQuery] MaintenanceStatus? status = null)
        {
            try
            {
                var query = _context.MaintenanceRecords.AsQueryable();

                if (deviceId.HasValue)
                {
                    query = query.Where(m => m.DeviceId == deviceId.Value);
                }

                if (status.HasValue)
                {
                    query = query.Where(m => m.Status == status.Value);
                }

                var records = await query
                    .Include(m => m.Device)
                    .OrderByDescending(m => m.ScheduledDate)
                    .ToListAsync();

                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting maintenance records");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("records")]
        public async Task<ActionResult<DeviceMaintenanceRecord>> CreateMaintenanceRecord([FromBody] CreateMaintenanceRecordRequest request)
        {
            try
            {
                var record = new DeviceMaintenanceRecord
                {
                    DeviceId = request.DeviceId,
                    Type = request.Type,
                    Title = request.Title,
                    Description = request.Description,
                    Status = MaintenanceStatus.Scheduled,
                    Priority = request.Priority,
                    ScheduledDate = request.ScheduledDate,
                    Cost = request.EstimatedCost ?? 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MaintenanceRecords.Add(record);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetMaintenanceRecord), new { id = record.Id }, record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating maintenance record");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("records/{id}")]
        public async Task<ActionResult<DeviceMaintenanceRecord>> GetMaintenanceRecord(int id)
        {
            try
            {
                var record = await _context.MaintenanceRecords
                    .Include(m => m.Device)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (record == null)
                {
                    return NotFound("Maintenance record not found");
                }

                return Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting maintenance record {RecordId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("records/{id}")]
        public async Task<ActionResult<DeviceMaintenanceRecord>> UpdateMaintenanceRecord(int id, [FromBody] UpdateMaintenanceRecordRequest request)
        {
            try
            {
                var record = await _context.MaintenanceRecords.FindAsync(id);
                if (record == null)
                {
                    return NotFound("Maintenance record not found");
                }

                record.Status = request.Status ?? record.Status;
                record.CompletedDate = request.CompletedDate ?? record.CompletedDate;
                record.Cost = request.ActualCost ?? record.Cost;
                record.TechnicianName = request.TechnicianName ?? record.TechnicianName;
                record.Notes = request.Notes ?? record.Notes;

                await _context.SaveChangesAsync();

                return Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating maintenance record {RecordId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("anomaly-detection/{deviceId}")]
        public async Task<ActionResult<AnomalyDetectionResult>> DetectAnomalies(int deviceId, [FromBody] List<decimal> values)
        {
            try
            {
                var result = await _predictiveService.DetectAnomaliesAsync(deviceId, values);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting anomalies for device {DeviceId}", deviceId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("models/train")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> TrainPredictiveModels()
        {
            try
            {
                await _predictiveService.TrainModelsAsync();
                return Ok(new { message = "Model training initiated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error training predictive models");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("feedback")]
        public async Task<ActionResult> SubmitMaintenanceFeedback([FromBody] MaintenanceFeedbackRequest request)
        {
            try
            {
                var feedback = new MaintenanceFeedback
                {
                    FeedbackType = request.FeedbackType,
                    PredictedFailureProbability = request.PredictedFailureProbability,
                    ActualOutcome = request.ActualOutcome,
                    Notes = request.Notes
                };

                await _predictiveService.UpdateModelAsync(request.DeviceId, feedback);

                return Ok(new { message = "Feedback submitted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting maintenance feedback");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // Request/Response DTOs
    public class CreateDeviceRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DeviceType Type { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public DeviceProtocol Protocol { get; set; }
        public string? Location { get; set; }
        public string? Room { get; set; }
        public decimal PowerRating { get; set; }
        public string? Configuration { get; set; }
    }

    public class UpdateDeviceRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public string? Room { get; set; }
        public string? Configuration { get; set; }
    }

    public class DeviceCommand
    {
        public string Command { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class MatterCommissionRequest
    {
        public string SetupCode { get; set; } = string.Empty;
        public string Discriminator { get; set; } = string.Empty;
    }

    public class CreateMaintenanceRecordRequest
    {
        public int DeviceId { get; set; }
        public MaintenanceType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public MaintenancePriority Priority { get; set; }
        public DateTime ScheduledDate { get; set; }
        public decimal? EstimatedCost { get; set; }
    }

    public class UpdateMaintenanceRecordRequest
    {
        public MaintenanceStatus? Status { get; set; }
        public DateTime? CompletedDate { get; set; }
        public decimal? ActualCost { get; set; }
        public string? TechnicianName { get; set; }
        public string? Notes { get; set; }
    }

    public class MaintenanceFeedbackRequest
    {
        public int DeviceId { get; set; }
        public string FeedbackType { get; set; } = string.Empty;
        public double PredictedFailureProbability { get; set; }
        public string ActualOutcome { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    // SignalR Hubs for real-time communication
    public class EnergyMonitoringHub : Hub
    {
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "EnergyMonitoring");
            await base.OnConnectedAsync();
        }
    }

    public class DeviceStatusHub : Hub
    {
        public async Task JoinDeviceGroup(string deviceId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Device_{deviceId}");
        }

        public async Task LeaveDeviceGroup(string deviceId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Device_{deviceId}");
        }
    }

    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }
            await base.OnConnectedAsync();
        }
    }

    // Additional service interfaces (simplified for this example)
    public interface IEnergyService
    {
        Task<EnergyDashboardDto> GetEnergyDashboardAsync();
        Task<List<EnergyConsumptionDto>> GetEnergyConsumptionAsync(DateTime startDate, DateTime endDate, int? deviceId = null);
        Task<object> GetRealTimeEnergyDataAsync();
    }

    public interface IDeviceService
    {
        Task<List<DeviceDto>> GetAllDevicesAsync();
        Task<DeviceDto?> GetDeviceByIdAsync(int id);
    }

    public interface INotificationService
    {
        Task SendNotificationAsync(string userId, string title, string message);
        Task SendBroadcastNotificationAsync(string title, string message);
    }

    public interface IAutomationService
    {
        Task<List<AutomationRule>> GetActiveRulesAsync();
        Task ExecuteRuleAsync(int ruleId);
        Task<bool> EvaluateRuleConditionsAsync(AutomationRule rule);
    }

    public interface ISolarPanelService
    {
        Task<List<SolarGeneration>> GetSolarGenerationDataAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetCurrentSolarGenerationAsync();
        Task<SolarOptimizationPlan> OptimizeSolarUsageAsync();
    }

    // Placeholder classes for missing types
    public class OptimizationPlan
    {
        public string Name { get; set; } = string.Empty;
        public List<OptimizationAction> Actions { get; set; } = new();
        public OptimizationPlanStatus ExecutionStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExecutedAt { get; set; }
    }

    public class OptimizationAction
    {
        public string ActionId { get; set; } = string.Empty;
        public OptimizationActionType ActionType { get; set; }
        public int? DeviceId { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public int ExecutionOrder { get; set; }
        public OptimizationActionStatus ExecutionStatus { get; set; }
        public DateTime? ExecutionTimestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class DemandResponseEvent
    {
        public string EventId { get; set; } = string.Empty;
        public DemandResponseEventType EventType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal TargetReduction { get; set; }
    }

    public enum DemandResponseEventType
    {
        PeakShaving,
        LoadReduction,
        FrequencyRegulation,
        EmergencyResponse
    }

    // Additional placeholder classes would be defined here...
}