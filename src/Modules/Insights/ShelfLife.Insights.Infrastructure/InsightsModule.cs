using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Insights.Application;

namespace ShelfLife.Insights.Infrastructure;

public static class InsightsModule
{
    public static IServiceCollection AddInsightsModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<InsightsDbContext>(opts =>
            opts.UseSqlServer(config.GetConnectionString("ShelfLife"))
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IInsightsReadModel, InsightsReadModel>();
        services.AddScoped<InsightsQueryHandler>();

        return services;
    }
}
