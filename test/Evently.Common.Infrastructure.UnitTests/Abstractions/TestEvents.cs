using Evently.Common.Application.EventBus;
using Evently.Common.Domain;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

public sealed class TicketsReservedDomainEvent(
    Guid reservationId) : DomainEvent
{
    public Guid ReservationId { get; init; } = reservationId;
}

public sealed class SeatsCountedDomainEvent(
    int seats) : DomainEvent
{
    public int Seats { get; init; } = seats;
}

public sealed class AttendeeRegisteredIntegrationEvent(
    Guid id,
    DateTime occurredOnUtc,
    Guid attendeeId) : IntegrationEvent(id, occurredOnUtc)
{
    public Guid AttendeeId { get; init; } = attendeeId;
}
