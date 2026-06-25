using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Lending.Application;
using ShelfLife.Lending.Domain;

namespace ShelfLife.Lending.Infrastructure;

public static class LendingModule
{
    public static IServiceCollection AddLendingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<LendingDbContext>(opts =>
            opts.UseSqlServer(config.GetConnectionString("ShelfLife"))
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<BorrowBookHandler>();
        services.AddScoped<ReturnBookHandler>();
        services.AddScoped<PlaceHoldHandler>();
        services.AddScoped<ILoansReadModel, LoansReadModel>();
        services.AddScoped<GetLoansHandler>();
        services.AddScoped<IHoldsReadModel, HoldsReadModel>();
        services.AddScoped<GetHoldsHandler>();
        services.AddScoped<IMyLoansReadModel, MyLoansReadModel>();
        services.AddScoped<GetMyLoansHandler>();
        services.AddScoped<IMyHoldsReadModel, MyHoldsReadModel>();
        services.AddScoped<GetMyHoldsHandler>();

        return services;
    }
}
