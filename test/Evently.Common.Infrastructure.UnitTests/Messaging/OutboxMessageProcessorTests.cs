using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.Outbox;
using Evently.Common.Infrastructure.UnitTests.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Evently.Common.Infrastructure.UnitTests.Messaging;

[Collection(nameof(MessagingDatabaseCollection))]
public sealed class OutboxMessageProcessorTests(
    MessagingDatabase database) : IDisposable
{
    private static readonly Error BrokerDown = Error.Dependency("Broker.Unreachable", "The broker did not answer");

    private static readonly Error ReservationMissing = Error.NotFound("Reservation.NotFound", "No such reservation");

    private static readonly Result BrokerDownOutcome = Result.Failure(BrokerDown);

    private static readonly Result ReservationMissingOutcome = Result.Failure(ReservationMissing);

    private static readonly TimeSpan TimestampPrecision = TimeSpan.FromMilliseconds(1);

    private static readonly MessageLaneSettings Lane = new()
    {
        Concurrency = 1,
        PollIntervalMilliseconds = 100,
        MaxRetries = 3,
        RetryDelaySeconds = 30
    };

    private readonly FixedDateTimeProvider _clock = new();

    private readonly List<ServiceProvider> _serviceProviders = [];

    private MessageTable OutboxTable => database.OutboxTable;

    [Fact]
    public async Task HandledMessage_IsMarkedProcessed_AndItsContentCleared()
    {
        var reserveTickets = new HandlerScript();

        MessageProcessor processor = BuildProcessor(reserveTickets);

        Guid messageId = await WriteReservationAsync();

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedOutboxMessage = await database.StoredMessageAsync(OutboxTable, messageId);

        storedOutboxMessage!.ProcessedOnUtc.Should().BeCloseTo(_clock.UtcNow, TimestampPrecision);
        storedOutboxMessage.Content.Should().Be("{}");
        storedOutboxMessage.Error.Should().BeNull();

        reserveTickets.HandledEventIds.Should().Equal(messageId);
    }

    [Fact]
    public async Task DependencyFailure_IsScheduledForRetry_WithExponentialBackoff()
    {
        var reserveTickets = new HandlerScript { Outcome = BrokerDownOutcome };

        MessageProcessor processor = BuildProcessor(reserveTickets);

        Guid messageId = await WriteReservationAsync(retryCount: 1);

        DateTime secondRetryOnUtc = _clock.UtcNow.AddSeconds(Lane.RetryDelaySeconds * 2);

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedOutboxMessage = await database.StoredMessageAsync(OutboxTable, messageId);

        storedOutboxMessage!.ProcessedOnUtc.Should().BeNull();
        storedOutboxMessage.RetryCount.Should().Be(2);
        storedOutboxMessage.NextRetryOnUtc.Should().BeCloseTo(secondRetryOnUtc, TimestampPrecision);
        storedOutboxMessage.ClaimedUntilUtc.Should().BeNull();
        storedOutboxMessage.Error.Should().Be(BrokerDown.Description);
    }

    [Fact]
    public async Task ThrowingHandler_IsRetriedLikeAnUnavailableDependency()
    {
        var reserveTickets = new HandlerScript { Throws = true };

        MessageProcessor processor = BuildProcessor(reserveTickets);

        Guid messageId = await WriteReservationAsync();

        DateTime firstRetryOnUtc = _clock.UtcNow.AddSeconds(Lane.RetryDelaySeconds);

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedOutboxMessage = await database.StoredMessageAsync(OutboxTable, messageId);

        storedOutboxMessage!.RetryCount.Should().Be(1);
        storedOutboxMessage.NextRetryOnUtc.Should().BeCloseTo(firstRetryOnUtc, TimestampPrecision);
        storedOutboxMessage.Error.Should().Contain(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task FinalFailure_IsDeadLetteredAtOnce()
    {
        var reserveTickets = new HandlerScript { Outcome = ReservationMissingOutcome };

        MessageProcessor processor = BuildProcessor(reserveTickets);

        Guid messageId = await WriteReservationAsync();

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedOutboxMessage = await database.StoredMessageAsync(OutboxTable, messageId);

        DeadLetteredMessageRow? deadLetteredOutboxMessage =
            await database.DeadLetteredMessageAsync(OutboxTable, messageId);

        storedOutboxMessage.Should().BeNull();

        deadLetteredOutboxMessage!.RetryCount.Should().Be(1);
        deadLetteredOutboxMessage.Error.Should().Be(ReservationMissing.Description);
        deadLetteredOutboxMessage.Content.Should().Contain(nameof(TicketsReservedDomainEvent.ReservationId));
    }

    [Fact]
    public async Task ExhaustedRetries_AreDeadLettered()
    {
        var reserveTickets = new HandlerScript { Outcome = BrokerDownOutcome };

        MessageProcessor processor = BuildProcessor(reserveTickets);

        Guid messageId = await WriteReservationAsync(retryCount: Lane.MaxRetries - 1);

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedOutboxMessage = await database.StoredMessageAsync(OutboxTable, messageId);

        DeadLetteredMessageRow? deadLetteredOutboxMessage =
            await database.DeadLetteredMessageAsync(OutboxTable, messageId);

        storedOutboxMessage.Should().BeNull();

        deadLetteredOutboxMessage!.RetryCount.Should().Be(Lane.MaxRetries);
    }

    [Fact]
    public async Task UnreadableContent_IsDeadLettered_WithoutRunningAHandler()
    {
        var reserveTickets = new HandlerScript();

        MessageProcessor processor = BuildProcessor(reserveTickets);

        var messageId = Guid.NewGuid();

        await database.InsertAsync(OutboxTable, messageId, """{"not":"a domain event"}""");

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        DeadLetteredMessageRow? deadLetteredOutboxMessage =
            await database.DeadLetteredMessageAsync(OutboxTable, messageId);

        deadLetteredOutboxMessage!.Error.Should().NotBeNullOrEmpty();

        reserveTickets.HandledEventIds.Should().BeEmpty();
    }

    [Fact]
    public async Task FirstFailure_StopsTheHandlersAfterIt()
    {
        var reserveTickets = new HandlerScript { Outcome = BrokerDownOutcome };

        var notifyReservation = new HandlerScript { Outcome = BrokerDownOutcome };

        MessageProcessor processor = BuildProcessor(reserveTickets, notifyReservation);

        Guid messageId = await WriteReservationAsync();

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        int handlersRun = reserveTickets.HandledEventIds.Count + notifyReservation.HandledEventIds.Count;

        handlersRun.Should().Be(1);
    }

    [Fact]
    public async Task ProcessedMessage_IsLeftAlone()
    {
        var reserveTickets = new HandlerScript();

        MessageProcessor processor = BuildProcessor(reserveTickets);

        var reservation = new TicketsReservedDomainEvent(Guid.NewGuid());

        string content = StoredRows.Serialize(reservation);

        await database.InsertAsync(OutboxTable, reservation.Id, content, processedOnUtc: _clock.UtcNow);

        await processor.ProcessAsync(reservation.Id, Lane, CancellationToken.None);

        reserveTickets.HandledEventIds.Should().BeEmpty();
    }

    public void Dispose()
    {
        foreach (ServiceProvider serviceProvider in _serviceProviders)
        {
            serviceProvider.Dispose();
        }
    }

    private async Task<Guid> WriteReservationAsync(int retryCount = 0)
    {
        var reservation = new TicketsReservedDomainEvent(Guid.NewGuid());

        string content = StoredRows.Serialize(reservation);

        await database.InsertAsync(OutboxTable, reservation.Id, content, retryCount: retryCount);

        return reservation.Id;
    }

    private MessageProcessor BuildProcessor(
        HandlerScript reserveTickets,
        HandlerScript? notifyReservation = null)
    {
        var reserveTicketsHandler = new ReserveTicketsDomainEventHandler(reserveTickets);

        var notifyReservationHandler = new NotifyReservationDomainEventHandler(notifyReservation ?? new HandlerScript());

        var services = new ServiceCollection();

        services.AddSingleton(reserveTicketsHandler);
        services.AddSingleton(notifyReservationHandler);
        services.AddScoped(_ => database.ConnectionFactory);

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _serviceProviders.Add(serviceProvider);

        var domainEventDispatcher = new DomainEventDispatcher(typeof(TicketsReservedDomainEvent).Assembly);

        IServiceScopeFactory serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return new MessageProcessor(
            OutboxTable,
            domainEventDispatcher,
            serviceScopeFactory,
            _clock,
            NullLogger<MessageProcessor>.Instance);
    }
}
