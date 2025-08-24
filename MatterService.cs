using System.Net.NetworkInformation;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexusHome.Models;
using NexusHome.Data;
using NexusHome.IoT;

namespace NexusHome.IoT
{
    public interface IMatterService
    {
        Task StartAsync();
        Task StopAsync();
        Task<MatterCommissioningResult> CommissionDeviceAsync(string setupCode, string discriminator);
        Task<List<MatterDevice>> DiscoverDevicesAsync();
        Task<MatterDeviceInfo> GetDeviceInfoAsync(ulong nodeId);
        Task<bool> SendCommandAsync(ulong nodeId, uint clusterId, uint commandId, byte[] payload);
        Task<MatterAttributeValue> ReadAttributeAsync(ulong nodeId, uint clusterId, uint attributeId);
        Task<bool> WriteAttributeAsync(ulong nodeId, uint clusterId, uint attributeId, object value);
        Task<bool> SubscribeToAttributeAsync(ulong nodeId, uint clusterId, uint attributeId, TimeSpan reportingInterval);
        Task<List<MatterFabric>> GetFabricsAsync();
        Task<bool> RemoveDeviceAsync(ulong nodeId);
        Task<MatterNetworkInfo> GetNetworkInfoAsync();
        event EventHandler<MatterDeviceEventArgs> DeviceCommissioned;
        event EventHandler<MatterDeviceEventArgs> DeviceDecommissioned;
        event EventHandler<MatterAttributeEventArgs> AttributeChanged;
    }

    public class MatterService : IMatterService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MatterService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMqttService _mqttService;
        private readonly Dictionary<ulong, MatterDevice> _commissionedDevices;
        private readonly Dictionary<string, MatterClusterHandler> _clusterHandlers;
        private bool _isStarted;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Timer _discoveryTimer;

        public event EventHandler<MatterDeviceEventArgs>? DeviceCommissioned;
        public event EventHandler<MatterDeviceEventArgs>? DeviceDecommissioned;
        public event EventHandler<MatterAttributeEventArgs>? AttributeChanged;

        // Matter configuration from appsettings
        private readonly ulong _fabricId;
        private readonly ulong _nodeId;
        private readonly uint _vendorId;
        private readonly uint _productId;
        private readonly int _commissioningTimeout;

