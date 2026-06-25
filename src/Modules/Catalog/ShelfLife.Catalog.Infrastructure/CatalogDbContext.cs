using Microsoft.EntityFrameworkCore;
using ShelfLife.Catalog.Domain;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Infrastructure.Persistence;

namespace ShelfLife.Catalog.Infrastructure;

public sealed class CatalogDbContext : ShelfLifeDbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<BookTitle> BookTitles => Set<BookTitle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // OutboxMessages and DeadLetterMessages are owned by IdentityDbContext's migration.
        // Keep EF tracking but exclude from this context's migrations.
        modelBuilder.Entity<OutboxMessage>().ToTable("OutboxMessages", t => t.ExcludeFromMigrations());
        modelBuilder.Entity<DeadLetterMessage>().ToTable("DeadLetterMessages", t => t.ExcludeFromMigrations());

        modelBuilder.Entity<BookTitle>(b =>
        {
            b.ToTable("BookTitles", "catalog");
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(500).IsRequired();
            b.Property(x => x.Author).HasMaxLength(300).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);

            b.OwnsOne(x => x.Isbn, isbn =>
            {
                isbn.Property(i => i.Value).HasColumnName("Isbn").HasMaxLength(13).IsRequired();
                isbn.HasIndex(i => i.Value).HasDatabaseName("IX_BookTitles_Isbn").IsUnique();
            });

            b.OwnsMany(x => x.Copies, copy =>
            {
                copy.ToTable("Copies", "catalog");
                copy.HasKey(c => c.Id);
                copy.Property(c => c.Status).HasConversion<string>().HasMaxLength(50);
                copy.Property(c => c.Condition).HasConversion<string>().HasMaxLength(50);

                // CopyBarcode as a scalar via HasConversion avoids the nested OwnsOne table-split
                // pattern, which causes EF to issue a secondary UPDATE for the same Copies row
                // after the INSERT, yielding DbUpdateConcurrencyException (0 rows affected).
                copy.Property(c => c.Barcode)
                    .HasConversion(bc => bc.Value, v => CopyBarcode.Create(v))
                    .HasColumnName("Barcode")
                    .HasMaxLength(100)
                    .IsRequired();
                copy.HasIndex("Barcode").HasDatabaseName("IX_Copies_Barcode").IsUnique();
            });
        });
    }
}
