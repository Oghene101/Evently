using System.Data.Common;
using Dapper;
using Evently.Common.Application.Data;
using Evently.Common.Application.EventBus;
using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;

namespace Evently.Common.Infrastructure.Inbox;

public sealed class IdempotentIntegrationEventHandler<TIntegrationEvent>(
    IIntegrationEventHandler<TIntegrationEvent> decorated,
    IDbConnectionFactory dbConnectionFactory,
    MessageTable inboxTable)
    : IntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    public override async Task<Result> Handle(
        TIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        var inboxMessageConsumer = new InboxMessageConsumer(integrationEvent.Id, decorated.GetType().Name);

        bool alreadyHandled = await InboxConsumerExistsAsync(connection, inboxMessageConsumer);

        if (alreadyHandled)
        {
            return Result.Success();
        }

        Result handled = await decorated.Handle(integrationEvent, cancellationToken);

        if (handled.IsFailure)
        {
            return handled;
        }

        await InsertInboxConsumerAsync(connection, inboxMessageConsumer);

        return handled;
    }

    private async Task<bool> InboxConsumerExistsAsync(
        DbConnection dbConnection,
        InboxMessageConsumer inboxMessageConsumer)
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

        return await dbConnection.ExecuteScalarAsync<bool>(sql, inboxMessageConsumer);
    }

    private async Task InsertInboxConsumerAsync(
        DbConnection dbConnection,
        InboxMessageConsumer inboxMessageConsumer)
    {
        string sql =
            $"""
             INSERT INTO {inboxTable.QualifiedConsumersTable}(inbox_message_id, name)
             VALUES (@InboxMessageId, @Name)
             """;

        await dbConnection.ExecuteAsync(sql, inboxMessageConsumer);
    }
}
