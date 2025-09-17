using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexusHome.IoT.Models
{
    // --- Device Models ---
    public class Device
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string DeviceId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DeviceType Type { get; set; }

        [StringLength(50)]
        public string Manufacturer { get; set; } = string.Empty;

        [StringLength(50)]
        public string Model { get; set; } = string.Empty;

        [StringLength(50)]
        public string FirmwareVersion { get; set; } = string.Empty;

        public DeviceProtocol Protocol { get; set; }
        public DeviceStatus Status { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }

        [StringLength(50)]
        public string? Room { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal PowerRating { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal CurrentPowerConsumption { get; set; }

        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [StringLength(200)]
        public string? MqttTopic { get; set; }

        public string? Configuration { get; set; } // JSON configuration
        public string? Metadata { get; set; } // Additional JSON metadata

        // Navigation properties
        public virtual ICollection<EnergyConsumption> EnergyConsumptions { get; set; } = new List<EnergyConsumption>();
        public virtual ICollection<DeviceMaintenanceRecord> MaintenanceRecords { get; set; } = new List<DeviceMaintenanceRecord>();
        public virtual ICollection<DeviceAlert> Alerts { get; set; } = new List<DeviceAlert>();
        public virtual ICollection<AutomationRule> AutomationRules { get; set; } = new List<AutomationRule>();
    }

    public enum DeviceType
    {
        SmartThermostat, SmartLight, SmartSwitch, EnergyMeter, SolarPanel, SolarInverter,
        BatteryStorage, SmartPlug, MotionSensor, DoorSensor, WindowSensor, SmartLock,
        SecurityCamera, SmartSpeaker, AirConditioner, WaterHeater, Refrigerator,
        WashingMachine, Dryer, Dishwasher, ElectricVehicleCharger, HeatPump, SmartMeter,
        WeatherStation, Other
    }

    public enum DeviceProtocol { Matter, Zigbee, ZWave, WiFi, Bluetooth, Thread, Modbus, MQTT }
    public enum DeviceStatus { Active, Inactive, Maintenance, Error, Offline }


    // --- Energy Models ---
    public class EnergyConsumption
    {
        [Key]
        public int Id { get; set; }
        public int DeviceId { get; set; }
        [Column(TypeName = "decimal(10,4)")]
        public decimal PowerConsumption { get; set; } // kWh
        [Column(TypeName = "decimal(10,4)")]
        public decimal Voltage { get; set; }
        [Column(TypeName = "decimal(10,4)")]
        public decimal Current { get; set; }
        public DateTime Timestamp { get; set; }
        public EnergySource Source { get; set; }
        public virtual Device Device { get; set; } = null!;
    }

    public enum EnergySource { Grid, Solar, Battery, Other }

    public class SolarGeneration
    {
        [Key]
        public int Id { get; set; }
        public int DeviceId { get; set; }
        [Column(TypeName = "decimal(10,4)")]
        public decimal PowerGeneration { get; set; } // kWh
        public DateTime Timestamp { get; set; }
        public virtual Device Device { get; set; } = null!;
    }

    public class BatteryStatus
    {
        [Key]
        public int Id { get; set; }
        public int DeviceId { get; set; }
        [Column(TypeName = "decimal(8,2)")]
        public decimal ChargeLevel { get; set; } // Percentage
        public BatteryMode Mode { get; set; }
        public DateTime Timestamp { get; set; }
        public virtual Device Device { get; set; } = null!;
    }

    public enum BatteryMode { Charging, Discharging, Standby, Backup }


    // --- Automation Models ---
    public class AutomationRule
    {
        [Key]
        public int Id { get; set; }
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string TriggerConditions { get; set; } = string.Empty; // JSON
        public string Actions { get; set; } = string.Empty; // JSON
        public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
    }

    public class EnergyOptimizationRule
    {
        [Key]
        public int Id { get; set; }
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public OptimizationType Type { get; set; }
        public string Conditions { get; set; } = string.Empty; // JSON
        public string Actions { get; set; } = string.Empty; // JSON
    }

    public enum OptimizationType { LoadShifting, PeakShaving, DemandResponse, SolarOptimization, BatteryOptimization, CostOptimization }

    // --- Maintenance Models ---
    public class DeviceMaintenanceRecord
    {
        [Key]
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public MaintenanceType Type { get; set; }
        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;
        public MaintenanceStatus Status { get; set; }
        public MaintenancePriority Priority { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual Device Device { get; set; } = null!;
    }

    public enum MaintenanceType { Preventive, Predictive, Corrective, Emergency }
    public enum MaintenanceStatus { Scheduled, InProgress, Completed, Cancelled, Overdue }
    public enum MaintenancePriority { Low, Medium, High, Critical }

    // --- Alert Models ---
    public class DeviceAlert
    {
        [Key]
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public bool IsResolved { get; set; }
        public DateTime Timestamp { get; set; }
        public virtual Device Device { get; set; } = null!;
    }

    public enum AlertType { EnergyAnomaly, MaintenanceRequired, DeviceOffline, SecurityBreach, HighConsumption }
    public enum AlertSeverity { Info, Warning, Error, Critical }

    // --- User Models ---
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required, StringLength(100)]
        public string Username { get; set; } = string.Empty;
        [Required, StringLength(255)]
        public string Email { get; set; } = string.Empty;
        [Required, StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Preferences { get; set; } // JSON user preferences
    }

    public enum UserRole { Admin, User, Technician }

    // --- Weather Data Model ---
    public class WeatherData
    {
        [Key]
        public int Id { get; set; }
        [Column(TypeName = "decimal(5,2)")]
        public decimal Temperature { get; set; }
        [Column(TypeName = "decimal(6,2)")]
        public decimal SolarIrradiance { get; set; }
        [Column(TypeName = "decimal(5,2)")]
        public decimal CloudCover { get; set; }
        [StringLength(50)]
        public string? Condition { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsForecast { get; set; }
    }
}
