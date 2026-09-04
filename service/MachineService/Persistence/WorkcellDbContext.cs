using MachineService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MachineService.Persistence;

public sealed class WorkcellDbContext(
    DbContextOptions<WorkcellDbContext> options
) : DbContext(options)
{
    public DbSet<MachineEvent> MachineEvents => Set<MachineEvent>();
    public DbSet<ProductionCycle> ProductionCycles => Set<ProductionCycle>();
    public DbSet<FaultEvent> FaultEvents => Set<FaultEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MachineEvent>(entity =>
        {
            entity.ToTable("machine_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(64);
            entity.Property(x => x.MachineState).HasMaxLength(32);
            entity.Property(x => x.Message).HasMaxLength(512);
            entity.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<ProductionCycle>(entity =>
        {
            entity.ToTable("production_cycles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FinalStatus).HasMaxLength(32);
            entity.Property(x => x.FaultCode).HasMaxLength(64);
            entity.Property(x => x.FaultMessage).HasMaxLength(512);
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<FaultEvent>(entity =>
        {
            entity.ToTable("fault_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FaultCode).HasMaxLength(64);
            entity.Property(x => x.Message).HasMaxLength(512);
            entity.Property(x => x.MachineState).HasMaxLength(32);
            entity.Property(x => x.CycleState).HasMaxLength(32);
            entity.HasIndex(x => x.Timestamp);
        });
    }
}
