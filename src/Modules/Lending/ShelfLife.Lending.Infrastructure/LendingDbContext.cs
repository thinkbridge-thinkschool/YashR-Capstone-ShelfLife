using Microsoft.EntityFrameworkCore;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Infrastructure.Persistence;
using ShelfLife.Lending.Domain;

namespace ShelfLife.Lending.Infrastructure;

public sealed class LendingDbContext : ShelfLifeDbContext
{
    public LendingDbContext(DbContextOptions<LendingDbContext> options) : base(options) { }

    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OutboxMessage>().ToTable("OutboxMessages", t => t.ExcludeFromMigrations());

        modelBuilder.Entity<Loan>(b =>
        {
            b.ToTable("Loans", "lending");
            b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);

            b.OwnsOne(x => x.Period, p =>
            {
                p.Property(pp => pp.BorrowedAt).HasColumnName("BorrowedAt").IsRequired();
                p.Property(pp => pp.DueDate).HasColumnName("DueDate").IsRequired();
            });

            b.OwnsMany(x => x.Holds, hold =>
            {
                hold.ToTable("Holds", "lending");
                hold.HasKey(h => h.Id);
                hold.Property(h => h.Status).HasConversion<string>().HasMaxLength(50);
            });
        });
    }
}
