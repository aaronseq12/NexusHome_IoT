using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using System.Text;
using System.Text.Json;
using NexusHome.IoT.Models;
using NexusHome.IoT.Data;
using Microsoft.EntityFrameworkCore;

namespace NexusHome.IoT.Services
{
    public interface IMqttService
    {
        Task PublishAsync(string topic, object payload);
        Task SubscribeAsync(string topic);
        Task SendDeviceCommandAsync(string deviceId, object command);
    }

    public class MqttService : BackgroundService, IMqttService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MqttService> _logger;
        private readonly IConfiguration _configuration;
        private IMqttClient? _mqttClient;

        public MqttService(
            IServiceProvider serviceProvider,
            ILogger<MqttService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await StartMqttClient(stoppingToken);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                // Keep the service running
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }
        
        private async Task StartMqttClient(CancellationToken cancellationToken)
        {
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_configuration["MQTT:BrokerHost"], _configuration.GetValue<int>("MQTT:BrokerPort"))
                .WithClientId($"NexusHome_Server_{Guid.NewGuid()}")
                .WithCleanSession()
                .Build();

            _mqttClient.ConnectedAsync += OnMqttClientConnected;
            _mqttClient.DisconnectedAsync += OnMqttClientDisconnected;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            await _mqttClient.ConnectAsync(options, cancellationToken);
        }

        public async Task PublishAsync(string topic, object payload)
        {
            if (_mqttClient?.IsConnected != true)
            {
                _logger.LogWarning("MQTT client not connected. Cannot publish to {Topic}", topic);
                return;
            }

            var jsonPayload = JsonSerializer.Serialize(payload);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(jsonPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message);
        }

        public async Task SubscribeAsync(string topic)
        {
            if (_mqttClient?.IsConnected != true)
            {
                _logger.LogWarning("MQTT client not connected. Cannot subscribe to {Topic}", topic);
                return;
            }
            await _mqttClient.SubscribeAsync(topic);
        }

        public async Task SendDeviceCommandAsync(string deviceId, object command)
        {
            var topic = $"nexushome/devices/{deviceId}/commands";
            await PublishAsync(topic, command);
        }

        private async Task OnMqttClientConnected(MqttClientConnectedEventArgs e)
        {
            _logger.LogInformation("MQTT client connected.");
            await SubscribeToDeviceTopics();
        }

        private async Task OnMqttClientDisconnected(MqttClientDisconnectedEventArgs e)
        {
            _logger.LogWarning("MQTT client disconnected. Attempting to reconnect...");
            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                 if (_mqttClient != null)
                 {
                    await _mqttClient.ConnectAsync(_mqttClient.Options);
                 }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT reconnection failed.");
            }
        }

        private async Task SubscribeToDeviceTopics()
        {
            await SubscribeAsync("nexushome/devices/+/telemetry");
            await SubscribeAsync("nexushome/devices/+/status");
        }

        private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            _logger.LogInformation("Received MQTT message on topic '{Topic}': {Payload}", topic, payload);

            // Example: nexushome/devices/LIVING_ROOM_LIGHT/telemetry
            var topicSegments = topic.Split('/');
            if (topicSegments.Length == 4 && topicSegments[0] == "nexushome" && topicSegments[1] == "devices")
            {
                var deviceId = topicSegments[2];
                var messageType = topicSegments[3];

                using var scope = _serviceProvider.CreateScope();
                var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
                await deviceService.ProcessDeviceMessageAsync(deviceId, messageType, payload);
            }
        }
    }
}
