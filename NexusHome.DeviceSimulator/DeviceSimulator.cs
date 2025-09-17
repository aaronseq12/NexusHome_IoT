using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NexusHome.DeviceSimulator
{
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
        private readonly Func<string> _valueGenerator;
        private readonly TimeSpan _interval;
        private readonly ILogger<DeviceSimulator> _logger;

        public DeviceSimulator(string deviceName, Func<string> valueGenerator, TimeSpan interval, ILogger<DeviceSimulator> logger, string apiBaseUrl)
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
            _deviceName = deviceName;
            _valueGenerator = valueGenerator;
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
                    string value = _valueGenerator();
                    var data = new TelemetryData { DeviceName = _deviceName, Value = value };

                    // This simulator is simplified and assumes a simple telemetry structure.
                    // In a real scenario, this would publish to an MQTT topic.
                    // For this version, it posts to a simple API endpoint for demonstration.
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
}
