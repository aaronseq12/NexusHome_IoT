using NexusHome.IoT.Core.Domain;

namespace NexusHome.IoT.Core.Services.Interfaces;

public interface ISmartDeviceManager
{
    Task<IEnumerable<SmartDevice>> GetAllDevicesAsync();
    Task<SmartDevice?> GetDeviceByIdAsync(string deviceId);
    Task<SmartDevice> AddDeviceAsync(SmartDevice device);
    Task<SmartDevice> UpdateDeviceAsync(SmartDevice device);
    Task<bool> DeleteDeviceAsync(string deviceId);
    Task<bool> ToggleDeviceAsync(string deviceId);
    Task ProcessTelemetryDataAsync(DeviceTelemetryRequest request);
    Task<IEnumerable<EnergyReading>> GetEnergyDataAsync(string deviceId, DateTime? from = null, DateTime? to = null);
}

public interface IEnergyConsumptionAnalyzer
{
    Task<decimal> CalculateTotalConsumptionAsync(DateTime from, DateTime to);
    Task<decimal> CalculateTotalCostAsync(DateTime from, DateTime to);
    Task<Dictionary<string, decimal>> GetConsumptionByDeviceAsync(DateTime from, DateTime to);
    Task<Dictionary<string, decimal>> GetConsumptionByRoomAsync(DateTime from, DateTime to);
}

public interface IAutomationRuleEngine
{
    Task<IEnumerable<AutomationRule>> GetAllRulesAsync();
    Task<AutomationRule> CreateRuleAsync(AutomationRule rule);
    Task<AutomationRule> UpdateRuleAsync(AutomationRule rule);
    Task<bool> DeleteRuleAsync(int ruleId);
    Task ProcessRulesAsync();
}

public interface IMqttClientService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);
    Task SubscribeAsync(string topic, CancellationToken cancellationToken = default);
    event Func<string, string, Task> MessageReceived;
}

public interface INotificationDispatcher
{
    Task SendNotificationAsync(string title, string message, string? userId = null);
    Task SendEmailNotificationAsync(string email, string subject, string body);
    Task SendRealTimeNotificationAsync(string connectionId, object data);
}

public interface IPredictiveMaintenanceEngine
{
    Task<double> PredictMaintenanceProbabilityAsync(string deviceId);
    Task<IEnumerable<MaintenanceRecord>> GetUpcomingMaintenanceAsync();
    Task ScheduleMaintenanceAsync(string deviceId, DateTime scheduledDate, string type);
}

public interface IEnergyOptimizationEngine
{
    Task<Dictionary<string, object>> OptimizeEnergyUsageAsync();
    Task<decimal> PredictEnergyConsumptionAsync(DateTime targetDate);
    Task<IEnumerable<string>> GetOptimizationRecommendationsAsync();
}

public interface IWeatherDataProvider
{
    Task<WeatherData> GetCurrentWeatherAsync(double latitude, double longitude);
    Task<IEnumerable<WeatherForecast>> GetForecastAsync(double latitude, double longitude, int days = 5);
}

public interface IUtilityPriceProvider
{
    Task<decimal> GetCurrentElectricityPriceAsync();
    Task<IEnumerable<HourlyPrice>> GetHourlyPricesAsync(DateTime date);
}

public interface IDataAggregationService
{
    Task<Dictionary<string, object>> GetDashboardDataAsync();
    Task<Dictionary<string, decimal>> GetEnergyStatisticsAsync(string period = "day");
    Task<IEnumerable<DeviceStatusDto>> GetDeviceStatusSummaryAsync();
}

public interface ISecurityManager
{
    Task<string> GenerateJwtTokenAsync(User user);
    Task<User?> ValidateCredentialsAsync(string username, string password);
    Task<bool> ValidateTokenAsync(string token);
    Task<User?> GetUserFromTokenAsync(string token);
}

// Additional DTOs
public record WeatherData(double Temperature, double Humidity, string Condition, double WindSpeed);
public record WeatherForecast(DateTime Date, double HighTemp, double LowTemp, string Condition);
public record HourlyPrice(DateTime Hour, decimal Price, string TariffType);
