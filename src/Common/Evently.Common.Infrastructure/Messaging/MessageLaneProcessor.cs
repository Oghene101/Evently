using Evently.Common.Application.Clock;
using Evently.Common.Application.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Evently.Common.Infrastructure.Messaging;

// Claims only as many messages as it has free slots and refills a slot as soon as its message finishes. Instances
// share a lane through SKIP LOCKED; a lapsed lease (its instance died) lets another instance claim the message.
public sealed class MessageLaneProcessor(
    MessageTable table,
    string lane,
    MessageLaneSettings laneSettings,
    int leaseSeconds,
    MessageProcessor messageProcessor,
    IServiceScopeFactory serviceScopeFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<MessageLaneProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan ClaimFailureBackoff = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _freeSlots = new(laneSettings.Concurrency, laneSettings.Concurrency);

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

                IReadOnlyList<Guid> claimedIds = await ClaimAsync(_freeSlots.CurrentCount, stoppingToken);

                foreach (Guid claimedId in claimedIds)
                {
                    await _freeSlots.WaitAsync(stoppingToken);

                    Task messageInFlight = ProcessAndFreeSlotAsync(claimedId, stoppingToken);

                    messagesInFlight.Add(messageInFlight);
                }
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

    private async Task<IReadOnlyList<Guid>> ClaimAsync(
        int limit,
        CancellationToken stoppingToken)
    {
        IReadOnlyList<Guid> claimedIds;
        TimeSpan waitWhenIdle;

        try
        {
            claimedIds = await ClaimFromStoreAsync(limit);

            waitWhenIdle = TimeSpan.FromMilliseconds(laneSettings.PollIntervalMilliseconds);
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

            claimedIds = [];

            waitWhenIdle = ClaimFailureBackoff;
        }

        if (claimedIds.Count == 0)
        {
            await Task.Delay(waitWhenIdle, stoppingToken);
        }

        return claimedIds;
    }

    private async Task<IReadOnlyList<Guid>> ClaimFromStoreAsync(int limit)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        IDbConnectionFactory dbConnectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        var messageStore = new MessageStore(table, dbConnectionFactory);

        DateTime nowUtc = dateTimeProvider.UtcNow;

        DateTime claimedUntilUtc = nowUtc.AddSeconds(leaseSeconds);

        // An idle lane polls several times a second; tracing every empty poll would export nothing useful.
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
}
