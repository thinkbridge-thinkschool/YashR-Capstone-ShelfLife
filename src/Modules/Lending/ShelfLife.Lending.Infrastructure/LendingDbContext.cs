using Microsoft.EntityFrameworkCore;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Infrastructure.Persistence;
using ShelfLife.Lending.Contracts;
using ShelfLife.Lending.Domain;
using ShelfLife.SharedKernel;
using System.Text.Json;

namespace ShelfLife.Lending.Infrastructure;

public sealed class LendingDbContext : ShelfLifeDbContext
{
    public LendingDbContext(DbContextOptions<LendingDbContext> options) : base(options) { }

    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        WriteSideEffects();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void WriteSideEffects()
    {
        var aggregates = ChangeTracker.Entries<Loan>()
            .Select(e => e.Entity)
            .Where(l => l.DomainEvents.Any())
            .ToList();

        foreach (var loan in aggregates)
        {
            foreach (var @event in loan.DomainEvents)
            {
                var msg = ToOutboxMessage(@event);
                if (msg is not null)
                    OutboxMessages.Add(msg);

                var entry = ToAuditLog(@event);
                if (entry is not null)
                    AuditLogs.Add(entry);
            }
            loan.ClearDomainEvents();
        }
    }

    private static OutboxMessage? ToOutboxMessage(IDomainEvent @event) => @event switch
    {
        LoanCreatedDomainEvent e => Wrap("shelflife.lending.book-borrowed", nameof(BookBorrowedEvent),
            new BookBorrowedEvent(e.EventId, e.OccurredAt, e.LoanId, e.MemberId, e.BookTitleId, e.CopyId, e.DueDate)),

        LoanReturnedDomainEvent e => Wrap("shelflife.lending.book-returned", nameof(BookReturnedEvent),
            new BookReturnedEvent(e.EventId, e.OccurredAt, e.LoanId, e.MemberId, e.BookTitleId, e.CopyId)),

        HoldReadyDomainEvent e => Wrap("shelflife.lending.hold-ready", nameof(HoldReadyEvent),
            new HoldReadyEvent(e.EventId, e.OccurredAt, e.HoldId, e.MemberId, e.BookTitleId, e.ExpiresAt)),

        HoldPlacedDomainEvent e => Wrap("shelflife.lending.hold-placed", nameof(HoldPlacedEvent),
            new HoldPlacedEvent(e.EventId, e.OccurredAt, e.HoldId, e.MemberId, e.BookTitleId)),

        _ => null
    };

    private static AuditLog? ToAuditLog(IDomainEvent @event) => @event switch
    {
        LoanCreatedDomainEvent e => new AuditLog
        {
            Action = "Borrow", ActorId = e.MemberId,
            LoanId = e.LoanId, BookTitleId = e.BookTitleId, CopyId = e.CopyId,
            OccurredAt = e.OccurredAt
        },
        LoanReturnedDomainEvent e => new AuditLog
        {
            Action = "Return", ActorId = e.MemberId,
            LoanId = e.LoanId, BookTitleId = e.BookTitleId, CopyId = e.CopyId,
            OccurredAt = e.OccurredAt
        },
        HoldPlacedDomainEvent e => new AuditLog
        {
            Action = "PlaceHold", ActorId = e.MemberId,
            BookTitleId = e.BookTitleId,
            OccurredAt = e.OccurredAt
        },
        HoldReadyDomainEvent e => new AuditLog
        {
            Action = "HoldReady", ActorId = e.MemberId,
            BookTitleId = e.BookTitleId,
            OccurredAt = e.OccurredAt
        },
        _ => null
    };

    private static OutboxMessage Wrap<T>(string topicName, string type, T payload) where T : class
        => new() { TopicName = topicName, Type = type, Payload = JsonSerializer.Serialize(payload) };

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OutboxMessage>().ToTable("OutboxMessages", t => t.ExcludeFromMigrations());
        modelBuilder.Entity<DeadLetterMessage>().ToTable("DeadLetterMessages", t => t.ExcludeFromMigrations());

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLog", "lending");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(50).IsRequired();
            b.HasIndex(x => x.OccurredAt);
        });

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
