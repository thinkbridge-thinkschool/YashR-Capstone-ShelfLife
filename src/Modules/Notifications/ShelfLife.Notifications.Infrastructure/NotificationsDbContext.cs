using Microsoft.EntityFrameworkCore;
using ShelfLife.Infrastructure.Persistence;

namespace ShelfLife.Notifications.Infrastructure;

public sealed class NotificationsDbContext : ShelfLifeDbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DeliveryLog>(b =>
        {
            b.ToTable("DeliveryLogs", "notifications");
            b.HasKey(x => x.Id);
            b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<IdempotencyKey>(b =>
        {
            b.ToTable("IdempotencyKeys", "notifications");
            b.HasKey(x => x.EventId);
        });
    }
}

public sealed class DeliveryLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public bool Success { get; set; }
}

public sealed class IdempotencyKey
{
    public Guid EventId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}
