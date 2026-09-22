using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkFlow360.Infrastructure.Persistence;

namespace WorkFlow360.Tests;

/// <summary>
/// In-memory SQLite database built from the real EF model. Unlike the EF InMemory provider,
/// SQLite enforces foreign keys, unique indexes, check constraints and concurrency tokens.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new SqliteAppDbContext(_options);
        Context.Database.EnsureCreated();
    }

    public AppDbContext Context { get; }

    /// <summary>A separate context for asserting on what was actually saved, bypassing the change tracker.</summary>
    public AppDbContext CreateVerificationContext() => new SqliteAppDbContext(_options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// SQLite has no decimal type and can't SUM decimals, so tests store them as REAL.
    /// SQL Server keeps decimal(5,1); nothing else about the model changes.
    /// </summary>
    private sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<decimal>().HaveConversion<double>();
        }
    }
}
