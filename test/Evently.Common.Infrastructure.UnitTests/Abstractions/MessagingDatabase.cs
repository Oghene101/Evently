using Evently.Common.Application.Data;
using Evently.Common.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

public sealed class MessagingDatabase : IAsyncLifetime
{
    private const string Schema = "messaging_tests";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:latest")
        .WithDatabase("evently")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public NpgsqlDataSource DataSource { get; private set; }

    public MessageTable OutboxTable { get; } = MessageTable.Outbox("Tests", Schema);

    public MessageTable InboxTable { get; } = MessageTable.Inbox("Tests", Schema);

    public IDbConnectionFactory ConnectionFactory { get; private set; }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        string connectionString = _dbContainer.GetConnectionString();

        DataSource = NpgsqlDataSource.Create(connectionString);

        ConnectionFactory = new TestDbConnectionFactory(DataSource);

        DbContextOptions<MessagingDbContext> dbContextOptions = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var dbContext = new MessagingDbContext(dbContextOptions, Schema);

        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();

        await _dbContainer.StopAsync();
    }
}
