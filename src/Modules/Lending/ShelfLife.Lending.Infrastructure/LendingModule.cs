using Microsoft.EntityFrameworkCore;
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
            opts.UseSqlServer(config.GetConnectionString("ShelfLife")));

        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<BorrowBookHandler>();
        services.AddScoped<ReturnBookHandler>();
        services.AddScoped<PlaceHoldHandler>();

        return services;
    }
}
