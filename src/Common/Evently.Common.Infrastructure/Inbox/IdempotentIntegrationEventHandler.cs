using System.Data.Common;
using Dapper;
using Evently.Common.Application.Data;
using Evently.Common.Application.EventBus;
using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;

namespace Evently.Common.Infrastructure.Inbox;

// The handled-record is saved by MessageProcessor together with the message's outcome, sparing a round trip per handler.
public sealed class IdempotentIntegrationEventHandler<TIntegrationEvent>(
    IIntegrationEventHandler<TIntegrationEvent> decorated,
    IDbConnectionFactory dbConnectionFactory,
    MessageTable inboxTable,
    HandledMessageConsumers handledMessageConsumers)
    : IntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    public override async Task<Result> Handle(
        TIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var inboxMessageConsumer = new InboxMessageConsumer(integrationEvent.Id, decorated.GetType().Name);

        bool alreadyHandled = await InboxConsumerExistsAsync(inboxMessageConsumer);

        if (alreadyHandled)
        {
            return Result.Success();
        }

        Result handled = await decorated.Handle(integrationEvent, cancellationToken);

        if (handled.IsFailure)
        {
            return handled;
        }

        handledMessageConsumers.Add(inboxMessageConsumer.Name);

        return handled;
    }

    private async Task<bool> InboxConsumerExistsAsync(InboxMessageConsumer inboxMessageConsumer)
    {
        string sql =
            $"""
             SELECT EXISTS(
                 SELECT 1
                 FROM {inboxTable.QualifiedConsumersTable}
                 WHERE inbox_message_id = @InboxMessageId AND
                       name = @Name
             )
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        return await connection.ExecuteScalarAsync<bool>(sql, inboxMessageConsumer);
    }
}
