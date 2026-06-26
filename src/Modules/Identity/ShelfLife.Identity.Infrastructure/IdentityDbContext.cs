using Microsoft.EntityFrameworkCore;
using ShelfLife.Identity.Domain;
using ShelfLife.Infrastructure.Persistence;

namespace ShelfLife.Identity.Infrastructure;

public sealed class IdentityDbContext : ShelfLifeDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Member> Members => Set<Member>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Member>(b =>
        {
            b.ToTable("Members", "identity");
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.HasIndex(x => x.Email).IsUnique();
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.PasswordHash).IsRequired();
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(50);
        });
    }
}
