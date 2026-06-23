using Microsoft.Extensions.Configuration;

namespace ShelfLife.Api.DesignTime;

internal static class DesignTimeConfig
{
    internal static IConfiguration Build() =>
        new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json",
                optional: true)
            .AddEnvironmentVariables()
            .Build();
}
