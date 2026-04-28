using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OWLServer.Context;
using OWLServer.Services.Interfaces;
using OWLServer.Tests.Helpers;

namespace OWLServer.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<OWLServer.Components.App>
{
    public MockTowerHttpClient MockTowerHttpClient { get; } = new();
    private readonly SqliteConnection _connection = TestDbHelper.CreateConnection();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<DatabaseContext>>();
            services.RemoveAll<DbContextOptions<DatabaseContext>>();

            services.AddDbContextFactory<DatabaseContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<ITowerHttpClientFactory>();
            services.AddSingleton<ITowerHttpClientFactory>(new MockTowerHttpClientFactory(MockTowerHttpClient));

            using var scope = services.BuildServiceProvider().CreateScope();
            using var ctx = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>().CreateDbContext();
            ctx.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
            MockTowerHttpClient.Dispose();
        }
        base.Dispose(disposing);
    }

    private class MockTowerHttpClientFactory(MockTowerHttpClient client) : ITowerHttpClientFactory
    {
        public ITowerHttpClient Create(string ip) => client;
    }
}
