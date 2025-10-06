using Microsoft.EntityFrameworkCore;
using NexusHome.IoT.Core.Domain;

namespace NexusHome.IoT.Infrastructure.Data;

public class SmartHomeDbContext : DbContext
{
    public SmartHomeDbContext(DbContextOptions<SmartHomeDbContext> options) : base(options)
    {
    }

    // Core entities
    public DbSet<SmartDevice> SmartDevices { get; set; }
    public DbSet<EnergyReading> EnergyReadings { get; set; }
    public DbSet<AutomationRule> AutomationRules { get; set; }
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<DeviceGroup> DeviceGroups { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure SmartDevice entity
        modelBuilder.Entity<SmartDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DeviceType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Location).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasIndex(e => e.DeviceId).IsUnique();
            
            entity.HasOne(e => e.Room)
                .WithMany(r => r.Devices)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure EnergyReading entity
        modelBuilder.Entity<EnergyReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PowerConsumption).HasColumnType("decimal(10,4)");
            entity.Property(e => e.Voltage).HasColumnType("decimal(8,2)");
            entity.Property(e => e.Current).HasColumnType("decimal(8,4)");
            entity.Property(e => e.Cost).HasColumnType("decimal(10,4)");
            
            entity.HasOne(e => e.Device)
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure Room entity
        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Seed initial data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Seed rooms
        modelBuilder.Entity<Room>().HasData(
            new Room { Id = 1, Name = "Living Room", Description = "Main living area" },
            new Room { Id = 2, Name = "Kitchen", Description = "Cooking and dining area" },
            new Room { Id = 3, Name = "Bedroom", Description = "Master bedroom" },
            new Room { Id = 4, Name = "Bathroom", Description = "Main bathroom" },
            new Room { Id = 5, Name = "Garage", Description = "Storage and parking area" }
        );

        // Seed admin user
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                Email = "admin@nexushome.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = "Administrator",
                FirstName = "System",
                LastName = "Administrator",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );
    }
}
