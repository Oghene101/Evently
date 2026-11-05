using Evently.Common.Application.Clock;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

internal sealed class FixedDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
}
