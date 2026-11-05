using Evently.Common.Application.Clock;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
