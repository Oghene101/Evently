namespace Evently.Common.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    public string Type { get; init; }

    public string Lane { get; init; }

    public string Content { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; init; }

    public DateTime? NextRetryOnUtc { get; init; }

    // When the message may next be claimed: its retry time, else when it occurred. Computed by the database.
    public DateTime AvailableOnUtc { get; init; }

    // While set and in the future, a processor holds the message; once it passes, another may claim it.
    public DateTime? ClaimedUntilUtc { get; init; }

    public int RetryCount { get; init; }

    public string? Error { get; init; }
}
