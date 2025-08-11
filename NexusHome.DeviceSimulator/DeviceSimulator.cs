// File: NexusHome.DeviceSimulator/DeviceSimulator.cs
// Purpose: Simulates a single smart device.

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NexusHome.DeviceSimulator;

public class TelemetryData
{
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class DeviceSimulator
{
    private readonly HttpClient _httpClient;
    private readonly string _deviceName;
    private readonly Func<string> _generateValue;
    private readonly TimeSpan _interval;
    private readonly ILogger<DeviceSimulator> _logger;

    public DeviceSimulator(string deviceName, Func<string> generateValue, TimeSpan interval, ILogger<DeviceSimulator> logger, string apiBaseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _deviceName = deviceName;
        _generateValue = generateValue;
        _interval = interval;
        _logger = logger;
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting simulator for device: {DeviceName}", _deviceName);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                string value = _generateValue();
                var data = new TelemetryData { DeviceName = _deviceName, Value = value };

                var response = await _httpClient.PostAsJsonAsync("/api/telemetry", data, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent data for {DeviceName}: {Value}", _deviceName, value);
                }
                else
                {
                    _logger.LogError("Failed to send data for {DeviceName}. Status: {StatusCode}", _deviceName, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simulator for {DeviceName}", _deviceName);
            }

            await Task.Delay(_interval, cancellationToken);
        }
    }
}
```csharp
// File: NexusHome.DeviceSimulator/Program.cs
// Purpose: Main entry point for the device simulator.

using Microsoft.Extensions.Logging;
using NexusHome.DeviceSimulator;

// --- Configuration ---
// IMPORTANT: Update this URL to match the one your API is running on.
// Check the launchSettings.json in the API project.
const string ApiBaseUrl = "https://localhost:7123"; 

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Device Simulator is starting.");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var random = new Random();

// --- Create Device Simulators ---
var devices = new List<DeviceSimulator>
{
    // Simulates a thermostat, sending a temperature value every 10 seconds.
    new DeviceSimulator(
        "LivingRoomThermostat",
        () => (20.0 + random.NextDouble() * 5).ToString("F1"), // Random temp between 20.0 and 25.0
        TimeSpan.FromSeconds(10),
        loggerFactory.CreateLogger<DeviceSimulator>(),
        ApiBaseUrl
    ),

    // Simulates a light, toggling between "On" and "Off" every 15 seconds.
    new DeviceSimulator(
        "LivingRoomLight",
        () => random.Next(2) == 0 ? "Off" : "On",
        TimeSpan.FromSeconds(15),
        loggerFactory.CreateLogger<DeviceSimulator>(),
        ApiBaseUrl
    ),
    
    // Simulates another light.
    new DeviceSimulator(
        "BedroomLight",
        () => random.Next(2) == 0 ? "Off" : "On",
        TimeSpan.FromSeconds(25),
        loggerFactory.CreateLogger<DeviceSimulator>(),
        ApiBaseUrl
    ),

    // Simulates a motion sensor, sending "true" or "false" every 7 seconds.
    new DeviceSimulator(
        "HallwayMotionSensor",
        () => (random.Next(5) == 0).ToString().ToLower(), // 1 in 5 chance of detecting motion
        TimeSpan.FromSeconds(7),
        loggerFactory.CreateLogger<DeviceSimulator>(),
        ApiBaseUrl
    )
};

// --- Start all simulators ---
var tasks = devices.Select(d => d.Start(cts.Token)).ToList();

logger.LogInformation("{Count} device simulators running. Press Ctrl+C to exit.", devices.Count);

await Task.WhenAll(tasks);

logger.LogInformation("Device Simulator is shutting down.");
