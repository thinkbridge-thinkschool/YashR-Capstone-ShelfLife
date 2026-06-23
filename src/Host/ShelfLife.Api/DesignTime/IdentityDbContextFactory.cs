using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShelfLife.Identity.Infrastructure;

namespace ShelfLife.Api.DesignTime;

internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var config = DesignTimeConfig.Build();
        var opts = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(config.GetConnectionString("ShelfLife"))
            .Options;
        return new IdentityDbContext(opts);
    }
}
