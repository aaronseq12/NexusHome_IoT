using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using NexusHome.IoT.Core.Domain;
using NexusHome.IoT.Core.Services.Interfaces;
using System.Text;

namespace NexusHome.IoT.Infrastructure.Services;

public class MqttClientService : IMqttClientService, IHostedService
{
    private readonly MqttBrokerSettings _settings;
    private readonly ILogger<MqttClientService> _logger;
    private IMqttClient? _mqttClient;
    private readonly MqttFactory _mqttFactory;

    public event Func<string, string, Task>? MessageReceived;

    public MqttClientService(IOptions<MqttBrokerSettings> settings, ILogger<MqttClientService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _mqttFactory = new MqttFactory();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _mqttClient = _mqttFactory.CreateMqttClient();
            
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.Host, _settings.Port)
                .WithCredentials(_settings.Username, _settings.Password)
                .WithClientId(_settings.ClientId)
                .WithCleanSession()
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

            await _mqttClient.ConnectAsync(options, cancellationToken);
            
            // Subscribe to default topics
            foreach (var topic in _settings.Topics.Values)
            {
                await SubscribeAsync(topic, cancellationToken);
            }
            
            _logger.LogInformation("MQTT client connected successfully to {Host}:{Port}", _settings.Host, _settings.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect MQTT client");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);
        }
        
        _mqttClient?.Dispose();
        _logger.LogInformation("MQTT client disconnected");
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("MQTT client is not connected, cannot publish message");
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _mqttClient.PublishAsync(message, cancellationToken);
        _logger.LogDebug("Published message to topic {Topic}", topic);
    }

    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("MQTT client is not connected, cannot subscribe to topic");
            return;
        }

        var subscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topic))
            .Build();

        await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
        _logger.LogInformation("Subscribed to topic {Topic}", topic);
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _logger.LogInformation("MQTT client connected");
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("MQTT client disconnected: {Reason}", args.Reason);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
        
        _logger.LogDebug("Received MQTT message on topic {Topic}", topic);
        
        if (MessageReceived != null)
        {
            await MessageReceived(topic, payload);
        }
    }
}
