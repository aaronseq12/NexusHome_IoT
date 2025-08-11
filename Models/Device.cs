// File: NexusHome.Api/Models/Device.cs
// Purpose: Defines the data model for a smart device.

using System.ComponentModel.DataAnnotations;

namespace NexusHome.Api.Models;

public enum DeviceType
{
    Light,
    Thermostat,
    MotionSensor
}

public class Device
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DeviceType Type { get; set; }

    [Required]
    public string Room { get; set; } = string.Empty;

    // Represents the current state, e.g., "On"/"Off", temperature value, or "Detected"
    public string CurrentState { get; set; } = "Off";

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
```csharp
// File: NexusHome.Api/Models/TelemetryData.cs
// Purpose: Represents a single piece of telemetry data received from a device.

using System.Text.Json.Serialization;

namespace NexusHome.Api.Models;

public class TelemetryData
{
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty; // e.g., "21.5", "On", "true"
}
```csharp
// File: NexusHome.Api/Models/AutomationRule.cs
// Purpose: Defines the model for a user-created automation rule.

using System.ComponentModel.DataAnnotations;

namespace NexusHome.Api.Models;

public enum Operator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan
}

public class AutomationRule
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    // Trigger
    [Required]
    public int TriggerDeviceId { get; set; }
    public Device? TriggerDevice { get; set; }

    [Required]
    public string TriggerValue { get; set; } = string.Empty; // e.g., "On", "25"

    [Required]
    public Operator TriggerOperator { get; set; }

    // Action
    [Required]
    public int ActionDeviceId { get; set; }
    public Device? ActionDevice { get; set; }

    [Required]
    public string ActionValue { get; set; } = string.Empty; // e.g., "On", "Off"

    public bool IsEnabled { get; set; } = true;
}
```csharp
// File: NexusHome.Api/Data/AppDbContext.cs
// Purpose: Entity Framework Core database context.

using Microsoft.EntityFrameworkCore;
using NexusHome.Api.Models;

namespace NexusHome.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices { get; set; }
    public DbSet<AutomationRule> AutomationRules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed initial device data for simulation
        modelBuilder.Entity<Device>().HasData(
            new Device { Id = 1, Name = "LivingRoomLight", Type = DeviceType.Light, Room = "Living Room", CurrentState = "Off" },
            new Device { Id = 2, Name = "LivingRoomThermostat", Type = DeviceType.Thermostat, Room = "Living Room", CurrentState = "21.0" },
            new Device { Id = 3, Name = "HallwayMotionSensor", Type = DeviceType.MotionSensor, Room = "Hallway", CurrentState = "false" },
            new Device { Id = 4, Name = "BedroomLight", Type = DeviceType.Light, Room = "Bedroom", CurrentState = "Off" }
        );
    }
}
```csharp
// File: NexusHome.Api/Hubs/TelemetryHub.cs
// Purpose: SignalR hub for real-time communication with clients.

using Microsoft.AspNetCore.SignalR;

namespace NexusHome.Api.Hubs;

public class TelemetryHub : Hub
{
    // Clients can call this method, but we don't need it for this project.
    // The server will push data to clients.
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
```csharp
// File: NexusHome.Api/Services/DeviceService.cs
// Purpose: Contains business logic for managing devices.

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NexusHome.Api.Data;
using NexusHome.Api.Hubs;
using NexusHome.Api.Models;

namespace NexusHome.Api.Services;

public class DeviceService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(AppDbContext context, IHubContext<TelemetryHub> hubContext, ILogger<DeviceService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<Device?> UpdateDeviceStateAsync(string deviceName, string newState)
    {
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.Name == deviceName);
        if (device == null)
        {
            _logger.LogWarning("Device not found: {DeviceName}", deviceName);
            return null;
        }

        device.CurrentState = newState;
        device.LastUpdated = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Device '{DeviceName}' state updated to '{NewState}'. Broadcasting change.", deviceName, newState);

        // Broadcast the update to all connected SignalR clients
        await _hubContext.Clients.All.SendAsync("ReceiveDeviceUpdate", device);

        return device;
    }
}
```csharp
// File: NexusHome.Api/Services/RulesEngineService.cs
// Purpose: Background service to evaluate automation rules.

using Microsoft.EntityFrameworkCore;
using NexusHome.Api.Data;
using NexusHome.Api.Models;

namespace NexusHome.Api.Services;

public class RulesEngineService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RulesEngineService> _logger;

