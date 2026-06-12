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
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void DispatchDomainEvents()
    {
        // Domain events are written to outbox during SaveChanges via interceptor
    }
}
