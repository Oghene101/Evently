namespace Evently.Common.Infrastructure.Messaging;

public sealed record MessageLaneSettings
{
    public int Concurrency { get; init; }

    public int PollIntervalMilliseconds { get; init; }

    // An idle lane doubles its wait after each empty claim, up to this.
    public int MaxPollIntervalMilliseconds { get; init; }

    public int MaxRetries { get; init; }

    public int RetryDelaySeconds { get; init; }

    public bool IsValid() =>
        Concurrency > 0
        && PollIntervalMilliseconds > 0
        && MaxPollIntervalMilliseconds >= PollIntervalMilliseconds
        && MaxRetries > 0
        && RetryDelaySeconds >= 0;
}
