namespace Evently.Common.Infrastructure.Inbox;

public sealed class DeadLetteredInboxMessage
{
    public Guid Id { get; init; }

    public string Type { get; init; }

    public string Content { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public DateTime DeadLetteredOnUtc { get; init; }

    public int RetryCount { get; init; }

    public string Error { get; init; }
}
