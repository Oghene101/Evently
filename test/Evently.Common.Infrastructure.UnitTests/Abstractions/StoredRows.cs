using System.Data.Common;
using Dapper;
using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.Serialization;
using Newtonsoft.Json;

namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

public sealed record StoredMessageRow(
    Guid Id,
    string Content,
    DateTime? ProcessedOnUtc,
    DateTime? NextRetryOnUtc,
    DateTime? ClaimedUntilUtc,
    int RetryCount,
    string? Error);

public sealed record DeadLetteredMessageRow(
    Guid Id,
    string Content,
    int RetryCount,
    string Error);

public static class StoredRows
{
    public static string Serialize(object message) => JsonConvert.SerializeObject(message, SerializerSettings.Instance);

    public static async Task InsertAsync(
        this MessagingDatabase database,
        MessageTable table,
        Guid id,
        string content,
        string lane = MessageLanes.Default,
        int retryCount = 0,
        DateTime? occurredOnUtc = null,
        DateTime? processedOnUtc = null,
        DateTime? nextRetryOnUtc = null,
        DateTime? claimedUntilUtc = null)
    {
        string sql =
            $"""
             INSERT INTO {table.QualifiedMessagesTable}
                 (id, type, lane, content, occurred_on_utc, processed_on_utc, next_retry_on_utc, claimed_until_utc, retry_count)
             VALUES (@Id, 'Test', @Lane, @Content::jsonb, @OccurredOnUtc, @ProcessedOnUtc, @NextRetryOnUtc, @ClaimedUntilUtc, @RetryCount)
             """;

        await using DbConnection connection = await database.ConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            sql,
            new
            {
                Id = id,
                Lane = lane,
                Content = content,
                OccurredOnUtc = occurredOnUtc ?? DateTime.UtcNow,
                ProcessedOnUtc = processedOnUtc,
                NextRetryOnUtc = nextRetryOnUtc,
                ClaimedUntilUtc = claimedUntilUtc,
                RetryCount = retryCount
            });
    }

    public static async Task InsertConsumerAsync(
        this MessagingDatabase database,
        MessageTable table,
        Guid messageId,
        string name)
    {
        string sql =
            $"""
             INSERT INTO {table.QualifiedConsumersTable}({table.ConsumerMessageIdColumn}, name)
             VALUES (@MessageId, @Name)
             """;

        await using DbConnection connection = await database.ConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(sql, new { MessageId = messageId, Name = name });
    }

    public static async Task<StoredMessageRow?> StoredMessageAsync(
        this MessagingDatabase database,
        MessageTable table,
        Guid id)
    {
        string sql =
            $"""
             SELECT
                id AS {nameof(StoredMessageRow.Id)},
                content::text AS {nameof(StoredMessageRow.Content)},
                processed_on_utc AS {nameof(StoredMessageRow.ProcessedOnUtc)},
                next_retry_on_utc AS {nameof(StoredMessageRow.NextRetryOnUtc)},
                claimed_until_utc AS {nameof(StoredMessageRow.ClaimedUntilUtc)},
                retry_count AS {nameof(StoredMessageRow.RetryCount)},
                error AS {nameof(StoredMessageRow.Error)}
             FROM {table.QualifiedMessagesTable}
             WHERE id = @Id
             """;

        await using DbConnection connection = await database.ConnectionFactory.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<StoredMessageRow>(sql, new { Id = id });
    }

    public static async Task<DeadLetteredMessageRow?> DeadLetteredMessageAsync(
        this MessagingDatabase database,
        MessageTable table,
        Guid id)
    {
        string sql =
            $"""
             SELECT
                id AS {nameof(DeadLetteredMessageRow.Id)},
                content::text AS {nameof(DeadLetteredMessageRow.Content)},
                retry_count AS {nameof(DeadLetteredMessageRow.RetryCount)},
                error AS {nameof(DeadLetteredMessageRow.Error)}
             FROM {table.QualifiedDeadLetteredMessagesTable}
             WHERE id = @Id
             """;

        await using DbConnection connection = await database.ConnectionFactory.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<DeadLetteredMessageRow>(sql, new { Id = id });
    }

    public static async Task<bool> ConsumerExistsAsync(
        this MessagingDatabase database,
        MessageTable table,
        Guid messageId,
        string name)
    {
        string sql =
            $"""
             SELECT EXISTS(
                 SELECT 1
                 FROM {table.QualifiedConsumersTable}
                 WHERE {table.ConsumerMessageIdColumn} = @MessageId AND name = @Name)
             """;

        await using DbConnection connection = await database.ConnectionFactory.OpenConnectionAsync();

        return await connection.ExecuteScalarAsync<bool>(sql, new { MessageId = messageId, Name = name });
    }
}
