using Microsoft.EntityFrameworkCore;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Infrastructure.Persistence;

namespace ShelfLife.Notifications.Infrastructure;

public sealed class NotificationsDbContext : ShelfLifeDbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<DispatchedNotification> DispatchedNotifications => Set<DispatchedNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OutboxMessage>().ToTable("OutboxMessages", t => t.ExcludeFromMigrations());
        modelBuilder.Entity<DeadLetterMessage>().ToTable("DeadLetterMessages", t => t.ExcludeFromMigrations());

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

        modelBuilder.Entity<DispatchedNotification>(b =>
        {
            b.ToTable("DispatchedNotifications", "notifications");
            b.HasKey(x => x.MessageId);
            b.HasIndex(x => x.DispatchedAt);
        });
    }
}

public sealed class DispatchedNotification
{
    public Guid MessageId { get; set; }
    public DateTimeOffset DispatchedAt { get; set; } = DateTimeOffset.UtcNow;
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
