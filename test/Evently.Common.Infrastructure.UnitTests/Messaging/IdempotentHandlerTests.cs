using Evently.Common.Domain;
using Evently.Common.Infrastructure.Inbox;
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

    [Fact]
    public async Task HandledDomainEvent_IsRecorded_AndNotHandledAgain()
    {
        var reserveTickets = new HandlerScript();

        var reserveTicketsHandler = new ReserveTicketsDomainEventHandler(reserveTickets);

        var idempotentHandler = new IdempotentDomainEventHandler<TicketsReservedDomainEvent>(
            reserveTicketsHandler,
            database.ConnectionFactory,
            database.OutboxTable);

        var reservation = new TicketsReservedDomainEvent(Guid.NewGuid());

        Result firstAttempt = await idempotentHandler.Handle(reservation);

        Result secondAttempt = await idempotentHandler.Handle(reservation);

        bool consumerRecorded = await database.ConsumerExistsAsync(
            database.OutboxTable,
            reservation.Id,
            nameof(ReserveTicketsDomainEventHandler));

        firstAttempt.IsSuccess.Should().BeTrue();

        secondAttempt.IsSuccess.Should().BeTrue();

        consumerRecorded.Should().BeTrue();

        reserveTickets.HandledEventIds.Should().Equal(reservation.Id);
    }

    [Fact]
    public async Task FailedDomainEvent_IsNotRecorded_SoItsRetryRunsTheHandlerAgain()
    {
        var reserveTickets = new HandlerScript { Outcome = BrokerDownOutcome };

        var reserveTicketsHandler = new ReserveTicketsDomainEventHandler(reserveTickets);

        var idempotentHandler = new IdempotentDomainEventHandler<TicketsReservedDomainEvent>(
            reserveTicketsHandler,
            database.ConnectionFactory,
            database.OutboxTable);

        var reservation = new TicketsReservedDomainEvent(Guid.NewGuid());

        Result firstAttempt = await idempotentHandler.Handle(reservation);

        await idempotentHandler.Handle(reservation);

        bool consumerRecorded = await database.ConsumerExistsAsync(
            database.OutboxTable,
            reservation.Id,
            nameof(ReserveTicketsDomainEventHandler));

        firstAttempt.Error.Should().Be(BrokerDown);

        consumerRecorded.Should().BeFalse();

        reserveTickets.HandledEventIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandledIntegrationEvent_IsRecorded_AndNotHandledAgain()
    {
        var registerAttendee = new HandlerScript();

        var registerAttendeeHandler = new RegisterAttendeeIntegrationEventHandler(registerAttendee);

        var idempotentHandler = new IdempotentIntegrationEventHandler<AttendeeRegisteredIntegrationEvent>(
            registerAttendeeHandler,
            database.ConnectionFactory,
            database.InboxTable);

        var attendeeRegistered = new AttendeeRegisteredIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());

        await idempotentHandler.Handle(attendeeRegistered);

        Result secondAttempt = await idempotentHandler.Handle(attendeeRegistered);

        bool consumerRecorded = await database.ConsumerExistsAsync(
            database.InboxTable,
            attendeeRegistered.Id,
            nameof(RegisterAttendeeIntegrationEventHandler));

        secondAttempt.IsSuccess.Should().BeTrue();

        consumerRecorded.Should().BeTrue();

        registerAttendee.HandledEventIds.Should().Equal(attendeeRegistered.Id);
    }

    [Fact]
    public async Task FailedIntegrationEvent_IsNotRecorded_SoItsRetryRunsTheHandlerAgain()
    {
        var registerAttendee = new HandlerScript { Outcome = BrokerDownOutcome };

        var registerAttendeeHandler = new RegisterAttendeeIntegrationEventHandler(registerAttendee);

        var idempotentHandler = new IdempotentIntegrationEventHandler<AttendeeRegisteredIntegrationEvent>(
            registerAttendeeHandler,
            database.ConnectionFactory,
            database.InboxTable);

        var attendeeRegistered = new AttendeeRegisteredIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());

        await idempotentHandler.Handle(attendeeRegistered);

        await idempotentHandler.Handle(attendeeRegistered);

        bool consumerRecorded = await database.ConsumerExistsAsync(
            database.InboxTable,
            attendeeRegistered.Id,
            nameof(RegisterAttendeeIntegrationEventHandler));

        consumerRecorded.Should().BeFalse();

        registerAttendee.HandledEventIds.Should().HaveCount(2);
    }
}
