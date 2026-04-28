using IoTMonitoringPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTMonitoringPlatform.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices { get; set; }
    public DbSet<DeviceData> DeviceData { get; set; }
    public DbSet<Alarm> Alarms { get; set; }
    public DbSet<AlarmRule> AlarmRules { get; set; }
    public DbSet<ControlCommand> ControlCommands { get; set; }
    public DbSet<DeviceGroup> DeviceGroups { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>()
            .HasIndex(d => d.SerialNumber)
            .IsUnique();

        modelBuilder.Entity<Device>()
            .HasIndex(d => d.Status);

        modelBuilder.Entity<DeviceData>()
            .HasIndex(d => new { d.DeviceId, d.Timestamp });

        modelBuilder.Entity<DeviceData>()
            .HasIndex(d => d.Metric);

        modelBuilder.Entity<Alarm>()
            .HasIndex(a => new { a.DeviceId, a.TriggeredAt });

        modelBuilder.Entity<Alarm>()
            .HasIndex(a => a.Status);

        modelBuilder.Entity<Alarm>()
            .HasIndex(a => a.Level);

        modelBuilder.Entity<AlarmRule>()
            .HasIndex(r => r.IsEnabled);

        modelBuilder.Entity<ControlCommand>()
            .HasIndex(c => new { c.DeviceId, c.CreatedAt });

        modelBuilder.Entity<ControlCommand>()
            .HasIndex(c => c.Status);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Device>()
            .Property(d => d.Status)
            .HasConversion<string>();

        modelBuilder.Entity<DeviceData>()
            .Property(d => d.Quality)
            .HasConversion<string>();

        modelBuilder.Entity<Alarm>()
            .Property(a => a.Level)
            .HasConversion<string>();

        modelBuilder.Entity<Alarm>()
            .Property(a => a.Status)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmRule>()
            .Property(r => r.Operator)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmRule>()
            .Property(r => r.AlarmLevel)
            .HasConversion<string>();

        modelBuilder.Entity<ControlCommand>()
            .Property(c => c.Status)
            .HasConversion<string>();

        modelBuilder.Entity<ControlCommand>()
            .Property(c => c.Priority)
            .HasConversion<string>();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();
    }
}
