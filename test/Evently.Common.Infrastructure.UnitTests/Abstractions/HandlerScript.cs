using System.Collections.Concurrent;
using Evently.Common.Domain;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

public sealed class HandlerScript
{
    private readonly ConcurrentQueue<Guid> _handledEventIds = new();

    public Result Outcome { get; init; } = Result.Success();

    public bool Throws { get; init; }

    public TimeSpan Delay { get; init; } = TimeSpan.Zero;

    public IReadOnlyCollection<Guid> HandledEventIds => _handledEventIds;

    public async Task<Result> RunAsync(Guid eventId)
    {
        _handledEventIds.Enqueue(eventId);

        await Task.Delay(Delay);

        if (Throws)
        {
            throw new InvalidOperationException("The handler threw");
        }

        return Outcome;
    }
}
