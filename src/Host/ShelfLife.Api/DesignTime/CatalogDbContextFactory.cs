using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShelfLife.Catalog.Infrastructure;

namespace ShelfLife.Api.DesignTime;

internal sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var config = DesignTimeConfig.Build();
        var opts = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(config.GetConnectionString("ShelfLife"))
            .Options;
        return new CatalogDbContext(opts);
    }
}