    public RulesEngineService(IServiceProvider serviceProvider, ILogger<RulesEngineService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rules Engine Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var deviceService = scope.ServiceProvider.GetRequiredService<DeviceService>();
                    
                    var rules = await dbContext.AutomationRules
                        .Include(r => r.TriggerDevice)
                        .Include(r => r.ActionDevice) // Eager load the ActionDevice
                        .Where(r => r.IsEnabled)
                        .ToListAsync(stoppingToken);

                    foreach (var rule in rules)
                    {
                        if (rule.TriggerDevice == null || rule.ActionDevice == null) continue;

                        bool triggerMet = EvaluateTrigger(rule.TriggerDevice.CurrentState, rule.TriggerOperator, rule.TriggerValue);

                        // Prevent rule from re-triggering itself constantly
                        bool actionAlreadyApplied = rule.ActionDevice.CurrentState.Equals(rule.ActionValue, StringComparison.OrdinalIgnoreCase);

                        if (triggerMet && !actionAlreadyApplied)
                        {
                            _logger.LogInformation("Rule '{RuleDescription}' triggered. Executing action.", rule.Description);
                            await deviceService.UpdateDeviceStateAsync(rule.ActionDevice.Name, rule.ActionValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in the Rules Engine Service.");
            }

            // This is a simple implementation. A real-world scenario would use
            // event-driven logic instead of polling.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private bool EvaluateTrigger(string currentState, Operator op, string triggerValue)
    {
        // Try parsing as doubles for numerical comparison
        if (double.TryParse(currentState, out double currentVal) && double.TryParse(triggerValue, out double triggerVal))
        {
            return op switch
            {
                Operator.Equals => Math.Abs(currentVal - triggerVal) < 0.01,
                Operator.NotEquals => Math.Abs(currentVal - triggerVal) > 0.01,
                Operator.GreaterThan => currentVal > triggerVal,
                Operator.LessThan => currentVal < triggerVal,
                _ => false
            };
        }

        // Fallback to string comparison for "On"/"Off" or "true"/"false"
        return op switch
        {
            Operator.Equals => currentState.Equals(triggerValue, StringComparison.OrdinalIgnoreCase),
            Operator.NotEquals => !currentState.Equals(triggerValue, StringComparison.OrdinalIgnoreCase),
            _ => false // Numerical operators are not supported for non-numeric strings
        };
    }
}
```csharp
// File: NexusHome.Api/Controllers/TelemetryController.cs
// Purpose: API endpoint for ingesting device data.

using Microsoft.AspNetCore.Mvc;
using NexusHome.Api.Models;
using NexusHome.Api.Services;

namespace NexusHome.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly DeviceService _deviceService;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(DeviceService deviceService, ILogger<TelemetryController> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] TelemetryData data)
    {
        if (data == null || string.IsNullOrEmpty(data.DeviceName))
        {
            return BadRequest("Invalid telemetry data.");
        }

        _logger.LogInformation("Received telemetry from '{DeviceName}' with value '{Value}'", data.DeviceName, data.Value);

        var updatedDevice = await _deviceService.UpdateDeviceStateAsync(data.DeviceName, data.Value);

        if (updatedDevice == null)
        {
            return NotFound($"Device with name '{data.DeviceName}' not found.");
        }

        return Ok(updatedDevice);
    }
}
```csharp
// File: NexusHome.Api/Controllers/DevicesController.cs
// Purpose: API endpoints for managing devices.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusHome.Api.Data;
using NexusHome.Api.Models;
using NexusHome.Api.Services;

namespace NexusHome.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly DeviceService _deviceService;

    public DevicesController(AppDbContext context, DeviceService deviceService)
    {
        _context = context;
        _deviceService = deviceService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Device>>> GetDevices()
    {
        return await _context.Devices.ToListAsync();
    }

    [HttpPost("{id}/state")]
    public async Task<IActionResult> UpdateDeviceState(int id, [FromBody] string newState)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        await _deviceService.UpdateDeviceStateAsync(device.Name, newState);
        return NoContent();
    }
}
```csharp
// File: NexusHome.Api/Controllers/RulesController.cs
// Purpose: API endpoints for managing automation rules.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusHome.Api.Data;
using NexusHome.Api.Models;

namespace NexusHome.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RulesController : ControllerBase
{
    private readonly AppDbContext _context;

    public RulesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AutomationRule>>> GetRules()
    {
        return await _context.AutomationRules
            .Include(r => r.TriggerDevice)
            .Include(r => r.ActionDevice)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<AutomationRule>> PostRule(AutomationRule rule)
    {
        // Clear navigation properties to avoid issues with entity tracking
        rule.TriggerDevice = null;
        rule.ActionDevice = null;

        _context.AutomationRules.Add(rule);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRules), new { id = rule.Id }, rule);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var rule = await _context.AutomationRules.FindAsync(id);
        if (rule == null)
        {
            return NotFound();
        }

        _context.AutomationRules.Remove(rule);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
```csharp
// File: NexusHome.Api/appsettings.json
// Purpose: Configuration for the API.
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=nexushome_dev;Username=nexushome;Password=yoursecurepassword"
  }
}
```csharp
// File: NexusHome.Api/Program.cs
// Purpose: Main entry point and service configuration for the API.

using Microsoft.EntityFrameworkCore;
using NexusHome.Api.Data;
using NexusHome.Api.Hubs;
using NexusHome.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Add services to the container ---

// 1. Database Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Controllers and API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. SignalR
builder.Services.AddSignalR();

// 4. Custom Application Services
builder.Services.AddScoped<DeviceService>();
builder.Services.AddHostedService<RulesEngineService>();

// 5. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy =>
        {
            // IMPORTANT: Update with your Blazor app's actual URL from its launchSettings.json
            policy.WithOrigins("https://localhost:7289", "http://localhost:5213") 
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});


var app = builder.Build();

// --- Configure the HTTP request pipeline ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

// Use CORS policy
app.UseCors("AllowWebApp");

app.UseAuthorization();

app.MapControllers();
app.MapHub<TelemetryHub>("/telemetryHub"); // Map SignalR hub endpoint

app.Run();
