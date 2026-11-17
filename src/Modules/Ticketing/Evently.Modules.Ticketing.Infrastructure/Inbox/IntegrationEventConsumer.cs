using System.Data.Common;
using Dapper;
using Evently.Common.Application.Data;
using Evently.Common.Application.EventBus;
using Evently.Common.Infrastructure.Inbox;
using Evently.Common.Infrastructure.Serialization;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Evently.Modules.Ticketing.Infrastructure.Inbox;

internal sealed class IntegrationEventConsumer<TIntegrationEvent>(
    IDbConnectionFactory dbConnectionFactory,
    [FromKeyedServices(TicketingModule.ModuleName)] InboxWakeUps inboxWakeUps)
    : IConsumer<TIntegrationEvent>
    where TIntegrationEvent : IntegrationEvent
{
    public async Task Consume(ConsumeContext<TIntegrationEvent> context)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        TIntegrationEvent integrationEvent = context.Message;

        var inboxMessage = new InboxMessage
        {
            Id = integrationEvent.Id,
            Type = integrationEvent.GetType().Name,
            Lane = InboxLanes.Of(integrationEvent),
            Content = JsonConvert.SerializeObject(integrationEvent, SerializerSettings.Instance),
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        };

        const string sql =
            """
            INSERT INTO ticketing.inbox_messages(id, type, lane, content, occurred_on_utc)
            VALUES (@Id, @Type, @Lane, @Content::json, @OccurredOnUtc)
            """;

        await connection.ExecuteAsync(sql, inboxMessage);

        inboxWakeUps.WakeUp(inboxMessage.Lane);
    }
}
