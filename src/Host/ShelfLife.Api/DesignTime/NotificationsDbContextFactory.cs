using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShelfLife.Notifications.Infrastructure;

namespace ShelfLife.Api.DesignTime;

internal sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var config = DesignTimeConfig.Build();
        var opts = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlServer(config.GetConnectionString("ShelfLife"))
            .Options;
        return new NotificationsDbContext(opts);
    }
}
