using Microsoft.EntityFrameworkCore;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Infrastructure.Persistence;

namespace ShelfLife.Insights.Infrastructure;

public sealed class InsightsDbContext : ShelfLifeDbContext
{
    public InsightsDbContext(DbContextOptions<InsightsDbContext> options) : base(options) { }

    public DbSet<PopularTitleProjection> PopularTitleProjections => Set<PopularTitleProjection>();
    public DbSet<OverdueLoanProjection> OverdueLoanProjections => Set<OverdueLoanProjection>();
    public DbSet<MemberActivityProjection> MemberActivityProjections => Set<MemberActivityProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OutboxMessage>().ToTable("OutboxMessages", t => t.ExcludeFromMigrations());

        modelBuilder.Entity<PopularTitleProjection>(b =>
        {
            b.ToTable("PopularTitles", "insights");
            b.HasKey(x => x.BookTitleId);
        });

        modelBuilder.Entity<OverdueLoanProjection>(b =>
        {
            b.ToTable("OverdueLoans", "insights");
            b.HasKey(x => x.LoanId);
        });

        modelBuilder.Entity<MemberActivityProjection>(b =>
        {
            b.ToTable("MemberActivity", "insights");
            b.HasKey(x => x.MemberId);
        });
    }
}
