using System.Data.Common;
using Dapper;
using Evently.Common.Application.Data;
using Evently.Common.Application.Messaging;
using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;

namespace Evently.Common.Infrastructure.Outbox;

// The handled-record is saved by MessageProcessor together with the message's outcome, sparing a round trip per handler.
public sealed class IdempotentDomainEventHandler<TDomainEvent>(
    IDomainEventHandler<TDomainEvent> decorated,
    IDbConnectionFactory dbConnectionFactory,
    MessageTable outboxTable,
    HandledMessageConsumers handledMessageConsumers)
    : DomainEventHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public override async Task<Result> Handle(TDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var outboxMessageConsumer = new OutboxMessageConsumer(domainEvent.Id, decorated.GetType().Name);

        bool alreadyHandled = await OutboxConsumerExistsAsync(outboxMessageConsumer);

        if (alreadyHandled)
        {
            return Result.Success();
        }

        Result handled = await decorated.Handle(domainEvent, cancellationToken);

        if (handled.IsFailure)
        {
            return handled;
        }

        handledMessageConsumers.Add(outboxMessageConsumer.Name);

        return handled;
    }

    private async Task<bool> OutboxConsumerExistsAsync(OutboxMessageConsumer outboxMessageConsumer)
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

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        return await connection.ExecuteScalarAsync<bool>(sql, outboxMessageConsumer);
    }
}