        public MatterService(
            IServiceProvider serviceProvider,
            ILogger<MatterService> logger,
            IConfiguration configuration,
            IMqttService mqttService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _mqttService = mqttService;
            _commissionedDevices = new Dictionary<ulong, MatterDevice>();
            _clusterHandlers = new Dictionary<string, MatterClusterHandler>();
            _cancellationTokenSource = new CancellationTokenSource();

            // Load Matter configuration
            _fabricId = Convert.ToUInt64(_configuration["Matter:FabricId"], 16);
            _nodeId = Convert.ToUInt64(_configuration["Matter:NodeId"], 16);
            _vendorId = Convert.ToUInt32(_configuration["Matter:VendorId"], 16);
            _productId = Convert.ToUInt32(_configuration["Matter:ProductId"], 16);
            _commissioningTimeout = _configuration.GetValue<int>("Matter:CommissioningTimeout");

            // Initialize cluster handlers
            InitializeClusterHandlers();

            // Set up device discovery timer
            _discoveryTimer = new Timer(PerformDeviceDiscovery, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        }

        public async Task StartAsync()
        {
            try
            {
                _logger.LogInformation("Starting Matter service...");

                // Initialize Matter stack (this would integrate with actual Matter SDK)
                await InitializeMatterStackAsync();

                // Load previously commissioned devices
                await LoadCommissionedDevicesAsync();

                // Start listening for Matter messages
                await StartMatterListenerAsync();

                _isStarted = true;
                _logger.LogInformation("Matter service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Matter service");
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                _logger.LogInformation("Stopping Matter service...");

                _cancellationTokenSource.Cancel();
                _discoveryTimer?.Dispose();

                // Close connections to commissioned devices
                foreach (var device in _commissionedDevices.Values)
                {
                    try
                    {
                        await CloseDeviceConnectionAsync(device.NodeId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing connection to device {NodeId}", device.NodeId);
                    }
                }

                // Shutdown Matter stack
                await ShutdownMatterStackAsync();

                _isStarted = false;
                _logger.LogInformation("Matter service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Matter service");
            }
        }

        public async Task<MatterCommissioningResult> CommissionDeviceAsync(string setupCode, string discriminator)
        {
            try
            {
                _logger.LogInformation("Starting device commissioning with setup code: {SetupCode}", setupCode);

                // Validate setup code format
                if (!IsValidSetupCode(setupCode))
                {
                    return new MatterCommissioningResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid setup code format",
                        NodeId = 0
                    };
                }

                // Start commissioning process
                var commissioningSession = await StartCommissioningSessionAsync(setupCode, discriminator);
                
                if (commissioningSession == null)
                {
                    return new MatterCommissioningResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to establish commissioning session",
                        NodeId = 0
                    };
                }

                // Perform PASE (Password-Authenticated Session Establishment)
                var paseResult = await PerformPaseHandshakeAsync(commissioningSession);
                
                if (!paseResult.Success)
                {
                    return new MatterCommissioningResult
                    {
                        Success = false,
                        ErrorMessage = $"PASE handshake failed: {paseResult.ErrorMessage}",
                        NodeId = 0
                    };
                }

                // Read device information
                var deviceInfo = await ReadDeviceInformationAsync(commissioningSession);
                
                // Assign node ID
                var nodeId = GenerateNodeId();
                
                // Configure network credentials (WiFi/Thread)
                var networkConfig = await ConfigureNetworkCredentialsAsync(commissioningSession);
                
                if (!networkConfig.Success)
                {
                    return new MatterCommissioningResult
                    {
                        Success = false,
                        ErrorMessage = $"Network configuration failed: {networkConfig.ErrorMessage}",
                        NodeId = 0
                    };
                }

                // Install operational certificates
                var certInstallResult = await InstallOperationalCertificatesAsync(commissioningSession, nodeId);
                
                if (!certInstallResult.Success)
                {
                    return new MatterCommissioningResult
                    {
                        Success = false,
                        ErrorMessage = $"Certificate installation failed: {certInstallResult.ErrorMessage}",
                        NodeId = 0
                    };
                }

                // Complete commissioning
                var commissioningComplete = await CompleteCommissioningAsync(commissioningSession);
                
                if (!commissioningComplete.Success)
                {
                    return new MatterCommissioningResult
                    {
                        Success = false,
                        ErrorMessage = $"Commissioning completion failed: {commissioningComplete.ErrorMessage}",
                        NodeId = 0
                    };
                }

                // Create Matter device object
                var matterDevice = new MatterDevice
                {
                    NodeId = nodeId,
                    VendorId = deviceInfo.VendorId,
                    ProductId = deviceInfo.ProductId,
                    DeviceType = deviceInfo.DeviceType,
                    DeviceName = deviceInfo.DeviceName,
                    FabricId = _fabricId,
                    SupportedClusters = deviceInfo.SupportedClusters,
                    NetworkType = networkConfig.NetworkType,
                    IsOnline = true,
                    CommissionedAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow
                };

                _commissionedDevices[nodeId] = matterDevice;

                // Save to database
                await SaveCommissionedDeviceAsync(matterDevice);

                // Set up attribute subscriptions
                await SetupDeviceSubscriptionsAsync(matterDevice);

                // Notify about successful commissioning
                DeviceCommissioned?.Invoke(this, new MatterDeviceEventArgs { Device = matterDevice });

                _logger.LogInformation("Device commissioned successfully. Node ID: {NodeId}", nodeId);

                return new MatterCommissioningResult
                {
                    Success = true,
                    NodeId = nodeId,
                    DeviceInfo = matterDevice,
                    CommissioningTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error commissioning device");
                return new MatterCommissioningResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    NodeId = 0
                };
            }
        }

