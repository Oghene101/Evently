using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.Outbox;
using Evently.Common.Infrastructure.UnitTests.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Evently.Common.Infrastructure.UnitTests.Messaging;

[Collection(nameof(MessagingDatabaseCollection))]
public sealed class MessageLaneProcessorTests(
    MessagingDatabase database)
{
    private const int MessageCount = 200;

    private static readonly MessageLaneSettings Lane = new()
    {
        Concurrency = 8,
        PollIntervalMilliseconds = 50,
        MaxRetries = 3,
        RetryDelaySeconds = 1
    };

    private static readonly TimeSpan HandlingTime = TimeSpan.FromMilliseconds(5);

    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ProcessedPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly string _lane = Guid.NewGuid().ToString();

    [Fact]
    public async Task TwoProcessorsOnOneLane_HandleEveryMessageExactlyOnce()
    {
        var countSeats = new HandlerScript { Delay = HandlingTime };

        List<Guid> messageIds = await WriteSeatsCountedAsync();

        var countSeatsHandler = new CountSeatsDomainEventHandler(countSeats);

        var services = new ServiceCollection();

        services.AddSingleton(countSeatsHandler);
        services.AddScoped(_ => database.ConnectionFactory);

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        using MessageLaneProcessor firstProcessor = BuildLaneProcessor(serviceProvider);

        using MessageLaneProcessor secondProcessor = BuildLaneProcessor(serviceProvider);

        await firstProcessor.StartAsync(CancellationToken.None);

        await secondProcessor.StartAsync(CancellationToken.None);

        bool allProcessed = await WaitUntilProcessedAsync(messageIds, ProcessingTimeout);

        await firstProcessor.StopAsync(CancellationToken.None);

        await secondProcessor.StopAsync(CancellationToken.None);

        allProcessed.Should().BeTrue();

        countSeats.HandledEventIds.Should().HaveCount(MessageCount).And.OnlyHaveUniqueItems();
        countSeats.HandledEventIds.Should().BeEquivalentTo(messageIds);
    }

    private async Task<List<Guid>> WriteSeatsCountedAsync()
    {
        List<Guid> messageIds = [];

        foreach (int seats in Enumerable.Range(0, MessageCount))
        {
            var seatsCounted = new SeatsCountedDomainEvent(seats);

            string content = StoredRows.Serialize(seatsCounted);

            await database.InsertAsync(database.OutboxTable, seatsCounted.Id, content, _lane);

            messageIds.Add(seatsCounted.Id);
        }

        return messageIds;
    }

    private MessageLaneProcessor BuildLaneProcessor(ServiceProvider serviceProvider)
    {
        var clock = new SystemDateTimeProvider();

        var domainEventDispatcher = new DomainEventDispatcher(typeof(SeatsCountedDomainEvent).Assembly);

        IServiceScopeFactory serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var messageProcessor = new MessageProcessor(
            database.OutboxTable,
            domainEventDispatcher,
            serviceScopeFactory,
            clock,
            NullLogger<MessageProcessor>.Instance);

        return new MessageLaneProcessor(
            database.OutboxTable,
            _lane,
            Lane,
            120,
            messageProcessor,
            serviceScopeFactory,
            clock,
            NullLogger<MessageLaneProcessor>.Instance);
    }

    private async Task<bool> WaitUntilProcessedAsync(
        List<Guid> messageIds,
        TimeSpan timeout)
    {
        DateTime giveUpAtUtc = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < giveUpAtUtc)
        {
            int processedCount = await CountProcessedAsync(messageIds);

            if (processedCount == messageIds.Count)
            {
                return true;
            }

            await Task.Delay(ProcessedPollInterval);
        }

        return false;
    }

    private async Task<int> CountProcessedAsync(List<Guid> messageIds)
    {
        int processedCount = 0;

        foreach (Guid messageId in messageIds)
        {
            StoredMessageRow? storedOutboxMessage = await database.StoredMessageAsync(database.OutboxTable, messageId);

            if (storedOutboxMessage is null || storedOutboxMessage.ProcessedOnUtc is null)
            {
                continue;
            }

            processedCount++;
        }

        return processedCount;
    }
}
