using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Notifications.Application;

namespace ShelfLife.Notifications.Infrastructure;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<NotificationsDbContext>(opts =>
            opts.UseSqlServer(config.GetConnectionString("ShelfLife")));

        services.AddScoped<INotificationSender, SmtpNotificationSender>();
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.AddScoped<IMemberLookup, MemberLookup>();
        services.AddScoped<BookBorrowedNotificationHandler>();
        services.AddScoped<HoldReadyNotificationHandler>();
        services.AddScoped<LoanOverdueNotificationHandler>();

        return services;
    }
}
