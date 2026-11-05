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
