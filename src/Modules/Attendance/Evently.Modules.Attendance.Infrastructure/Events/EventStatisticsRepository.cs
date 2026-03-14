using Evently.Modules.Attendance.Application.EventStatistics;
using MongoDB.Driver;

namespace Evently.Modules.Attendance.Infrastructure.Events;

internal sealed class EventStatisticsRepository : IEventStatisticsRepository
{
    private readonly IMongoCollection<EventStatistics> _collection;

    public EventStatisticsRepository(IMongoClient mongoClient)
    {
        IMongoDatabase mongoDatabase = mongoClient.GetDatabase(DocumentDbSettings.Database);
        _collection = mongoDatabase.GetCollection<EventStatistics>(DocumentDbSettings.EventStatistics);
    }

    public async Task<EventStatistics> GetAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        FilterDefinition<EventStatistics> filter = Builders<EventStatistics>.Filter.Eq(x => x.EventId, eventId);

        EventStatistics eventStatistics = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

        return eventStatistics;
    }

    public async Task InsertAsync(EventStatistics eventStatistics, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(eventStatistics, new InsertOneOptions(), cancellationToken);
    }

    public async Task ReplaceAsync(EventStatistics eventStatistics, CancellationToken cancellationToken = default)
    {
        FilterDefinition<EventStatistics> filter =
            Builders<EventStatistics>.Filter.Eq(x => x.EventId, eventStatistics.EventId);

        await _collection.ReplaceOneAsync(filter, eventStatistics, new ReplaceOptions(), cancellationToken);
    }
}
