using CardTransactionApi.Data;
using CardTransactionApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CardTransactionApi.Tests.Integration;

/// <summary>
/// Custom factory that swaps SQLite for an in-memory database
/// and injects a mock exchange rate service for deterministic testing.
/// Each test should create a new instance for full isolation.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IExchangeRateService> MockExchangeRateService { get; } = new();

    private readonly string _dbName = "TestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core / DbContext registrations
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in descriptorsToRemove) services.Remove(d);

            // Remove the real ExchangeRateService and HttpClient registrations
            var exchangeDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(IExchangeRateService) ||
                    d.ImplementationType == typeof(ExchangeRateService))
                .ToList();
            foreach (var d in exchangeDescriptors) services.Remove(d);

            // Add in-memory database (unique per factory instance)
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Add mock exchange rate service
            services.AddSingleton<IExchangeRateService>(MockExchangeRateService.Object);
        });
    }
}
