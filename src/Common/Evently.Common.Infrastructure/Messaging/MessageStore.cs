using System.Data.Common;
using Dapper;
using Evently.Common.Application.Data;

namespace Evently.Common.Infrastructure.Messaging;

public sealed class MessageStore(
    MessageTable table,
    IDbConnectionFactory dbConnectionFactory)
{
    // Content a processed message keeps: the event's details are not needed once it has been handled.
    private const string ClearedContent = "{}";

    // Saved in the same statement as the message's outcome, so a retry skips the handlers that already succeeded.
    private string SaveHandledConsumersSql =>
        $"""
         WITH handled_consumers AS (
             INSERT INTO {table.QualifiedConsumersTable}({table.ConsumerMessageIdColumn}, name)
             SELECT @MessageId, unnest(@HandledConsumers::text[])
             ON CONFLICT DO NOTHING)
         """;

    public async Task<IReadOnlyList<Guid>> ClaimAsync(
        string lane,
        int limit,
        DateTime nowUtc,
        DateTime claimedUntilUtc)
    {
        string sql =
            $"""
             WITH claimed AS (
                 UPDATE {table.QualifiedMessagesTable}
                 SET claimed_until_utc = @ClaimedUntilUtc
                 WHERE id IN (
                     SELECT id
                     FROM {table.QualifiedMessagesTable}
                     WHERE lane = @Lane
                       AND processed_on_utc IS NULL
                       AND available_on_utc <= @NowUtc
                       AND (claimed_until_utc IS NULL OR claimed_until_utc < @NowUtc)
                     ORDER BY available_on_utc
                     LIMIT @Limit
                     FOR UPDATE SKIP LOCKED)
                 RETURNING id, available_on_utc)
             SELECT id
             FROM claimed
             ORDER BY available_on_utc
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        IEnumerable<Guid> claimedIds = await connection.QueryAsync<Guid>(
            sql,
            new
            {
                Lane = lane,
                Limit = limit,
                NowUtc = nowUtc,
                ClaimedUntilUtc = claimedUntilUtc
            });

        return claimedIds.AsList();
    }

    public async Task<StoredMessage?> GetUnprocessedAsync(Guid messageId)
    {
        string sql =
            $"""
             SELECT
                id AS {nameof(StoredMessage.Id)},
                type AS {nameof(StoredMessage.Type)},
                content AS {nameof(StoredMessage.Content)},
                retry_count AS {nameof(StoredMessage.RetryCount)}
             FROM {table.QualifiedMessagesTable}
             WHERE id = @MessageId AND processed_on_utc IS NULL
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<StoredMessage>(sql, new { MessageId = messageId });
    }

    public async Task MarkProcessedAsync(
        Guid messageId,
        DateTime processedOnUtc,
        string[] handledConsumers)
    {
        string sql =
            $"""
             {SaveHandledConsumersSql}
             UPDATE {table.QualifiedMessagesTable}
             SET content = @ClearedContent::jsonb,
                 processed_on_utc = @ProcessedOnUtc,
                 claimed_until_utc = NULL,
                 next_retry_on_utc = NULL,
                 error = NULL
             WHERE id = @MessageId
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            sql,
            new
            {
                MessageId = messageId,
                ClearedContent,
                ProcessedOnUtc = processedOnUtc,
                HandledConsumers = handledConsumers
            });
    }

    public async Task ScheduleRetryAsync(
        Guid messageId,
        int retryCount,
        string error,
        DateTime nextRetryOnUtc,
        string[] handledConsumers)
    {
        string sql =
            $"""
             {SaveHandledConsumersSql}
             UPDATE {table.QualifiedMessagesTable}
             SET retry_count = @RetryCount,
                 error = @Error,
                 next_retry_on_utc = @NextRetryOnUtc,
                 claimed_until_utc = NULL
             WHERE id = @MessageId
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            sql,
            new
            {
                MessageId = messageId,
                RetryCount = retryCount,
                Error = error,
                NextRetryOnUtc = nextRetryOnUtc,
                HandledConsumers = handledConsumers
            });
    }

    public async Task DeadLetterAsync(
        Guid messageId,
        int retryCount,
        string error,
        DateTime deadLetteredOnUtc,
        string[] handledConsumers)
    {
        string sql =
            $"""
             {SaveHandledConsumersSql},
             dead_lettered AS (
                 DELETE FROM {table.QualifiedMessagesTable}
                 WHERE id = @MessageId
                 RETURNING id, type, content, occurred_on_utc)
             INSERT INTO {table.QualifiedDeadLetteredMessagesTable}
                 (id, type, content, occurred_on_utc, dead_lettered_on_utc, retry_count, error)
             SELECT id, type, content, occurred_on_utc, @DeadLetteredOnUtc, @RetryCount, @Error
             FROM dead_lettered
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            sql,
            new
            {
                MessageId = messageId,
                RetryCount = retryCount,
                Error = error,
                DeadLetteredOnUtc = deadLetteredOnUtc,
                HandledConsumers = handledConsumers
            });
    }

    // A consumer record only guards against handling its message twice, so it goes when the message does. A batch keeps
    // each statement's locks and WAL small, even after a backlog of a few million rows.
    public async Task<PurgedMessages> PurgeProcessedBatchAsync(
        DateTime processedBeforeUtc,
        int batchSize)
    {
        string sql =
            $"""
             WITH purged_messages AS (
                 DELETE FROM {table.QualifiedMessagesTable}
                 WHERE id IN (
                     SELECT id
                     FROM {table.QualifiedMessagesTable}
                     WHERE processed_on_utc < @ProcessedBeforeUtc
                     LIMIT @BatchSize)
                 RETURNING id),
             purged_consumers AS (
                 DELETE FROM {table.QualifiedConsumersTable}
                 WHERE {table.ConsumerMessageIdColumn} IN (SELECT id FROM purged_messages)
                 RETURNING 1)
             SELECT
                (SELECT COUNT(*) FROM purged_messages) AS {nameof(PurgedMessages.Messages)},
                (SELECT COUNT(*) FROM purged_consumers) AS {nameof(PurgedMessages.Consumers)}
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        return await connection.QuerySingleAsync<PurgedMessages>(
            sql,
            new
            {
                ProcessedBeforeUtc = processedBeforeUtc,
                BatchSize = batchSize
            });
    }
}
