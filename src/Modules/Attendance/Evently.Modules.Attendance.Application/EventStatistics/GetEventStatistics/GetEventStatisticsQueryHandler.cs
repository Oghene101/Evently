using Evently.Common.Application.Messaging;
using Evently.Common.Domain;

namespace Evently.Modules.Attendance.Application.EventStatistics.GetEventStatistics;

internal sealed class GetEventStatisticsQueryHandler(IEventStatisticsRepository eventStatisticsRepository)
    : IQueryHandler<GetEventStatisticsQuery, EventStatistics>
{
    public async Task<Result<EventStatistics>> Handle(
        GetEventStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        EventStatistics eventStatistics =
            await eventStatisticsRepository.GetAsync(request.EventId, cancellationToken);

        return eventStatistics;
    }
}
