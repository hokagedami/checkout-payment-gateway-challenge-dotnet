using Microsoft.EntityFrameworkCore;
using PaymentGateway.Api.Models.Entities;

namespace PaymentGateway.Api.Data;

public class PaymentGatewayDbContext : DbContext
{
    public PaymentGatewayDbContext(DbContextOptions<PaymentGatewayDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.CardNumberLastFour)
                .IsRequired()
                .HasMaxLength(4);

            entity.Property(e => e.ExpiryMonth)
                .IsRequired();

            entity.Property(e => e.ExpiryYear)
                .IsRequired();

            entity.Property(e => e.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(e => e.Amount)
                .IsRequired();

            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(256);

            // Create index on IdempotencyKey for performance
            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
