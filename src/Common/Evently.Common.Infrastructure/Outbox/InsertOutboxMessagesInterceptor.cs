using Evently.Common.Domain;
using Evently.Common.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;

namespace Evently.Common.Infrastructure.Outbox;

public sealed class InsertOutboxMessagesInterceptor(
    Func<IDomainEvent, string> laneOf,
    OutboxWakeUps outboxWakeUps) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            InsertOutboxMessages(eventData.Context);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            WakeUpLanesOfSavedMessages(eventData.Context);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    // Outbox messages are only ever added through this context; the processors update them with Dapper.
    private void WakeUpLanesOfSavedMessages(DbContext context)
    {
        string[] lanesOfSavedMessages =
        [
            .. context.ChangeTracker
                .Entries<OutboxMessage>()
                .Select(entry => entry.Entity.Lane)
                .Distinct()
        ];

        foreach (string lane in lanesOfSavedMessages)
        {
            outboxWakeUps.WakeUp(lane);
        }
    }

    private void InsertOutboxMessages(DbContext context)
    {
        var outboxMessages = context
            .ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                IReadOnlyCollection<IDomainEvent> domainEvents = [.. entity.DomainEvents];

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .Select(domainEvent => new OutboxMessage
            {
                Id = domainEvent.Id,
                Type = domainEvent.GetType().Name,
                Lane = laneOf(domainEvent),
                Content = JsonConvert.SerializeObject(domainEvent, SerializerSettings.Instance),
                OccurredOnUtc = domainEvent.OccurredOnUtc
            })
            .ToList();

        context.Set<OutboxMessage>().AddRange(outboxMessages);
    }
}
