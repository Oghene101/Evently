using Evently.Common.Application.Messaging;
using Evently.Common.Domain;
using Evently.Modules.Attendance.Domain.Events;

namespace Evently.Modules.Attendance.Application.EventStatistics.Projections;

internal sealed class EventCreatedDomainEventHandler(
    IEventStatisticsRepository eventStatisticsRepository)
    : DomainEventHandler<EventCreatedDomainEvent>
{
    public override async Task<Result> Handle(
        EventCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        await eventStatisticsRepository.InsertAsync(
            EventStatistics.Create(
                domainEvent.EventId,
                domainEvent.Title,
                domainEvent.Description,
                domainEvent.Location,
                domainEvent.StartsAtUtc,
                domainEvent.EndsAtUtc),
            cancellationToken);

        return Result.Success();
    }
}
