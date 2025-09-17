using Microsoft.Extensions.Logging;
using NexusHome.DeviceSimulator;

// --- Configuration ---
const string ApiBaseUrl = "https://localhost:7123"; // Make sure this matches your API's launch URL

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Starting NexusHome Device Simulator...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    logger.LogInformation("Cancellation requested. Shutting down simulators.");
    eventArgs.Cancel = true;
    cts.Cancel();
};

var random = new Random();

// --- Simulator Definitions ---
var simulators = new List<DeviceSimulator>
{
    new ("LIVING_ROOM_THERMOSTAT", () => (20.0 + random.NextDouble() * 5).ToString("F1"), TimeSpan.FromSeconds(10), loggerFactory.CreateLogger<DeviceSimulator>(), ApiBaseUrl),
    new ("LIVING_ROOM_LIGHT", () => (random.Next(2) == 0 ? "Off" : "On"), TimeSpan.FromSeconds(15), loggerFactory.CreateLogger<DeviceSimulator>(), ApiBaseUrl),
    new ("BEDROOM_LIGHT", () => (random.Next(2) == 0 ? "Off" : "On"), TimeSpan.FromSeconds(25), loggerFactory.CreateLogger<DeviceSimulator>(), ApiBaseUrl),
    new ("HALLWAY_MOTION_SENSOR", () => (random.Next(5) == 0).ToString().ToLower(), TimeSpan.FromSeconds(7), loggerFactory.CreateLogger<DeviceSimulator>(), ApiBaseUrl)
};

// --- Start Simulators ---
var simulationTasks = simulators.Select(s => s.Start(cts.Token)).ToList();
logger.LogInformation("{SimulatorCount} device simulators are running. Press Ctrl+C to stop.", simulators.Count);

await Task.WhenAll(simulationTasks);

logger.LogInformation("All device simulators have been stopped.");
