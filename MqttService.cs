using Microsoft.Azure.Devices.Client;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NexusHome.Models;
using NexusHome.Data;
using Microsoft.EntityFrameworkCore;

namespace NexusHome.IoT
{
    public interface IMqttService
    {
        Task StartAsync();
        Task StopAsync();
        Task PublishAsync(string topic, object payload);
        Task SubscribeAsync(string topic);
        Task SendDeviceCommandAsync(string deviceId, object command);
        event EventHandler<DeviceDataReceivedEventArgs> DeviceDataReceived;
        event EventHandler<DeviceStatusChangedEventArgs> DeviceStatusChanged;
    }

    public class MqttService : IMqttService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MqttService> _logger;
        private readonly IConfiguration _configuration;
        private IMqttClient? _mqttClient;
        private readonly DeviceClient? _azureDeviceClient;
        private readonly Timer _heartbeatTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<DeviceDataReceivedEventArgs>? DeviceDataReceived;
        public event EventHandler<DeviceStatusChangedEventArgs>? DeviceStatusChanged;

        public MqttService(
            IServiceProvider serviceProvider,
            ILogger<MqttService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize Azure IoT Hub Device Client if connection string is provided
            var azureConnectionString = _configuration.GetConnectionString("IoTHub");
            if (!string.IsNullOrEmpty(azureConnectionString))
            {
                try
                {
                    _azureDeviceClient = DeviceClient.CreateFromConnectionString(azureConnectionString, TransportType.Mqtt);
                    _logger.LogInformation("Azure IoT Hub Device Client initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Azure IoT Hub Device Client");
                }
            }

            // Initialize heartbeat timer for device health monitoring
            _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public async Task StartAsync()
        {
            try
            {
                var mqttFactory = new MqttFactory();
                _mqttClient = mqttFactory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_configuration["MQTT:BrokerHost"], 
                                  _configuration.GetValue<int>("MQTT:BrokerPort"))
                    .WithClientId(_configuration["MQTT:ClientId"] ?? $"NexusHome_{Guid.NewGuid()}")
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(_configuration.GetValue<int>("MQTT:KeepAlivePeriod")))
                    .WithCleanSession(_configuration.GetValue<bool>("MQTT:CleanSession"))
                    .Build();

                // Set up event handlers
                _mqttClient.ConnectedAsync += OnMqttClientConnected;
                _mqttClient.DisconnectedAsync += OnMqttClientDisconnected;
                _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

                await _mqttClient.ConnectAsync(options, _cancellationTokenSource.Token);

                // Subscribe to device topics
                await SubscribeToDeviceTopics();

                // Start Azure IoT Hub connection if available
                if (_azureDeviceClient != null)
                {
                    await _azureDeviceClient.OpenAsync();
                    await _azureDeviceClient.SetMethodHandlerAsync("DirectCommand", OnDirectMethodReceived, null);
                    _logger.LogInformation("Connected to Azure IoT Hub successfully");
                }

                _logger.LogInformation("MQTT Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start MQTT Service");
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _heartbeatTimer?.Dispose();

                if (_mqttClient?.IsConnected == true)
                {
                    await _mqttClient.DisconnectAsync();
                }

                if (_azureDeviceClient != null)
                {
                    await _azureDeviceClient.CloseAsync();
                }

                _logger.LogInformation("MQTT Service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping MQTT Service");
            }
        }

        public async Task PublishAsync(string topic, object payload)
        {
            if (_mqttClient?.IsConnected != true)
            {
                _logger.LogWarning("MQTT client is not connected. Cannot publish message to topic: {Topic}", topic);
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(json)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(message, _cancellationTokenSource.Token);
                _logger.LogDebug("Published message to topic: {Topic}", topic);

                // Also send to Azure IoT Hub if available
                if (_azureDeviceClient != null)
                {
                    var azureMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(json))
                    {
                        Properties = { { "messageType", "telemetry" }, { "topic", topic } }
                    };
                    await _azureDeviceClient.SendEventAsync(azureMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to topic: {Topic}", topic);
            }
        }

        public async Task SubscribeAsync(string topic)
        {
            if (_mqttClient?.IsConnected != true)
            {
                _logger.LogWarning("MQTT client is not connected. Cannot subscribe to topic: {Topic}", topic);
                return;
            }

            try
            {
                await _mqttClient.SubscribeAsync(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                _logger.LogInformation("Subscribed to topic: {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to topic: {Topic}", topic);
            }
        }

        public async Task SendDeviceCommandAsync(string deviceId, object command)
        {
            var topic = $"nexushome/devices/{deviceId}/commands";
            await PublishAsync(topic, command);
        }

        private async Task SubscribeToDeviceTopics()
        {
            var topics = new[]
            {
                "nexushome/devices/+/data",
                "nexushome/devices/+/status",
                "nexushome/energy/+/consumption",
                "nexushome/solar/+/generation",
                "nexushome/battery/+/status",
                "nexushome/alerts/+"
            };

            foreach (var topic in topics)
            {
                await SubscribeAsync(topic);
            }
        }

        private async Task OnMqttClientConnected(MqttClientConnectedEventArgs e)
        {
            _logger.LogInformation("MQTT client connected successfully");
            await SubscribeToDeviceTopics();
        }

        private Task OnMqttClientDisconnected(MqttClientDisconnectedEventArgs e)
        {
            _logger.LogWarning("MQTT client disconnected: {Reason}", e.Reason);
            
            // Attempt to reconnect after a delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    if (_mqttClient != null && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await StartAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconnect MQTT client");
                }
            });

            return Task.CompletedTask;
        }

        private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                
                _logger.LogDebug("Received message on topic {Topic}: {Payload}", topic, payload);

                // Parse topic to determine message type and device
                var topicParts = topic.Split('/');
                if (topicParts.Length < 4) return;

                var messageType = topicParts[2]; // devices, energy, solar, battery, alerts
                var deviceId = topicParts[3];
                var dataType = topicParts.Length > 4 ? topicParts[4] : "data";

                await ProcessDeviceMessage(deviceId, messageType, dataType, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received MQTT message");
            }
        }

        private async Task ProcessDeviceMessage(string deviceId, string messageType, string dataType, string payload)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                switch (messageType)
                {
                    case "devices":
                        await ProcessDeviceData(context, deviceId, dataType, payload);
                        break;
                    case "energy":
                        await ProcessEnergyData(context, deviceId, payload);
                        break;
                    case "solar":
                        await ProcessSolarData(context, deviceId, payload);
                        break;
                    case "battery":
                        await ProcessBatteryData(context, deviceId, payload);
                        break;
                    case "alerts":
                        await ProcessAlertData(context, deviceId, payload);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {MessageType} message for device {DeviceId}", messageType, deviceId);
            }
        }

        private async Task ProcessDeviceData(NexusHomeDbContext context, string deviceId, string dataType, string payload)
        {
            var device = await context.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device == null)
            {
                _logger.LogWarning("Unknown device: {DeviceId}", deviceId);
                return;
            }

            // Update device last seen
            device.LastSeen = DateTime.UtcNow;
            device.IsOnline = true;

            if (dataType == "data")
            {
                // Parse device telemetry data
                var telemetryData = JsonSerializer.Deserialize<DeviceTelemetryData>(payload);
                if (telemetryData != null)
                {
                    device.CurrentPowerConsumption = telemetryData.PowerConsumption;

                    // Trigger device data received event
                    DeviceDataReceived?.Invoke(this, new DeviceDataReceivedEventArgs
                    {
                        DeviceId = deviceId,
                        TelemetryData = telemetryData,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            else if (dataType == "status")
            {
                // Parse device status data
                var statusData = JsonSerializer.Deserialize<DeviceStatusData>(payload);
                if (statusData != null)
                {
                    var oldStatus = device.Status;
                    device.Status = Enum.Parse<DeviceStatus>(statusData.Status, true);

                    // Trigger device status changed event if status changed
                    if (oldStatus != device.Status)
                    {
                        DeviceStatusChanged?.Invoke(this, new DeviceStatusChangedEventArgs
                        {
                            DeviceId = deviceId,
                            OldStatus = oldStatus,
                            NewStatus = device.Status,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        private async Task ProcessEnergyData(NexusHomeDbContext context, string deviceId, string payload)
        {
            var device = await context.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device == null) return;

            var energyData = JsonSerializer.Deserialize<EnergyConsumptionData>(payload);
            if (energyData == null) return;

            var consumption = new EnergyConsumption
            {
                DeviceId = device.Id,
                PowerConsumption = energyData.PowerConsumption,
                Voltage = energyData.Voltage,
                Current = energyData.Current,
                PowerFactor = energyData.PowerFactor,
                Frequency = energyData.Frequency,
                Cost = energyData.Cost,
                Source = energyData.Source,
                TariffRate = energyData.TariffRate,
                Timestamp = energyData.Timestamp ?? DateTime.UtcNow
            };

            context.EnergyConsumptions.Add(consumption);
            await context.SaveChangesAsync();
        }

        private async Task ProcessSolarData(NexusHomeDbContext context, string deviceId, string payload)
        {
            var device = await context.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device == null) return;

            var solarData = JsonSerializer.Deserialize<SolarGenerationData>(payload);
            if (solarData == null) return;

            var generation = new SolarGeneration
            {
                DeviceId = device.Id,
                PowerGeneration = solarData.PowerGeneration,
                Efficiency = solarData.Efficiency,
                Temperature = solarData.Temperature,
                Irradiance = solarData.Irradiance,
                Revenue = solarData.Revenue,
                Timestamp = solarData.Timestamp ?? DateTime.UtcNow
            };

            context.SolarGenerations.Add(generation);
            await context.SaveChangesAsync();
        }

        private async Task ProcessBatteryData(NexusHomeDbContext context, string deviceId, string payload)
        {
            var device = await context.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device == null) return;

            var batteryData = JsonSerializer.Deserialize<BatteryStatusData>(payload);
            if (batteryData == null) return;

            var status = new BatteryStatus
            {
                DeviceId = device.Id,
                ChargeLevel = batteryData.ChargeLevel,
                Capacity = batteryData.Capacity,
                ChargingRate = batteryData.ChargingRate,
                DischargingRate = batteryData.DischargingRate,
                Temperature = batteryData.Temperature,
                Health = batteryData.Health,
                CycleCount = batteryData.CycleCount,
                Mode = batteryData.Mode,
                Timestamp = batteryData.Timestamp ?? DateTime.UtcNow
            };

            context.BatteryStatuses.Add(status);
            await context.SaveChangesAsync();
        }

        private async Task ProcessAlertData(NexusHomeDbContext context, string deviceId, string payload)
        {
            var device = await context.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device == null) return;

            var alertData = JsonSerializer.Deserialize<AlertData>(payload);
            if (alertData == null) return;

            var alert = new DeviceAlert
            {
                DeviceId = device.Id,
                Type = alertData.Type,
                Severity = alertData.Severity,
                Title = alertData.Title,
                Message = alertData.Message,
                Timestamp = alertData.Timestamp ?? DateTime.UtcNow,
                Data = payload
            };

            context.DeviceAlerts.Add(alert);
            await context.SaveChangesAsync();
        }

        private async Task<MethodResponse> OnDirectMethodReceived(MethodRequest methodRequest, object userContext)
        {
            try
            {
                _logger.LogInformation("Direct method received: {MethodName}", methodRequest.Name);

                var response = methodRequest.Name switch
                {
                    "GetDeviceStatus" => await HandleGetDeviceStatus(methodRequest.DataAsJson),
                    "SetDeviceConfiguration" => await HandleSetDeviceConfiguration(methodRequest.DataAsJson),
                    "TriggerMaintenance" => await HandleTriggerMaintenance(methodRequest.DataAsJson),
                    _ => new { result = "Unknown method", status = 404 }
                };

                var responseJson = JsonSerializer.Serialize(response);
                return new MethodResponse(Encoding.UTF8.GetBytes(responseJson), 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling direct method: {MethodName}", methodRequest.Name);
                var errorResponse = JsonSerializer.Serialize(new { error = ex.Message, status = 500 });
                return new MethodResponse(Encoding.UTF8.GetBytes(errorResponse), 500);
            }
        }

        private async Task<object> HandleGetDeviceStatus(string requestData)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            var request = JsonSerializer.Deserialize<DeviceStatusRequest>(requestData);
            if (request?.DeviceId == null)
                return new { error = "DeviceId is required", status = 400 };

            var device = await context.Devices.FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId);
            if (device == null)
                return new { error = "Device not found", status = 404 };

            return new
            {
                deviceId = device.DeviceId,
                status = device.Status.ToString(),
                isOnline = device.IsOnline,
                lastSeen = device.LastSeen,
                powerConsumption = device.CurrentPowerConsumption
            };
        }

        private async Task<object> HandleSetDeviceConfiguration(string requestData)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            var request = JsonSerializer.Deserialize<DeviceConfigurationRequest>(requestData);
            if (request?.DeviceId == null || request.Configuration == null)
                return new { error = "DeviceId and Configuration are required", status = 400 };

            var device = await context.Devices.FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId);
            if (device == null)
                return new { error = "Device not found", status = 404 };

            device.Configuration = JsonSerializer.Serialize(request.Configuration);
            await context.SaveChangesAsync();

            // Send configuration update to device
            await SendDeviceCommandAsync(request.DeviceId, new
            {
                command = "updateConfiguration",
                configuration = request.Configuration,
                timestamp = DateTime.UtcNow
            });

            return new { result = "Configuration updated successfully", status = 200 };
        }

        private async Task<object> HandleTriggerMaintenance(string requestData)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            var request = JsonSerializer.Deserialize<MaintenanceRequest>(requestData);
            if (request?.DeviceId == null)
                return new { error = "DeviceId is required", status = 400 };

            var device = await context.Devices.FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId);
            if (device == null)
                return new { error = "Device not found", status = 404 };

            var maintenanceRecord = new DeviceMaintenanceRecord
            {
                DeviceId = device.Id,
                Type = MaintenanceType.Predictive,
                Title = request.Title ?? "Scheduled Maintenance",
                Description = request.Description,
                Status = MaintenanceStatus.Scheduled,
                Priority = request.Priority ?? MaintenancePriority.Medium,
                ScheduledDate = request.ScheduledDate ?? DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow
            };

            context.MaintenanceRecords.Add(maintenanceRecord);
            await context.SaveChangesAsync();

            return new { result = "Maintenance scheduled successfully", maintenanceId = maintenanceRecord.Id, status = 200 };
        }

        private async void SendHeartbeat(object? state)
        {
            if (_mqttClient?.IsConnected == true && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var heartbeat = new
                    {
                        timestamp = DateTime.UtcNow,
                        service = "NexusHome MQTT Service",
                        status = "healthy",
                        connectedDevices = await GetConnectedDeviceCount()
                    };

                    await PublishAsync("nexushome/system/heartbeat", heartbeat);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending heartbeat");
                }
            }
        }

        private async Task<int> GetConnectedDeviceCount()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();
            return await context.Devices.CountAsync(d => d.IsOnline);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _heartbeatTimer?.Dispose();
            _mqttClient?.Dispose();
            _azureDeviceClient?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }

    // Event argument classes
    public class DeviceDataReceivedEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public DeviceTelemetryData TelemetryData { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class DeviceStatusChangedEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public DeviceStatus OldStatus { get; set; }
        public DeviceStatus NewStatus { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // Data transfer objects for MQTT messages
    public class DeviceTelemetryData
    {
        public decimal PowerConsumption { get; set; }
        public decimal Temperature { get; set; }
        public decimal Humidity { get; set; }
        public bool Motion { get; set; }
        public bool DoorOpen { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }

    public class DeviceStatusData
    {
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> StatusDetails { get; set; } = new();
    }

    public class EnergyConsumptionData
    {
        public decimal PowerConsumption { get; set; }
        public decimal Voltage { get; set; }
        public decimal Current { get; set; }
        public decimal PowerFactor { get; set; }
        public decimal Frequency { get; set; }
        public decimal Cost { get; set; }
        public EnergySource Source { get; set; }
        public string? TariffRate { get; set; }
        public DateTime? Timestamp { get; set; }
    }

    public class SolarGenerationData
    {
        public decimal PowerGeneration { get; set; }
        public decimal Efficiency { get; set; }
        public decimal Temperature { get; set; }
        public decimal Irradiance { get; set; }
        public decimal Revenue { get; set; }
        public DateTime? Timestamp { get; set; }
    }

    public class BatteryStatusData
    {
        public decimal ChargeLevel { get; set; }
        public decimal Capacity { get; set; }
        public decimal ChargingRate { get; set; }
        public decimal DischargingRate { get; set; }
        public decimal Temperature { get; set; }
        public decimal Health { get; set; }
        public int CycleCount { get; set; }
        public BatteryMode Mode { get; set; }
        public DateTime? Timestamp { get; set; }
    }

    public class AlertData
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }
    }

    // Request objects for direct methods
    public class DeviceStatusRequest
    {
        public string DeviceId { get; set; } = string.Empty;
    }

    public class DeviceConfigurationRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    public class MaintenanceRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public MaintenancePriority? Priority { get; set; }
        public DateTime? ScheduledDate { get; set; }
    }
}