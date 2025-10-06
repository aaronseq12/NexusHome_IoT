using Microsoft.AspNetCore.SignalR;

namespace NexusHome.IoT.Application.Hubs;

public class SmartDeviceStatusHub : Hub
{
    private readonly ILogger<SmartDeviceStatusHub> _logger;

    public SmartDeviceStatusHub(ILogger<SmartDeviceStatusHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinDeviceGroup(string deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device_{deviceId}");
        _logger.LogDebug("Client {ConnectionId} joined device group {DeviceId}", Context.ConnectionId, deviceId);
    }

    public async Task LeaveDeviceGroup(string deviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device_{deviceId}");
        _logger.LogDebug("Client {ConnectionId} left device group {DeviceId}", Context.ConnectionId, deviceId);
    }

    public async Task JoinRoomGroup(string roomName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomName}");
        _logger.LogDebug("Client {ConnectionId} joined room group {RoomName}", Context.ConnectionId, roomName);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to device status hub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected from device status hub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

public class EnergyMonitoringHub : Hub
{
    private readonly ILogger<EnergyMonitoringHub> _logger;

    public EnergyMonitoringHub(ILogger<EnergyMonitoringHub> logger)
    {
        _logger = logger;
    }

    public async Task SubscribeToEnergyUpdates()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "energy_monitoring");
        _logger.LogDebug("Client {ConnectionId} subscribed to energy updates", Context.ConnectionId);
    }

    public async Task UnsubscribeFromEnergyUpdates()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "energy_monitoring");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from energy updates", Context.ConnectionId);
    }
}

public class SystemNotificationHub : Hub
{
    private readonly ILogger<SystemNotificationHub> _logger;

    public SystemNotificationHub(ILogger<SystemNotificationHub> logger)
    {
        _logger = logger;
    }

    public async Task SubscribeToNotifications()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "system_notifications");
        _logger.LogDebug("Client {ConnectionId} subscribed to system notifications", Context.ConnectionId);
    }
}
