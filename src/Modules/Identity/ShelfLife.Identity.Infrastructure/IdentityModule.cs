using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Identity.Application;
using ShelfLife.Identity.Domain;

namespace ShelfLife.Identity.Infrastructure;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<IdentityDbContext>(opts =>
            opts.UseSqlServer(config.GetConnectionString("ShelfLife"))
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<RegisterMemberHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<IMembersReadModel, MembersReadModel>();
        services.AddScoped<GetMembersHandler>();

        return services;
    }
}
