using System.Data.Common;
using Dapper;
using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.UnitTests.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;

namespace Evently.Common.Infrastructure.UnitTests.Messaging;

[Collection(nameof(MessagingDatabaseCollection))]
public sealed class PurgeProcessedMessagesJobTests(
    MessagingDatabase database)
{
    private const int RetentionDays = 7;

    private const string ConsumerName = "PurgeTestHandler";

    private readonly FixedDateTimeProvider _clock = new();

    [Fact]
    public async Task Purge_DeletesOnlyMessagesProcessedBeforeTheRetention_WithTheirConsumers()
    {
        MessageTable inboxTable = database.InboxTable;

        var processedLongAgo = Guid.NewGuid();
        var processedRecently = Guid.NewGuid();
        var unprocessedLongAgo = Guid.NewGuid();

        DateTime longAgoUtc = _clock.UtcNow.AddDays(-RetentionDays - 3);
        DateTime recentlyUtc = _clock.UtcNow.AddDays(-RetentionDays + 1);

        await database.InsertAsync(inboxTable, processedLongAgo, "{}", occurredOnUtc: longAgoUtc, processedOnUtc: longAgoUtc);
        await database.InsertAsync(inboxTable, processedRecently, "{}", occurredOnUtc: recentlyUtc, processedOnUtc: recentlyUtc);
        await database.InsertAsync(inboxTable, unprocessedLongAgo, "{}", occurredOnUtc: longAgoUtc);

        await database.InsertConsumerAsync(inboxTable, processedLongAgo, ConsumerName);
        await database.InsertConsumerAsync(inboxTable, processedRecently, ConsumerName);

        IJobExecutionContext jobContext = PurgeJobContext(inboxTable);

        var purgeJob = new PurgeProcessedMessagesJob(
            database.ConnectionFactory,
            _clock,
            NullLogger<PurgeProcessedMessagesJob>.Instance);

        await purgeJob.Execute(jobContext);

        StoredMessageRow? purgedInboxMessage = await database.StoredMessageAsync(inboxTable, processedLongAgo);

        StoredMessageRow? keptProcessedInboxMessage = await database.StoredMessageAsync(inboxTable, processedRecently);

        StoredMessageRow? keptUnprocessedInboxMessage = await database.StoredMessageAsync(inboxTable, unprocessedLongAgo);

        bool purgedConsumerExists = await database.ConsumerExistsAsync(inboxTable, processedLongAgo, ConsumerName);

        bool keptConsumerExists = await database.ConsumerExistsAsync(inboxTable, processedRecently, ConsumerName);

        purgedInboxMessage.Should().BeNull();

        purgedConsumerExists.Should().BeFalse();

        keptProcessedInboxMessage.Should().NotBeNull();

        keptConsumerExists.Should().BeTrue();

        keptUnprocessedInboxMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task Purge_DeletesInBatches_UntilNothingIsLeftToPurge()
    {
        MessageTable outboxTable = database.OutboxTable;

        string lane = Guid.NewGuid().ToString();

        DateTime longAgoUtc = _clock.UtcNow.AddDays(-RetentionDays - 3);

        int messageCount = PurgeProcessedMessagesJob.PurgeBatchSize + 1;

        await InsertProcessedMessagesAsync(outboxTable, lane, messageCount, longAgoUtc);

        IJobExecutionContext jobContext = PurgeJobContext(outboxTable);

        var purgeJob = new PurgeProcessedMessagesJob(
            database.ConnectionFactory,
            _clock,
            NullLogger<PurgeProcessedMessagesJob>.Instance);

        await purgeJob.Execute(jobContext);

        long messagesLeft = await CountMessagesAsync(outboxTable, lane);

        messagesLeft.Should().Be(0);
    }

    private async Task InsertProcessedMessagesAsync(
        MessageTable table,
        string lane,
        int messageCount,
        DateTime processedOnUtc)
    {
        string sql =
            $"""
             INSERT INTO {table.QualifiedMessagesTable}(id, type, lane, content, occurred_on_utc, processed_on_utc, retry_count)
             SELECT gen_random_uuid(), 'Test', @Lane, @EmptyContent::jsonb, @ProcessedOnUtc, @ProcessedOnUtc, 0
             FROM generate_series(1, @MessageCount)
             """;

        await using DbConnection connection = await database.ConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            sql,
            new
            {
                Lane = lane,
                EmptyContent = "{}",
                ProcessedOnUtc = processedOnUtc,
                MessageCount = messageCount
            });
    }

    private async Task<long> CountMessagesAsync(
        MessageTable table,
        string lane)
    {
        string sql = $"SELECT COUNT(*) FROM {table.QualifiedMessagesTable} WHERE lane = @Lane";

        await using DbConnection connection = await database.ConnectionFactory.OpenConnectionAsync();

        return await connection.ExecuteScalarAsync<long>(sql, new { Lane = lane });
    }

    private static IJobExecutionContext PurgeJobContext(MessageTable table)
    {
        var purgeJobData = new JobDataMap
        {
            [PurgeProcessedMessagesJob.MessageTableKey] = table,
            [PurgeProcessedMessagesJob.RetentionDaysKey] = RetentionDays
        };

        IJobExecutionContext jobContext = Substitute.For<IJobExecutionContext>();

        jobContext.MergedJobDataMap.Returns(purgeJobData);

        return jobContext;
    }
}
