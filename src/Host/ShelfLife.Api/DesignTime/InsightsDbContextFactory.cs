using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShelfLife.Insights.Infrastructure;

namespace ShelfLife.Api.DesignTime;

internal sealed class InsightsDbContextFactory : IDesignTimeDbContextFactory<InsightsDbContext>
{
    public InsightsDbContext CreateDbContext(string[] args)
    {
        var config = DesignTimeConfig.Build();
        var opts = new DbContextOptionsBuilder<InsightsDbContext>()
            .UseSqlServer(config.GetConnectionString("ShelfLife"))
            .Options;
        return new InsightsDbContext(opts);
    }
}