        public async Task<List<MatterDevice>> DiscoverDevicesAsync()
        {
            try
            {
                _logger.LogInformation("Starting Matter device discovery");

                var discoveredDevices = new List<MatterDevice>();

                // Perform mDNS-based discovery for Matter devices
                var mdnsDevices = await PerformMdnsDiscoveryAsync();
                discoveredDevices.AddRange(mdnsDevices);

                // Perform Thread network discovery if Thread is enabled
                if (_configuration.GetValue<bool>("Matter:EnableThread"))
                {
                    var threadDevices = await PerformThreadDiscoveryAsync();
                    discoveredDevices.AddRange(threadDevices);
                }

                // Filter out already commissioned devices
                var uncommissionedDevices = discoveredDevices
                    .Where(d => !_commissionedDevices.ContainsKey(d.NodeId))
                    .ToList();

                _logger.LogInformation("Discovered {Count} Matter devices ({Uncommissioned} uncommissioned)", 
                    discoveredDevices.Count, uncommissionedDevices.Count);

                return uncommissionedDevices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering Matter devices");
                return new List<MatterDevice>();
            }
        }

        public async Task<MatterDeviceInfo> GetDeviceInfoAsync(ulong nodeId)
        {
            try
            {
                if (!_commissionedDevices.TryGetValue(nodeId, out var device))
                {
                    throw new ArgumentException($"Device with node ID {nodeId} not found");
                }

                // Read basic information cluster (cluster ID 0x0028)
                const uint BASIC_INFORMATION_CLUSTER = 0x0028;

                var vendorName = await ReadAttributeAsync(nodeId, BASIC_INFORMATION_CLUSTER, 0x0001); // VendorName
                var productName = await ReadAttributeAsync(nodeId, BASIC_INFORMATION_CLUSTER, 0x0003); // ProductName
                var hardwareVersion = await ReadAttributeAsync(nodeId, BASIC_INFORMATION_CLUSTER, 0x0007); // HardwareVersion
                var softwareVersion = await ReadAttributeAsync(nodeId, BASIC_INFORMATION_CLUSTER, 0x0009); // SoftwareVersion
                var serialNumber = await ReadAttributeAsync(nodeId, BASIC_INFORMATION_CLUSTER, 0x000F); // SerialNumber

                return new MatterDeviceInfo
                {
                    NodeId = nodeId,
                    VendorId = device.VendorId,
                    ProductId = device.ProductId,
                    DeviceType = device.DeviceType,
                    VendorName = vendorName?.StringValue ?? "Unknown",
                    ProductName = productName?.StringValue ?? "Unknown",
                    HardwareVersion = hardwareVersion?.StringValue ?? "Unknown",
                    SoftwareVersion = softwareVersion?.StringValue ?? "Unknown",
                    SerialNumber = serialNumber?.StringValue ?? "Unknown",
                    SupportedClusters = device.SupportedClusters,
                    IsOnline = device.IsOnline,
                    LastSeen = device.LastSeen
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device info for node {NodeId}", nodeId);
                throw;
            }
        }

        public async Task<bool> SendCommandAsync(ulong nodeId, uint clusterId, uint commandId, byte[] payload)
        {
            try
            {
                if (!_commissionedDevices.ContainsKey(nodeId))
                {
                    _logger.LogWarning("Attempt to send command to unknown device {NodeId}", nodeId);
                    return false;
                }

                _logger.LogDebug("Sending command {CommandId} to cluster {ClusterId} on node {NodeId}", 
                    commandId, clusterId, nodeId);

                // Create Matter message
                var message = CreateMatterMessage(nodeId, clusterId, commandId, payload);
                
                // Send message via Matter transport
                var success = await SendMatterMessageAsync(message);
                
                if (success)
                {
                    // Update device last seen timestamp
                    if (_commissionedDevices.TryGetValue(nodeId, out var device))
                    {
                        device.LastSeen = DateTime.UtcNow;
                        device.IsOnline = true;
                    }

                    // Publish command to MQTT for logging/monitoring
                    await _mqttService.PublishAsync($"nexushome/matter/{nodeId}/command", new
                    {
                        nodeId,
                        clusterId,
                        commandId,
                        timestamp = DateTime.UtcNow,
                        success = true
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending command to node {NodeId}", nodeId);
                return false;
            }
        }

        public async Task<MatterAttributeValue> ReadAttributeAsync(ulong nodeId, uint clusterId, uint attributeId)
        {
            try
            {
                if (!_commissionedDevices.ContainsKey(nodeId))
                {
                    throw new ArgumentException($"Device with node ID {nodeId} not found");
                }

                _logger.LogDebug("Reading attribute {AttributeId} from cluster {ClusterId} on node {NodeId}", 
                    attributeId, clusterId, nodeId);

                // Create read attribute message
                var readMessage = CreateReadAttributeMessage(nodeId, clusterId, attributeId);
                
                // Send read request and wait for response
                var response = await SendMatterMessageWithResponseAsync(readMessage);
                
                if (response != null)
                {
                    // Parse attribute value from response
                    var attributeValue = ParseAttributeValue(response);
                    
                    // Update device last seen timestamp
                    if (_commissionedDevices.TryGetValue(nodeId, out var device))
                    {
                        device.LastSeen = DateTime.UtcNow;
                        device.IsOnline = true;
                    }

                    return attributeValue;
                }

                return new MatterAttributeValue { Success = false, ErrorMessage = "No response received" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading attribute {AttributeId} from node {NodeId}", attributeId, nodeId);
                throw;
            }
        }

        public async Task<bool> WriteAttributeAsync(ulong nodeId, uint clusterId, uint attributeId, object value)
        {
            try
            {
                if (!_commissionedDevices.ContainsKey(nodeId))
                {
                    _logger.LogWarning("Attempt to write attribute to unknown device {NodeId}", nodeId);
                    return false;
                }

                _logger.LogDebug("Writing attribute {AttributeId} to cluster {ClusterId} on node {NodeId}", 
                    attributeId, clusterId, nodeId);

                // Create write attribute message
                var writeMessage = CreateWriteAttributeMessage(nodeId, clusterId, attributeId, value);
                
                // Send write request
                var success = await SendMatterMessageAsync(writeMessage);
                
                if (success)
                {
                    // Update device last seen timestamp
                    if (_commissionedDevices.TryGetValue(nodeId, out var device))
                    {
                        device.LastSeen = DateTime.UtcNow;
                        device.IsOnline = true;
                    }

                    // Publish attribute change to MQTT
                    await _mqttService.PublishAsync($"nexushome/matter/{nodeId}/attribute", new
                    {
                        nodeId,
                        clusterId,
                        attributeId,
                        value,
                        timestamp = DateTime.UtcNow,
                        operation = "write"
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing attribute {AttributeId} to node {NodeId}", attributeId, nodeId);
                return false;
            }
        }

        public async Task<bool> SubscribeToAttributeAsync(ulong nodeId, uint clusterId, uint attributeId, TimeSpan reportingInterval)
        {
            try
            {
                if (!_commissionedDevices.ContainsKey(nodeId))
                {
                    _logger.LogWarning("Attempt to subscribe to attribute on unknown device {NodeId}", nodeId);
                    return false;
                }

                _logger.LogDebug("Subscribing to attribute {AttributeId} on cluster {ClusterId} of node {NodeId}", 
                    attributeId, clusterId, nodeId);

                // Create subscription message
                var subscribeMessage = CreateSubscribeMessage(nodeId, clusterId, attributeId, reportingInterval);
                
                // Send subscription request
                var success = await SendMatterMessageAsync(subscribeMessage);

                if (success)
                {
                    // Store subscription info for management
                    var subscriptionKey = $"{nodeId}:{clusterId}:{attributeId}";
                    
                    // Add to subscription tracking (implementation would include proper tracking)
                    
                    _logger.LogInformation("Successfully subscribed to attribute {AttributeId} on node {NodeId}", 
                        attributeId, nodeId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to attribute {AttributeId} on node {NodeId}", 
                    attributeId, nodeId);
                return false;
            }
        }

        public async Task<List<MatterFabric>> GetFabricsAsync()
        {
            try
            {
                var fabrics = new List<MatterFabric>
                {
                    new MatterFabric
                    {
                        FabricId = _fabricId,
                        FabricLabel = "NexusHome Main Fabric",
                        RootPublicKey = "Matter Root Public Key", // Would be actual key
                        VendorId = _vendorId,
                        NodeId = _nodeId,
                        IsActive = true,
                        CommissionedDeviceCount = _commissionedDevices.Count
                    }
                };

                return fabrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fabric information");
                return new List<MatterFabric>();
            }
        }

        public async Task<bool> RemoveDeviceAsync(ulong nodeId)
        {
            try
            {
                _logger.LogInformation("Removing Matter device {NodeId}", nodeId);

                if (!_commissionedDevices.TryGetValue(nodeId, out var device))
                {
                    _logger.LogWarning("Attempt to remove unknown device {NodeId}", nodeId);
                    return false;
                }

                // Send device removal command
                var removalSuccess = await SendDeviceRemovalCommandAsync(nodeId);
                
                if (removalSuccess)
                {
                    // Remove from local tracking
                    _commissionedDevices.Remove(nodeId);
                    
                    // Remove from database
                    await RemoveDeviceFromDatabaseAsync(nodeId);
                    
                    // Notify about decommissioning
                    DeviceDecommissioned?.Invoke(this, new MatterDeviceEventArgs { Device = device });
                    
                    _logger.LogInformation("Successfully removed Matter device {NodeId}", nodeId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing device {NodeId}", nodeId);
                return false;
            }
        }

        public async Task<MatterNetworkInfo> GetNetworkInfoAsync()
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .ToList();

                var wifiInterface = networkInterfaces
                    .FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
                
                var ethernetInterface = networkInterfaces
                    .FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet);

                return new MatterNetworkInfo
                {
                    FabricId = _fabricId,
                    NodeId = _nodeId,
                    OperationalDatasetPresent = true, // Would check actual Thread dataset
                    WiFiConnected = wifiInterface?.OperationalStatus == OperationalStatus.Up,
                    ThreadEnabled = _configuration.GetValue<bool>("Matter:EnableThread"),
                    IPv6Enabled = _configuration.GetValue<bool>("Matter:EnableIPv6"),
                    CommissionedDeviceCount = _commissionedDevices.Count,
                    ActiveSubscriptions = 0, // Would track actual subscriptions
                    LastNetworkActivity = _commissionedDevices.Values.Any() 
                        ? _commissionedDevices.Values.Max(d => d.LastSeen) 
                        : DateTime.MinValue
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting network information");
                throw;
            }
        }

        // Private helper methods for Matter operations

        private async Task InitializeMatterStackAsync()
        {
            // Initialize the Matter stack (would integrate with actual Matter SDK)
            // This includes:
            // - Setting up the Matter controller
            // - Configuring fabric information
            // - Initializing cryptographic components
            // - Setting up network interfaces

            _logger.LogDebug("Matter stack initialized");
            await Task.CompletedTask;
        }

        private async Task LoadCommissionedDevicesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                var devices = await context.Devices
                    .Where(d => d.Protocol == DeviceProtocol.Matter)
                    .ToListAsync();

                foreach (var device in devices)
                {
                    var matterDevice = ConvertToMatterDevice(device);
                    _commissionedDevices[matterDevice.NodeId] = matterDevice;
                }

                _logger.LogInformation("Loaded {Count} commissioned Matter devices", _commissionedDevices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading commissioned devices");
            }
        }

        private async Task StartMatterListenerAsync()
        {
            // Start listening for Matter messages on the appropriate network interfaces
            // This would set up UDP listeners for Matter-over-IP communication
            
            _logger.LogDebug("Matter message listener started");
            await Task.CompletedTask;
        }

        private void InitializeClusterHandlers()
        {
            // Initialize handlers for standard Matter clusters
            _clusterHandlers["on_off"] = new OnOffClusterHandler();
            _clusterHandlers["level_control"] = new LevelControlClusterHandler();
            _clusterHandlers["color_control"] = new ColorControlClusterHandler();
            _clusterHandlers["thermostat"] = new ThermostatClusterHandler();
            _clusterHandlers["door_lock"] = new DoorLockClusterHandler();
        }

        private async void PerformDeviceDiscovery(object? state)
        {
            if (!_isStarted) return;

            try
            {
                var discoveredDevices = await DiscoverDevicesAsync();
                
                if (discoveredDevices.Any())
                {
                    _logger.LogInformation("Device discovery found {Count} new devices", discoveredDevices.Count);
                    
                    // Publish discovery results to MQTT
                    await _mqttService.PublishAsync("nexushome/matter/discovery", new
                    {
                        timestamp = DateTime.UtcNow,
                        discoveredDevices = discoveredDevices.Count,
                        devices = discoveredDevices.Select(d => new
                        {
                            d.NodeId,
                            d.VendorId,
                            d.ProductId,
                            d.DeviceType,
                            d.DeviceName
                        })
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during device discovery");
            }
        }

        // Mock implementations for Matter SDK integration points
        // In a real implementation, these would interface with the actual Matter SDK

        private bool IsValidSetupCode(string setupCode)
        {
            // Validate Matter setup code format (11 digits for manual codes)
            return setupCode.Length == 11 && setupCode.All(char.IsDigit);
        }

        private Task<MatterCommissioningSession?> StartCommissioningSessionAsync(string setupCode, string discriminator)
        {
            // Mock implementation - would start actual commissioning session
            return Task.FromResult(new MatterCommissioningSession
            {
                SessionId = Guid.NewGuid().ToString(),
                SetupCode = setupCode,
                Discriminator = discriminator,
                StartTime = DateTime.UtcNow
            });
        }

        private ulong GenerateNodeId()
        {
            // Generate a unique node ID for the commissioned device
            return (ulong)DateTime.UtcNow.Ticks;
        }

        private MatterDevice ConvertToMatterDevice(Device device)
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(device.Configuration ?? "{}");
            
            return new MatterDevice
            {
                NodeId = config.TryGetValue("nodeId", out var nodeIdObj) 
                    ? Convert.ToUInt64(nodeIdObj) 
                    : (ulong)device.Id,
                VendorId = 0xFFF1, // Default vendor ID
                ProductId = 0x8000, // Default product ID
                DeviceType = ConvertDeviceTypeToMatter(device.Type),
                DeviceName = device.Name,
                FabricId = _fabricId,
                SupportedClusters = GetSupportedClustersForDeviceType(device.Type),
                NetworkType = MatterNetworkType.WiFi,
                IsOnline = device.IsOnline,
                CommissionedAt = device.CreatedAt,
                LastSeen = device.LastSeen
            };
        }

        private uint ConvertDeviceTypeToMatter(DeviceType deviceType)
        {
            return deviceType switch
            {
                DeviceType.SmartLight => 0x0100, // On/Off Light
                DeviceType.SmartThermostat => 0x0301, // Thermostat
                DeviceType.SmartLock => 0x000A, // Door Lock
                DeviceType.SmartSwitch => 0x0103, // On/Off Light Switch
                DeviceType.MotionSensor => 0x0015, // Occupancy Sensor
                DeviceType.DoorSensor => 0x0016, // Contact Sensor
                _ => 0x0000 // Unknown device type
            };
        }

        private List<uint> GetSupportedClustersForDeviceType(DeviceType deviceType)
        {
            var clusters = new List<uint> { 0x001D, 0x0028, 0x002A }; // Basic clusters

            switch (deviceType)
            {
                case DeviceType.SmartLight:
                    clusters.AddRange(new uint[] { 0x0006, 0x0008, 0x0300 }); // OnOff, LevelControl, ColorControl
                    break;
                case DeviceType.SmartThermostat:
                    clusters.AddRange(new uint[] { 0x0201, 0x0202, 0x0204 }); // Thermostat, Fan Control, Temperature Measurement
                    break;
                case DeviceType.SmartLock:
                    clusters.Add(0x0101); // Door Lock
                    break;
                case DeviceType.SmartSwitch:
                    clusters.Add(0x0006); // OnOff
                    break;
            }

            return clusters;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _discoveryTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        // Additional helper methods would be implemented here for complete Matter functionality
        private Task ShutdownMatterStackAsync() => Task.CompletedTask;
        private Task CloseDeviceConnectionAsync(ulong nodeId) => Task.CompletedTask;
        private Task<PaseResult> PerformPaseHandshakeAsync(MatterCommissioningSession session) => 
            Task.FromResult(new PaseResult { Success = true });
        private Task<MatterDeviceInfo> ReadDeviceInformationAsync(MatterCommissioningSession session) => 
            Task.FromResult(new MatterDeviceInfo());
        private Task<NetworkConfigResult> ConfigureNetworkCredentialsAsync(MatterCommissioningSession session) => 
            Task.FromResult(new NetworkConfigResult { Success = true, NetworkType = MatterNetworkType.WiFi });
        private Task<CertificateResult> InstallOperationalCertificatesAsync(MatterCommissioningSession session, ulong nodeId) => 
            Task.FromResult(new CertificateResult { Success = true });
        private Task<CommissioningResult> CompleteCommissioningAsync(MatterCommissioningSession session) => 
            Task.FromResult(new CommissioningResult { Success = true });
        private Task SaveCommissionedDeviceAsync(MatterDevice device) => Task.CompletedTask;
        private Task SetupDeviceSubscriptionsAsync(MatterDevice device) => Task.CompletedTask;
        private Task<List<MatterDevice>> PerformMdnsDiscoveryAsync() => Task.FromResult(new List<MatterDevice>());
        private Task<List<MatterDevice>> PerformThreadDiscoveryAsync() => Task.FromResult(new List<MatterDevice>());
        private MatterMessage CreateMatterMessage(ulong nodeId, uint clusterId, uint commandId, byte[] payload) => new();
        private Task<bool> SendMatterMessageAsync(MatterMessage message) => Task.FromResult(true);
        private MatterMessage CreateReadAttributeMessage(ulong nodeId, uint clusterId, uint attributeId) => new();
        private Task<MatterMessage?> SendMatterMessageWithResponseAsync(MatterMessage message) => Task.FromResult<MatterMessage?>(new MatterMessage());
        private MatterAttributeValue ParseAttributeValue(MatterMessage response) => new() { Success = true };
        private MatterMessage CreateWriteAttributeMessage(ulong nodeId, uint clusterId, uint attributeId, object value) => new();
        private MatterMessage CreateSubscribeMessage(ulong nodeId, uint clusterId, uint attributeId, TimeSpan interval) => new();
        private Task<bool> SendDeviceRemovalCommandAsync(ulong nodeId) => Task.FromResult(true);
        private Task RemoveDeviceFromDatabaseAsync(ulong nodeId) => Task.CompletedTask;
    }

    // Data classes for Matter functionality
    public class MatterDevice
    {
        public ulong NodeId { get; set; }
        public uint VendorId { get; set; }
        public uint ProductId { get; set; }
        public uint DeviceType { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public ulong FabricId { get; set; }
        public List<uint> SupportedClusters { get; set; } = new();
        public MatterNetworkType NetworkType { get; set; }
        public bool IsOnline { get; set; }
        public DateTime CommissionedAt { get; set; }
        public DateTime LastSeen { get; set; }
    }

    public class MatterDeviceInfo
    {
        public ulong NodeId { get; set; }
        public uint VendorId { get; set; }
        public uint ProductId { get; set; }
        public uint DeviceType { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string HardwareVersion { get; set; } = string.Empty;
        public string SoftwareVersion { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public List<uint> SupportedClusters { get; set; } = new();
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
    }

    public class MatterCommissioningResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public ulong NodeId { get; set; }
        public MatterDevice? DeviceInfo { get; set; }
        public DateTime CommissioningTime { get; set; }
    }

    public class MatterAttributeValue
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public object? Value { get; set; }
        public string? StringValue => Value?.ToString();
        public int? IntValue => Value as int?;
        public bool? BoolValue => Value as bool?;
        public float? FloatValue => Value as float?;
    }

    public class MatterFabric
    {
        public ulong FabricId { get; set; }
        public string FabricLabel { get; set; } = string.Empty;
        public string RootPublicKey { get; set; } = string.Empty;
        public uint VendorId { get; set; }
        public ulong NodeId { get; set; }
        public bool IsActive { get; set; }
        public int CommissionedDeviceCount { get; set; }
    }

    public class MatterNetworkInfo
    {
        public ulong FabricId { get; set; }
        public ulong NodeId { get; set; }
        public bool OperationalDatasetPresent { get; set; }
        public bool WiFiConnected { get; set; }
        public bool ThreadEnabled { get; set; }
        public bool IPv6Enabled { get; set; }
        public int CommissionedDeviceCount { get; set; }
        public int ActiveSubscriptions { get; set; }
        public DateTime LastNetworkActivity { get; set; }
    }

    // Event argument classes
    public class MatterDeviceEventArgs : EventArgs
    {
        public MatterDevice Device { get; set; } = new();
    }

    public class MatterAttributeEventArgs : EventArgs
    {
        public ulong NodeId { get; set; }
        public uint ClusterId { get; set; }
        public uint AttributeId { get; set; }
        public object? NewValue { get; set; }
        public object? OldValue { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // Supporting classes and enums
    public enum MatterNetworkType
    {
        WiFi,
        Thread,
        Ethernet
    }

    // Abstract base class for cluster handlers
    public abstract class MatterClusterHandler
    {
        public abstract Task<bool> HandleCommandAsync(uint commandId, byte[] payload);
        public abstract Task<MatterAttributeValue> ReadAttributeAsync(uint attributeId);
        public abstract Task<bool> WriteAttributeAsync(uint attributeId, object value);
    }

    // Specific cluster handler implementations
    public class OnOffClusterHandler : MatterClusterHandler
    {
        public override async Task<bool> HandleCommandAsync(uint commandId, byte[] payload)
        {
            // Handle On/Off cluster commands (On, Off, Toggle)
            return await Task.FromResult(true);
        }

        public override async Task<MatterAttributeValue> ReadAttributeAsync(uint attributeId)
        {
            // Read On/Off cluster attributes
            return await Task.FromResult(new MatterAttributeValue { Success = true, Value = true });
        }

        public override async Task<bool> WriteAttributeAsync(uint attributeId, object value)
        {
            // Write On/Off cluster attributes
            return await Task.FromResult(true);
        }
    }

    public class LevelControlClusterHandler : MatterClusterHandler
    {
        public override async Task<bool> HandleCommandAsync(uint commandId, byte[] payload)
        {
            return await Task.FromResult(true);
        }

        public override async Task<MatterAttributeValue> ReadAttributeAsync(uint attributeId)
        {
            return await Task.FromResult(new MatterAttributeValue { Success = true, Value = 128 });
        }

        public override async Task<bool> WriteAttributeAsync(uint attributeId, object value)
        {
            return await Task.FromResult(true);
        }
    }

    public class ColorControlClusterHandler : MatterClusterHandler
    {
        public override async Task<bool> HandleCommandAsync(uint commandId, byte[] payload)
        {
            return await Task.FromResult(true);
        }

        public override async Task<MatterAttributeValue> ReadAttributeAsync(uint attributeId)
        {
            return await Task.FromResult(new MatterAttributeValue { Success = true, Value = 0 });
        }

        public override async Task<bool> WriteAttributeAsync(uint attributeId, object value)
        {
            return await Task.FromResult(true);
        }
    }

    public class ThermostatClusterHandler : MatterClusterHandler
    {
        public override async Task<bool> HandleCommandAsync(uint commandId, byte[] payload)
        {
            return await Task.FromResult(true);
        }

        public override async Task<MatterAttributeValue> ReadAttributeAsync(uint attributeId)
        {
            return await Task.FromResult(new MatterAttributeValue { Success = true, Value = 2200 }); // 22.00°C
        }

        public override async Task<bool> WriteAttributeAsync(uint attributeId, object value)
        {
            return await Task.FromResult(true);
        }
    }

    public class DoorLockClusterHandler : MatterClusterHandler
    {
        public override async Task<bool> HandleCommandAsync(uint commandId, byte[] payload)
        {
            return await Task.FromResult(true);
        }

        public override async Task<MatterAttributeValue> ReadAttributeAsync(uint attributeId)
        {
            return await Task.FromResult(new MatterAttributeValue { Success = true, Value = false }); // Unlocked
        }

        public override async Task<bool> WriteAttributeAsync(uint attributeId, object value)
        {
            return await Task.FromResult(true);
        }
    }

    // Internal classes for Matter SDK integration
    internal class MatterCommissioningSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string SetupCode { get; set; } = string.Empty;
        public string Discriminator { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
    }

    internal class MatterMessage
    {
        public ulong NodeId { get; set; }
        public uint ClusterId { get; set; }
        public uint CommandId { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    internal class PaseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    internal class NetworkConfigResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public MatterNetworkType NetworkType { get; set; }
    }

    internal class CertificateResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    internal class CommissioningResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}