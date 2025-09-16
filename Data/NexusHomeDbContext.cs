using Microsoft.EntityFrameworkCore;
using NexusHome.IoT.Models;

namespace NexusHome.IoT.Data
{
    public class NexusHomeDbContext : DbContext
    {
        public NexusHomeDbContext(DbContextOptions<NexusHomeDbContext> options) : base(options)
        {
        }

        // Device and IoT related tables
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

            // --- Entity Configurations ---

            // Device Configuration
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasIndex(e => e.DeviceId).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // Energy Consumption Configuration
            modelBuilder.Entity<EnergyConsumption>(entity =>
            {
                entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
                entity.HasOne(e => e.Device)
                    .WithMany(d => d.EnergyConsumptions)
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // --- Seeding Data ---
            SeedUserData(modelBuilder);
            SeedDeviceData(modelBuilder);
        }
        
        private void SeedUserData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    Email = "admin@nexushome.dev",
                    FirstName = "System",
                    LastName = "Administrator",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPass123!"),
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Preferences = "{ \"theme\": \"dark\" }"
                }
            );
        }

        private void SeedDeviceData(ModelBuilder modelBuilder)
        {
             modelBuilder.Entity<Device>().HasData(
                new Device { Id = 1, DeviceId = "LIVING_ROOM_LIGHT", Name = "Living Room Light", Type = DeviceType.SmartLight, Room = "Living Room", IsOnline = true, Status = DeviceStatus.Active, LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Manufacturer = "Philips", Model = "Hue" },
                new Device { Id = 2, DeviceId = "LIVING_ROOM_THERMOSTAT", Name = "Living Room Thermostat", Type = DeviceType.SmartThermostat, Room = "Living Room", IsOnline = true, Status = DeviceStatus.Active, LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Manufacturer = "Nest", Model = "Learning" },
                new Device { Id = 3, DeviceId = "HALLWAY_MOTION_SENSOR", Name = "Hallway Motion Sensor", Type = DeviceType.MotionSensor, Room = "Hallway", IsOnline = true, Status = DeviceStatus.Active, LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Manufacturer = "Samsung", Model = "SmartThings" },
                new Device { Id = 4, DeviceId = "BEDROOM_LIGHT", Name = "Bedroom Light", Type = DeviceType.SmartLight, Room = "Bedroom", IsOnline = false, Status = DeviceStatus.Offline, LastSeen = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Manufacturer = "Philips", Model = "Hue" }
            );
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is Device && (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entityEntry in entries)
            {
                ((Device)entityEntry.Entity).UpdatedAt = DateTime.UtcNow;

                if (entityEntry.State == EntityState.Added)
                {
                    ((Device)entityEntry.Entity).CreatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
