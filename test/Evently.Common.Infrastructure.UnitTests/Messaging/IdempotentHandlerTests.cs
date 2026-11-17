using Evently.Common.Domain;
using Evently.Common.Infrastructure.Inbox;
using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.Outbox;
using Evently.Common.Infrastructure.UnitTests.Abstractions;
using FluentAssertions;

namespace Evently.Common.Infrastructure.UnitTests.Messaging;

[Collection(nameof(MessagingDatabaseCollection))]
public sealed class IdempotentHandlerTests(
    MessagingDatabase database)
{
    private static readonly Error BrokerDown = Error.Dependency("Broker.Unreachable", "The broker did not answer");

    private static readonly Result BrokerDownOutcome = Result.Failure(BrokerDown);

    private readonly HandledMessageConsumers _handledMessageConsumers = new();

    [Fact]
    public async Task HandledDomainEvent_IsLeftForTheProcessorToSave()
    {
        var reserveTickets = new HandlerScript();

        IdempotentDomainEventHandler<TicketsReservedDomainEvent> idempotentHandler =
            IdempotentReserveTicketsHandler(reserveTickets);

        var reservation = new TicketsReservedDomainEvent(Guid.NewGuid());

        Result handled = await idempotentHandler.Handle(reservation);

        bool consumerSaved = await database.ConsumerExistsAsync(
            database.OutboxTable,
            reservation.Id,
            nameof(ReserveTicketsDomainEventHandler));

        handled.IsSuccess.Should().BeTrue();

        _handledMessageConsumers.Names.Should().Equal(nameof(ReserveTicketsDomainEventHandler));

        consumerSaved.Should().BeFalse();
    }

    [Fact]
    public async Task SavedDomainEventConsumer_IsNotHandledAgain()
    {
        var reserveTickets = new HandlerScript();

        IdempotentDomainEventHandler<TicketsReservedDomainEvent> idempotentHandler =
            IdempotentReserveTicketsHandler(reserveTickets);

        var reservation = new TicketsReservedDomainEvent(Guid.NewGuid());

        await database.InsertConsumerAsync(
            database.OutboxTable,
            reservation.Id,
            nameof(ReserveTicketsDomainEventHandler));

        Result redelivered = await idempotentHandler.Handle(reservation);

        redelivered.IsSuccess.Should().BeTrue();

        reserveTickets.HandledEventIds.Should().BeEmpty();

        _handledMessageConsumers.Names.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedDomainEvent_IsNotRecorded_SoItsRetryRunsTheHandlerAgain()
    {
        var reserveTickets = new HandlerScript { Outcome = BrokerDownOutcome };

        IdempotentDomainEventHandler<TicketsReservedDomainEvent> idempotentHandler =
            IdempotentReserveTicketsHandler(reserveTickets);

        var reservation = new TicketsReservedDomainEvent(Guid.NewGuid());

        Result firstAttempt = await idempotentHandler.Handle(reservation);

        await idempotentHandler.Handle(reservation);

        firstAttempt.Error.Should().Be(BrokerDown);

        _handledMessageConsumers.Names.Should().BeEmpty();

        reserveTickets.HandledEventIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandledIntegrationEvent_IsLeftForTheProcessorToSave()
    {
        var registerAttendee = new HandlerScript();

        IdempotentIntegrationEventHandler<AttendeeRegisteredIntegrationEvent> idempotentHandler =
            IdempotentRegisterAttendeeHandler(registerAttendee);

        var attendeeRegistered = new AttendeeRegisteredIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());

        Result handled = await idempotentHandler.Handle(attendeeRegistered);

        bool consumerSaved = await database.ConsumerExistsAsync(
            database.InboxTable,
            attendeeRegistered.Id,
            nameof(RegisterAttendeeIntegrationEventHandler));

        handled.IsSuccess.Should().BeTrue();

        _handledMessageConsumers.Names.Should().Equal(nameof(RegisterAttendeeIntegrationEventHandler));

        consumerSaved.Should().BeFalse();
    }

    [Fact]
    public async Task SavedIntegrationEventConsumer_IsNotHandledAgain()
    {
        var registerAttendee = new HandlerScript();

        IdempotentIntegrationEventHandler<AttendeeRegisteredIntegrationEvent> idempotentHandler =
            IdempotentRegisterAttendeeHandler(registerAttendee);

        var attendeeRegistered = new AttendeeRegisteredIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());

        await database.InsertConsumerAsync(
            database.InboxTable,
            attendeeRegistered.Id,
            nameof(RegisterAttendeeIntegrationEventHandler));

        Result redelivered = await idempotentHandler.Handle(attendeeRegistered);

        redelivered.IsSuccess.Should().BeTrue();

        registerAttendee.HandledEventIds.Should().BeEmpty();

        _handledMessageConsumers.Names.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedIntegrationEvent_IsNotRecorded_SoItsRetryRunsTheHandlerAgain()
    {
        var registerAttendee = new HandlerScript { Outcome = BrokerDownOutcome };

        IdempotentIntegrationEventHandler<AttendeeRegisteredIntegrationEvent> idempotentHandler =
            IdempotentRegisterAttendeeHandler(registerAttendee);

        var attendeeRegistered = new AttendeeRegisteredIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());

        await idempotentHandler.Handle(attendeeRegistered);

        await idempotentHandler.Handle(attendeeRegistered);

        _handledMessageConsumers.Names.Should().BeEmpty();

        registerAttendee.HandledEventIds.Should().HaveCount(2);
    }

    private IdempotentDomainEventHandler<TicketsReservedDomainEvent> IdempotentReserveTicketsHandler(
        HandlerScript reserveTickets)
    {
        var reserveTicketsHandler = new ReserveTicketsDomainEventHandler(reserveTickets);

        return new IdempotentDomainEventHandler<TicketsReservedDomainEvent>(
            reserveTicketsHandler,
            database.ConnectionFactory,
            database.OutboxTable,
            _handledMessageConsumers);
    }

    private IdempotentIntegrationEventHandler<AttendeeRegisteredIntegrationEvent> IdempotentRegisterAttendeeHandler(
        HandlerScript registerAttendee)
    {
        var registerAttendeeHandler = new RegisterAttendeeIntegrationEventHandler(registerAttendee);

        return new IdempotentIntegrationEventHandler<AttendeeRegisteredIntegrationEvent>(
            registerAttendeeHandler,
            database.ConnectionFactory,
            database.InboxTable,
            _handledMessageConsumers);
    }
}
