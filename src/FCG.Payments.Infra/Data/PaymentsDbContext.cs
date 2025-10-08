using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FCG.Payments.Infra.Data;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<EventRecord> Events => Set<EventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var statusConv = new EnumToStringConverter<PaymentStatus>();

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("payments");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
             .HasColumnName("id")
             .ValueGeneratedNever();

            e.Property(x => x.UserId)
             .HasColumnName("user_id")
             .IsRequired();

            e.Property(x => x.GameId)
             .HasColumnName("game_id")
             .IsRequired();

            e.Property(x => x.Amount)
             .HasColumnName("amount")
             .HasColumnType("decimal(10,2)")
             .IsRequired();

            e.Property(x => x.Status)
             .HasColumnName("status")
             .HasConversion(statusConv)
             .HasMaxLength(20)
             .IsRequired();

            e.Property(x => x.CreatedAtUtc)
             .HasColumnName("created_at_utc")
             .IsRequired();

            e.Property(x => x.UpdatedAtUtc)
             .HasColumnName("updated_at_utc");

            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.GameId);
            e.HasIndex(x => new { x.UserId, x.Status });
        });

        modelBuilder.Entity<EventRecord>(entity =>
        {
            entity.ToTable("event_store");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .IsRequired();

            entity.Property(e => e.AggregateId)
                .HasColumnName("aggregate_id")
                .IsRequired();

            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Data)
                .HasColumnName("data")
                .HasColumnType("json")
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
        });
    }
}
