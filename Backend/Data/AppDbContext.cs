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

    public DbSet<DeviceHeartbeat> DeviceHeartbeats { get; set; }
    public DbSet<DeviceStateHistory> DeviceStateHistories { get; set; }
    public DbSet<DeviceConnection> DeviceConnections { get; set; }
    public DbSet<StateSyncPacket> StateSyncPackets { get; set; }

    public DbSet<AlarmDeduplication> AlarmDeduplications { get; set; }
    public DbSet<AlarmSuppression> AlarmSuppressions { get; set; }
    public DbSet<AlarmAggregation> AlarmAggregations { get; set; }
    public DbSet<AlarmRateLimit> AlarmRateLimits { get; set; }
    public DbSet<AlarmQueueItem> AlarmQueueItems { get; set; }
    public DbSet<AlarmBatch> AlarmBatches { get; set; }

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

        modelBuilder.Entity<DeviceHeartbeat>()
            .HasIndex(h => new { h.DeviceId, h.Timestamp });

        modelBuilder.Entity<DeviceHeartbeat>()
            .HasIndex(h => h.Status);

        modelBuilder.Entity<DeviceStateHistory>()
            .HasIndex(h => new { h.DeviceId, h.Timestamp });

        modelBuilder.Entity<DeviceConnection>()
            .HasIndex(c => c.ConnectionId)
            .IsUnique();

        modelBuilder.Entity<DeviceConnection>()
            .HasIndex(c => new { c.DeviceId, c.IsActive });

        modelBuilder.Entity<StateSyncPacket>()
            .HasIndex(p => new { p.DeviceId, p.Timestamp });

        modelBuilder.Entity<AlarmDeduplication>()
            .HasIndex(d => new { d.DeviceId, d.Metric, d.LastTriggeredAt });

        modelBuilder.Entity<AlarmDeduplication>()
            .HasIndex(d => d.CorrelationId);

        modelBuilder.Entity<AlarmSuppression>()
            .HasIndex(s => new { s.IsActive, s.StartTime, s.EndTime });

        modelBuilder.Entity<AlarmSuppression>()
            .HasIndex(s => s.DeviceId);

        modelBuilder.Entity<AlarmAggregation>()
            .HasIndex(a => a.CorrelationId)
            .IsUnique();

        modelBuilder.Entity<AlarmAggregation>()
            .HasIndex(a => new { a.DeviceId, a.IsResolved });

        modelBuilder.Entity<AlarmRateLimit>()
            .HasIndex(r => r.RuleKey)
            .IsUnique();

        modelBuilder.Entity<AlarmRateLimit>()
            .HasIndex(r => new { r.DeviceId, r.Metric });

        modelBuilder.Entity<AlarmQueueItem>()
            .HasIndex(q => new { q.Status, q.Priority, q.CreatedAt });

        modelBuilder.Entity<AlarmQueueItem>()
            .HasIndex(q => q.DeviceId);

        modelBuilder.Entity<AlarmBatch>()
            .HasIndex(b => b.Status);

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

        modelBuilder.Entity<DeviceHeartbeat>()
            .Property(h => h.Status)
            .HasConversion<string>();

        modelBuilder.Entity<DeviceStateHistory>()
            .Property(h => h.FromStatus)
            .HasConversion<string>();

        modelBuilder.Entity<DeviceStateHistory>()
            .Property(h => h.ToStatus)
            .HasConversion<string>();

        modelBuilder.Entity<DeviceConnection>()
            .Property(c => c.ConnectionType)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmDeduplication>()
            .Property(d => d.Operator)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmSuppression>()
            .Property(s => s.Type)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmSuppression>()
            .Property(s => s.MinLevel)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmAggregation>()
            .Property(a => a.HighestLevel)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmQueueItem>()
            .Property(q => q.Level)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmQueueItem>()
            .Property(q => q.Status)
            .HasConversion<string>();

        modelBuilder.Entity<AlarmBatch>()
            .Property(b => b.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Device>()
            .HasMany(d => d.DataPoints)
            .WithOne(d => d.Device)
            .HasForeignKey(d => d.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Device>()
            .HasMany(d => d.Alarms)
            .WithOne(a => a.Device)
            .HasForeignKey(a => a.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Device>()
            .HasMany(d => d.Commands)
            .WithOne(c => c.Device)
            .HasForeignKey(c => c.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AlarmRule>()
            .HasMany(r => r.Alarms)
            .WithOne(a => a.Rule)
            .HasForeignKey(a => a.RuleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AlarmAggregation>()
            .HasMany(a => a.Alarms)
            .WithOne(alarm => alarm.Aggregation)
            .HasForeignKey(alarm => alarm.AggregationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
