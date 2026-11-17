using Evently.Common.Application.Clock;
using Evently.Common.Application.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Evently.Common.Infrastructure.Messaging;

// Claims only as many messages as it has free slots and refills a slot as soon as its message finishes. Instances
// share a lane through SKIP LOCKED; a lapsed lease (its instance died) lets another instance claim the message.
// An idle lane doubles its wait after each empty claim up to MaxPollIntervalMilliseconds, unless a message stored in
// this instance wakes it first.
public sealed class MessageLaneProcessor(
    MessageTable table,
    string lane,
    MessageLaneSettings laneSettings,
    int leaseSeconds,
    MessageProcessor messageProcessor,
    LaneWakeUps laneWakeUps,
    IServiceScopeFactory serviceScopeFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<MessageLaneProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan ClaimFailureBackoff = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _freeSlots = new(laneSettings.Concurrency, laneSettings.Concurrency);

    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(laneSettings.PollIntervalMilliseconds);

    private readonly TimeSpan _maxPollInterval = TimeSpan.FromMilliseconds(laneSettings.MaxPollIntervalMilliseconds);

    private TimeSpan _idleWait = TimeSpan.FromMilliseconds(laneSettings.PollIntervalMilliseconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Task> messagesInFlight = [];

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                messagesInFlight.RemoveAll(messageInFlight => messageInFlight.IsCompleted);

                if (_freeSlots.CurrentCount == 0)
                {
                    await Task.WhenAny(messagesInFlight);

                    continue;
                }

                ClaimOutcome claim = await ClaimAsync(_freeSlots.CurrentCount);

                foreach (Guid claimedId in claim.ClaimedIds)
                {
                    await _freeSlots.WaitAsync(stoppingToken);

                    Task messageInFlight = ProcessAndFreeSlotAsync(claimedId, stoppingToken);

                    messagesInFlight.Add(messageInFlight);
                }

                if (claim.ClaimedIds.Count > 0)
                {
                    _idleWait = _pollInterval;

                    continue;
                }

                bool wokenUp = await laneWakeUps.WaitAsync(lane, claim.WaitBeforeNextClaim, stoppingToken);

                TimeSpan doubledIdleWait = DoubledIdleWait();

                _idleWait = wokenUp ? _pollInterval : doubledIdleWait;
            }
        }
        finally
        {
            await Task.WhenAll(messagesInFlight);
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        _freeSlots.Dispose();
    }

    private TimeSpan DoubledIdleWait()
    {
        TimeSpan doubledIdleWait = _idleWait * 2;

        return doubledIdleWait < _maxPollInterval ? doubledIdleWait : _maxPollInterval;
    }

    private async Task<ClaimOutcome> ClaimAsync(int limit)
    {
        try
        {
            IReadOnlyList<Guid> claimedIds = await ClaimFromStoreAsync(limit);

            return new ClaimOutcome(claimedIds, _idleWait);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "{Module} - Claiming {Table} messages in the {Lane} lane failed; trying again in {Backoff}",
                table.ModuleName,
                table.MessagesTable,
                lane,
                ClaimFailureBackoff);

            return new ClaimOutcome([], ClaimFailureBackoff);
        }
    }

    private async Task<IReadOnlyList<Guid>> ClaimFromStoreAsync(int limit)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        IDbConnectionFactory dbConnectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        var messageStore = new MessageStore(table, dbConnectionFactory);

        DateTime nowUtc = dateTimeProvider.UtcNow;

        DateTime claimedUntilUtc = nowUtc.AddSeconds(leaseSeconds);

        // An idle lane keeps polling; tracing every empty claim would export nothing useful.
        using IDisposable quietClaim = SuppressInstrumentationScope.Begin();

        return await messageStore.ClaimAsync(lane, limit, nowUtc, claimedUntilUtc);
    }

    private async Task ProcessAndFreeSlotAsync(
        Guid messageId,
        CancellationToken stoppingToken)
    {
        try
        {
            await messageProcessor.ProcessAsync(messageId, laneSettings, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Recording the outcome failed (the database is down, say); the lease lapses and it is claimed again.
            logger.LogError(
                exception,
                "{Module} - {Table} message {MessageId} in the {Lane} lane could not be processed",
                table.ModuleName,
                table.MessagesTable,
                messageId,
                lane);
        }
        finally
        {
            _freeSlots.Release();
        }
    }

    private sealed record ClaimOutcome(
        IReadOnlyList<Guid> ClaimedIds,
        TimeSpan WaitBeforeNextClaim);
}
