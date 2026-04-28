using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;

namespace OWLServer.Tests.Helpers;

public static class TestDbHelper
{
    public static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        connection.Open();
        return connection;
    }

    public static IDbContextFactory<DatabaseContext> CreateContextFactory(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new DatabaseContext(options);
        context.Database.EnsureCreated();

        return new TestDbContextFactory(options);
    }

    private class TestDbContextFactory(DbContextOptions<DatabaseContext> options)
        : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => new(options);
    }
}
