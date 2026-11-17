namespace Evently.Common.Infrastructure.Messaging;

// The idempotent handlers of one message add their names here; MessageProcessor saves them with the message's outcome,
// sparing a round trip per handler.
public sealed class HandledMessageConsumers
{
    private readonly List<string> _names = [];

    public IReadOnlyList<string> Names => _names;

    public void Add(string consumerName) => _names.Add(consumerName);
}
