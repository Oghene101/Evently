using System.Data.Common;
using Dapper;
using Evently.Common.Application.Data;
using Evently.Common.Application.Messaging;
using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;

namespace Evently.Common.Infrastructure.Outbox;

public sealed class IdempotentDomainEventHandler<TDomainEvent>(
    IDomainEventHandler<TDomainEvent> decorated,
    IDbConnectionFactory dbConnectionFactory,
    MessageTable outboxTable)
    : DomainEventHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public override async Task<Result> Handle(TDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        var outboxMessageConsumer = new OutboxMessageConsumer(domainEvent.Id, decorated.GetType().Name);

        bool alreadyHandled = await OutboxConsumerExistsAsync(connection, outboxMessageConsumer);

        if (alreadyHandled)
        {
            return Result.Success();
        }

        Result handled = await decorated.Handle(domainEvent, cancellationToken);

        if (handled.IsFailure)
        {
            return handled;
        }

        await InsertOutboxConsumerAsync(connection, outboxMessageConsumer);

        return handled;
    }

    private async Task<bool> OutboxConsumerExistsAsync(
        DbConnection dbConnection,
        OutboxMessageConsumer outboxMessageConsumer)
    {
        string sql =
            $"""
             SELECT EXISTS(
                 SELECT 1
                 FROM {outboxTable.QualifiedConsumersTable}
                 WHERE outbox_message_id = @OutboxMessageId AND
                       name = @Name
             )
             """;

        return await dbConnection.ExecuteScalarAsync<bool>(sql, outboxMessageConsumer);
    }

    private async Task InsertOutboxConsumerAsync(
        DbConnection dbConnection,
        OutboxMessageConsumer outboxMessageConsumer)
    {
        string sql =
            $"""
             INSERT INTO {outboxTable.QualifiedConsumersTable}(outbox_message_id, name)
             VALUES (@OutboxMessageId, @Name)
             """;

        await dbConnection.ExecuteAsync(sql, outboxMessageConsumer);
    }
}
