using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShelfLife.Lending.Infrastructure;

namespace ShelfLife.Api.DesignTime;

internal sealed class LendingDbContextFactory : IDesignTimeDbContextFactory<LendingDbContext>
{
    public LendingDbContext CreateDbContext(string[] args)
    {
        var config = DesignTimeConfig.Build();
        var opts = new DbContextOptionsBuilder<LendingDbContext>()
            .UseSqlServer(config.GetConnectionString("ShelfLife"))
            .Options;
        return new LendingDbContext(opts);
    }
}
