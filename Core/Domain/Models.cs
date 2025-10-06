using System.ComponentModel.DataAnnotations;

namespace NexusHome.IoT.Core.Domain;

public class SmartDevice
{
    public int Id { get; set; }
    
    [Required]
    public string DeviceId { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string DeviceType { get; set; } = string.Empty;
    
    public string? Location { get; set; }
    public string? IpAddress { get; set; }
    public bool IsOnline { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    
    // Device-specific properties stored as JSON
    public string? Properties { get; set; }
    public string? Configuration { get; set; }
}

public class EnergyReading
{
    public int Id { get; set; }
    
    [Required]
    public string DeviceId { get; set; } = string.Empty;
    
    public decimal PowerConsumption { get; set; } // in Watts
    public decimal Voltage { get; set; } // in Volts
    public decimal Current { get; set; } // in Amperes
    public decimal Cost { get; set; } // in currency units
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public SmartDevice? Device { get; set; }
}

public class AutomationRule
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public string TriggerCondition { get; set; } = string.Empty; // JSON format
    
    [Required]
    public string Action { get; set; } = string.Empty; // JSON format
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastExecuted { get; set; }
    public int ExecutionCount { get; set; } = 0;
}

public class MaintenanceRecord
{
    public int Id { get; set; }
    
    [Required]
    public string DeviceId { get; set; } = string.Empty;
    
    public string MaintenanceType { get; set; } = string.Empty; // "Scheduled", "Predictive", "Emergency"
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // "Pending", "InProgress", "Completed", "Cancelled"
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? TechnicianNotes { get; set; }
    public decimal Cost { get; set; }
    
    // Navigation property
    public SmartDevice? Device { get; set; }
}

public class User
{
    public int Id { get; set; }
    
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    public string Role { get; set; } = "User"; // "User", "Administrator", "Technician"
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public class Room
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    public double? SquareFootage { get; set; }
    public string? Floor { get; set; }
    
    // Navigation properties
    public ICollection<SmartDevice> Devices { get; set; } = new List<SmartDevice>();
}

public class DeviceGroup
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    public string GroupType { get; set; } = "Custom"; // "Room", "Type", "Custom"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// DTOs for API responses
public record DeviceStatusDto(
    string DeviceId,
    string Name,
    string DeviceType,
    bool IsOnline,
    DateTime LastSeen,
    Dictionary<string, object>? Properties = null);

public record EnergyDataDto(
    string DeviceId,
    decimal PowerConsumption,
    decimal Cost,
    DateTime Timestamp);

public record DeviceTelemetryRequest(
    string DeviceId,
    Dictionary<string, object> SensorData,
    DateTime Timestamp);

// Configuration classes
public class JwtAuthenticationSettings
{
    public string SecretKey { get; set; } = "DefaultSecretKey-ChangeInProduction";
    public string Issuer { get; set; } = "NexusHome.IoT";
    public string Audience { get; set; } = "NexusHome.Clients";
    public int ExpirationInMinutes { get; set; } = 1440;
    public int RefreshTokenExpirationInDays { get; set; } = 7;
}

public class MqttBrokerSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientId { get; set; } = "NexusHome-Server";
    public Dictionary<string, string> Topics { get; set; } = new();
}

public class WeatherApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Units { get; set; } = "metric";
    public int RefreshIntervalMinutes { get; set; } = 15;
}
