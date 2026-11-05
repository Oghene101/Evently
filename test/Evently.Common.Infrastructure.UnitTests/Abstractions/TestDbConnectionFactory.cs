using System.Data.Common;
using Evently.Common.Application.Data;
using Npgsql;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

internal sealed class TestDbConnectionFactory(
    NpgsqlDataSource dataSource) : IDbConnectionFactory
{
    public async ValueTask<DbConnection> OpenConnectionAsync()
    {
        return await dataSource.OpenConnectionAsync();
    }
}
