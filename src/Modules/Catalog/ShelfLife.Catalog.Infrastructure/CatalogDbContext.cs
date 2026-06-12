using Microsoft.EntityFrameworkCore;
using ShelfLife.Catalog.Domain;
using ShelfLife.Infrastructure.Persistence;

namespace ShelfLife.Catalog.Infrastructure;

public sealed class CatalogDbContext : ShelfLifeDbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<BookTitle> BookTitles => Set<BookTitle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
                isbn.HasIndex(i => i.Value).IsUnique();
            });

            b.OwnsMany(x => x.Copies, copy =>
            {
                copy.ToTable("Copies", "catalog");
                copy.HasKey(c => c.Id);
                copy.Property(c => c.Status).HasConversion<string>().HasMaxLength(50);
                copy.Property(c => c.Condition).HasConversion<string>().HasMaxLength(50);
                copy.OwnsOne(c => c.Barcode, bc =>
                {
                    bc.Property(b => b.Value).HasColumnName("Barcode").HasMaxLength(100).IsRequired();
                    bc.HasIndex(b => b.Value).IsUnique();
                });
            });
        });
    }
}
