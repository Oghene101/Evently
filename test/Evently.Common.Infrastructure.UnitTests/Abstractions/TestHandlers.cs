using Evently.Common.Application.EventBus;
using Evently.Common.Application.Messaging;
using Evently.Common.Domain;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

public sealed class ReserveTicketsDomainEventHandler(
    HandlerScript script) : DomainEventHandler<TicketsReservedDomainEvent>
{
    public override Task<Result> Handle(
        TicketsReservedDomainEvent domainEvent,
        CancellationToken cancellationToken = default) => script.RunAsync(domainEvent.Id);
}

public sealed class NotifyReservationDomainEventHandler(
    HandlerScript script) : DomainEventHandler<TicketsReservedDomainEvent>
{
    public override Task<Result> Handle(
        TicketsReservedDomainEvent domainEvent,
        CancellationToken cancellationToken = default) => script.RunAsync(domainEvent.Id);
}

public sealed class CountSeatsDomainEventHandler(
    HandlerScript script) : DomainEventHandler<SeatsCountedDomainEvent>
{
    public override Task<Result> Handle(
        SeatsCountedDomainEvent domainEvent,
        CancellationToken cancellationToken = default) => script.RunAsync(domainEvent.Id);
}

public sealed class RegisterAttendeeIntegrationEventHandler(
    HandlerScript script) : IntegrationEventHandler<AttendeeRegisteredIntegrationEvent>
{
    public override Task<Result> Handle(
        AttendeeRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) => script.RunAsync(integrationEvent.Id);
}
