using System.Data.Common;
using Evently.Common.Application.Data;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

internal sealed class CountingDbConnectionFactory(
    IDbConnectionFactory dbConnectionFactory) : IDbConnectionFactory
{
    private int _openedConnections;

    public int OpenedConnections => _openedConnections;

    public ValueTask<DbConnection> OpenConnectionAsync()
    {
        Interlocked.Increment(ref _openedConnections);

        return dbConnectionFactory.OpenConnectionAsync();
    }
}
