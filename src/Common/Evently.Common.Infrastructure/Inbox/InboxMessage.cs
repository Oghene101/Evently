namespace Evently.Common.Infrastructure.Inbox;

public sealed class InboxMessage
{
    public Guid Id { get; init; }

    public string Type { get; init; }

    public string Lane { get; init; }

    public string Content { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; init; }

    public DateTime? NextRetryOnUtc { get; init; }

    // While set and in the future, a processor holds the message; once it passes, another may claim it.
    public DateTime? ClaimedUntilUtc { get; init; }

    public int RetryCount { get; init; }

    public string? Error { get; init; }
}
