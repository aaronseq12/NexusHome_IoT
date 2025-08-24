using Microsoft.EntityFrameworkCore;
using NexusHome.Models;

namespace NexusHome.Data
{
    public class NexusHomeDbContext : DbContext
    {
        public NexusHomeDbContext(DbContextOptions<NexusHomeDbContext> options) : base(options)
        {
        }

        // Device and IoT
        public DbSet<Device> Devices { get; set; }
        public DbSet<EnergyConsumption> EnergyConsumptions { get; set; }
        public DbSet<SolarGeneration> SolarGenerations { get; set; }
        public DbSet<BatteryStatus> BatteryStatuses { get; set; }
        
        // Automation and Rules
        public DbSet<AutomationRule> AutomationRules { get; set; }
        public DbSet<EnergyOptimizationRule> EnergyOptimizationRules { get; set; }
        
        // Maintenance and Alerts
        public DbSet<DeviceMaintenanceRecord> MaintenanceRecords { get; set; }
        public DbSet<DeviceAlert> DeviceAlerts { get; set; }
        
        // Users and Security
        public DbSet<User> Users { get; set; }
        
        // Weather and Environmental
        public DbSet<WeatherData> WeatherData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Device configuration
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasIndex(e => e.DeviceId).IsUnique();
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.IsOnline);
                entity.HasIndex(e => e.LastSeen);
                
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsOnline).HasDefaultValue(false);
                entity.Property(e => e.Status).HasDefaultValue(DeviceStatus.Inactive);
            });

            // Energy Consumption configuration
            modelBuilder.Entity<EnergyConsumption>(entity =>
            {
                entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Source);
                
                entity.HasOne(e => e.Device)
                    .WithMany(d => d.EnergyConsumptions)
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");
            });

            // Solar Generation configuration
            modelBuilder.Entity<SolarGeneration>(entity =>
            {
                entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
                entity.HasIndex(e => e.Timestamp);
                
                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");
            });

            // Battery Status configuration  
            modelBuilder.Entity<BatteryStatus>(entity =>
            {
                entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Mode);
                
                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");
            });

            // Automation Rule configuration
            modelBuilder.Entity<AutomationRule>(entity =>
            {
                entity.HasIndex(e => e.IsEnabled);
                entity.HasIndex(e => e.Trigger);
                entity.HasIndex(e => e.Priority);
                entity.HasIndex(e => e.LastExecuted);
                
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.Priority).HasDefaultValue(0);
                entity.Property(e => e.ExecutionCount).HasDefaultValue(0);
            });

            // Device Maintenance Record configuration
            modelBuilder.Entity<DeviceMaintenanceRecord>(entity =>
            {
                entity.HasIndex(e => new { e.DeviceId, e.Status });
                entity.HasIndex(e => e.ScheduledDate);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Priority);
                entity.HasIndex(e => e.Type);
                
                entity.HasOne(e => e.Device)
                    .WithMany(d => d.MaintenanceRecords)
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Status).HasDefaultValue(MaintenanceStatus.Scheduled);
                entity.Property(e => e.Priority).HasDefaultValue(MaintenancePriority.Medium);
            });

            // Device Alert configuration
            modelBuilder.Entity<DeviceAlert>(entity =>
            {
                entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.Severity);
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.IsResolved);
                entity.HasIndex(e => e.Timestamp);
                
                entity.HasOne(e => e.Device)
                    .WithMany(d => d.Alerts)
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                entity.Property(e => e.IsResolved).HasDefaultValue(false);
            });

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Role);
                entity.HasIndex(e => e.IsActive);
                
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.Role).HasDefaultValue(UserRole.User);
            });

            // Energy Optimization Rule configuration
            modelBuilder.Entity<EnergyOptimizationRule>(entity =>
            {
                entity.HasIndex(e => e.IsEnabled);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.Priority);
                entity.HasIndex(e => e.LastExecuted);
                
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.Priority).HasDefaultValue(0);
                entity.Property(e => e.TotalSavings).HasDefaultValue(0);
            });

            // Weather Data configuration
            modelBuilder.Entity<WeatherData>(entity =>
            {
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.IsForecast);
                entity.HasIndex(e => new { e.IsForecast, e.Timestamp });
                
                entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsForecast).HasDefaultValue(false);
            });

            // Configure many-to-many relationship between AutomationRule and Device
            modelBuilder.Entity<AutomationRule>()
                .HasMany(ar => ar.Devices)
                .WithMany(d => d.AutomationRules)
                .UsingEntity<Dictionary<string, object>>(
                    "AutomationRuleDevice",
                    ar => ar.HasOne<Device>().WithMany().HasForeignKey("DeviceId"),
                    d => d.HasOne<AutomationRule>().WithMany().HasForeignKey("AutomationRuleId"),
                    je =>
                    {
                        je.HasKey("AutomationRuleId", "DeviceId");
                        je.HasIndex("AutomationRuleId");
                        je.HasIndex("DeviceId");
                    });

            // Seed initial data
            SeedData(modelBuilder);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            // Seed default admin user
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    Email = "admin@nexushome.com",
                    FirstName = "System",
                    LastName = "Administrator",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Preferences = "{ \"theme\": \"dark\", \"notifications\": true, \"autoOptimization\": true }"
                }
            );

            // Seed default devices
            var defaultDevices = new[]
            {
                new Device
                {
                    Id = 1,
                    DeviceId = "NEST_THERMOSTAT_001",
                    Name = "Living Room Thermostat",
                    Description = "Smart thermostat for main living area",
                    Type = DeviceType.SmartThermostat,
                    Manufacturer = "Google Nest",
                    Model = "Learning Thermostat 4th Gen",
                    FirmwareVersion = "6.2.1",
                    Protocol = DeviceProtocol.Matter,
                    Status = DeviceStatus.Active,
                    Location = "Living Room",
                    Room = "Living Room",
                    PowerRating = 3.5m,
                    CurrentPowerConsumption = 0,
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    MqttTopic = "nexushome/devices/NEST_THERMOSTAT_001/data",
                    Configuration = "{ \"targetTemperature\": 22, \"mode\": \"auto\", \"learningEnabled\": true }"
                },
                new Device
                {
                    Id = 2,
                    DeviceId = "SOLAR_INVERTER_001",
                    Name = "Main Solar Inverter",
                    Description = "Primary solar panel inverter system",
                    Type = DeviceType.SolarInverter,
                    Manufacturer = "SolarEdge",
                    Model = "SE7600H-US",
                    FirmwareVersion = "4.18.7",
                    Protocol = DeviceProtocol.Modbus,
                    Status = DeviceStatus.Active,
                    Location = "Garage",
                    Room = "Garage",
                    PowerRating = 7600m,
                    CurrentPowerConsumption = 15,
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    MqttTopic = "nexushome/solar/SOLAR_INVERTER_001/generation",
                    Configuration = "{ \"maxPowerOutput\": 7600, \"optimizationEnabled\": true }"
                },
                new Device
                {
                    Id = 3,
                    DeviceId = "TESLA_POWERWALL_001",
                    Name = "Home Battery Storage",
                    Description = "Tesla Powerwall 2 for energy storage",
                    Type = DeviceType.BatteryStorage,
                    Manufacturer = "Tesla",
                    Model = "Powerwall 2",
                    FirmwareVersion = "23.12.10",
                    Protocol = DeviceProtocol.WiFi,
                    Status = DeviceStatus.Active,
                    Location = "Garage",
                    Room = "Garage",
                    PowerRating = 13500m,
                    CurrentPowerConsumption = 5,
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    MqttTopic = "nexushome/battery/TESLA_POWERWALL_001/status",
                    Configuration = "{ \"capacity\": 13.5, \"mode\": \"backup\", \"reservePercent\": 20 }"
                }
            };

            modelBuilder.Entity<Device>().HasData(defaultDevices);

            // Seed default automation rules
            var defaultRules = new[]
            {
                new AutomationRule
                {
                    Id = 1,
                    Name = "Peak Hour Energy Optimization",
                    Description = "Reduce non-essential device consumption during peak hours",
                    IsEnabled = true,
                    Trigger = AutomationTrigger.TimeSchedule,
                    TriggerConditions = "{ \"timeRange\": { \"start\": \"17:00\", \"end\": \"21:00\" }, \"days\": [\"Monday\", \"Tuesday\", \"Wednesday\", \"Thursday\", \"Friday\"] }",
                    Actions = "{ \"actions\": [{ \"type\": \"dimLights\", \"value\": 80 }, { \"type\": \"adjustThermostat\", \"value\": 1 }] }",
                    Priority = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExecutionCount = 0
                },
                new AutomationRule
                {
                    Id = 2,
                    Name = "Solar Optimization",
                    Description = "Maximize solar energy utilization during peak generation",
                    IsEnabled = true,
                    Trigger = AutomationTrigger.SolarGeneration,
                    TriggerConditions = "{ \"solarGeneration\": { \"threshold\": 5000 }, \"batteryLevel\": { \"min\": 20 } }",
                    Actions = "{ \"actions\": [{ \"type\": \"chargeBattery\", \"priority\": \"high\" }, { \"type\": \"runHighEnergyAppliances\" }] }",
                    Priority = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExecutionCount = 0
                }
            };

            modelBuilder.Entity<AutomationRule>().HasData(defaultRules);

            // Seed default energy optimization rules
            var optimizationRules = new[]
            {
                new EnergyOptimizationRule
                {
                    Id = 1,
                    Name = "Load Shifting",
                    Description = "Shift energy-intensive tasks to off-peak hours",
                    IsEnabled = true,
                    Type = OptimizationType.LoadShifting,
                    TargetSavingsPercent = 15,
                    ComfortThreshold = 2,
                    Conditions = "{ \"peakHours\": [17, 18, 19, 20], \"deferableLoads\": [\"WashingMachine\", \"Dryer\", \"Dishwasher\"] }",
                    Actions = "{ \"defer\": { \"maxHours\": 4 }, \"reschedule\": true }",
                    Priority = 1,
                    CreatedAt = DateTime.UtcNow,
                    TotalSavings = 0
                },
                new EnergyOptimizationRule
                {
                    Id = 2,
                    Name = "Battery Optimization",
                    Description = "Optimize battery charging and discharging cycles",
                    IsEnabled = true,
                    Type = OptimizationType.BatteryOptimization,
                    TargetSavingsPercent = 20,
                    ComfortThreshold = 1,
                    Conditions = "{ \"batteryCapacity\": 13.5, \"minimumReserve\": 20, \"maxChargingRate\": 5 }",
                    Actions = "{ \"charge\": { \"offPeakOnly\": true }, \"discharge\": { \"peakHoursOnly\": true } }",
                    Priority = 1,
                    CreatedAt = DateTime.UtcNow,
                    TotalSavings = 0
                }
            };

            modelBuilder.Entity<EnergyOptimizationRule>().HasData(optimizationRules);
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries<Device>()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }

            var automationEntries = ChangeTracker.Entries<AutomationRule>()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in automationEntries)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    // Repository Pattern Implementation
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> GetAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate);
        Task<T> AddAsync(T entity);
        Task<T> UpdateAsync(T entity);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
    }

    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly NexusHomeDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(NexusHomeDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task<T> UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task DeleteAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public virtual async Task<bool> ExistsAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            return entity != null;
        }
    }
}