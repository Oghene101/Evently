namespace Evently.Common.Infrastructure.Messaging;

public sealed record MessageLaneSettings
{
    public int Concurrency { get; init; }

    public int PollIntervalMilliseconds { get; init; }

    public int MaxRetries { get; init; }

    public int RetryDelaySeconds { get; init; }
}
