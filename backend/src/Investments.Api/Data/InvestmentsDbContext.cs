using Investments.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Investments.Api.Data;

public sealed class InvestmentsDbContext(DbContextOptions<InvestmentsDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Investment> Investments => Set<Investment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Owner>(owner =>
        {
            owner.Property(o => o.Id).ValueGeneratedNever();
            owner.Property(o => o.Name).HasMaxLength(Owner.NameMaxLength);
            owner.Property(o => o.Email).HasMaxLength(Owner.EmailMaxLength);
            owner.HasIndex(o => o.Email).IsUnique();
        });

        modelBuilder.Entity<Investment>(investment =>
        {
            investment.Property(i => i.Id).ValueGeneratedNever();
            investment.Property(i => i.InitialAmount).HasPrecision(18, 2);

            // Concurrency token: two simultaneous withdrawals can't both succeed.
            investment.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();

            investment.HasOne(i => i.Owner).WithMany().HasForeignKey(i => i.OwnerId).OnDelete(DeleteBehavior.Restrict);
            investment.HasIndex(i => new { i.OwnerId, i.CreationDate });

            investment.OwnsOne(i => i.Withdrawal, withdrawal =>
            {
                withdrawal.Property(w => w.GrossAmount).HasPrecision(18, 2);
                withdrawal.Property(w => w.Gains).HasPrecision(18, 2);
                withdrawal.Property(w => w.TaxRate).HasPrecision(5, 4);
                withdrawal.Property(w => w.TaxAmount).HasPrecision(18, 2);
                withdrawal.Property(w => w.NetAmount).HasPrecision(18, 2);
            });
        });
    }
}
