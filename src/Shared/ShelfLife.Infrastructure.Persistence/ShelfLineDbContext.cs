using Microsoft.EntityFrameworkCore;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.SharedKernel;

namespace ShelfLife.Infrastructure.Persistence;

public abstract class ShelfLifeDbContext : DbContext, IUnitOfWork
{
    protected ShelfLifeDbContext(DbContextOptions options) : base(options) { }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasMaxLength(500).IsRequired();
            b.Property(x => x.TopicName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Payload).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        DispatchDomainEvents();

        // EF Core's OwnsMany snapshot-based change detection attaches newly-created owned entities
        // with state=Modified (not Added) when the owner is also Modified. This causes a spurious
        // UPDATE that finds 0 rows → DbUpdateConcurrencyException.
        // Heuristic: if ALL "modified" properties have orig==curr the entity was never in the DB
        // — convert to Added so EF Core generates INSERT instead of UPDATE.
        ChangeTracker.DetectChanges();
        foreach (var e in ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Modified))
        {
            if (e.Properties.All(p => !p.IsModified || Equals(p.OriginalValue, p.CurrentValue)))
                e.State = EntityState.Added;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void DispatchDomainEvents()
    {
        // Domain events are written to outbox during SaveChanges via interceptor
    }
}
