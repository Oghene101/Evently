using Evently.Common.Application.Clock;
using Evently.Common.Application.Data;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Evently.Common.Infrastructure.Messaging;

[DisallowConcurrentExecution]
public sealed class PurgeProcessedMessagesJob(
    IDbConnectionFactory dbConnectionFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<PurgeProcessedMessagesJob> logger) : IJob
{
    public const string MessageTableKey = nameof(MessageTable);

    public const string RetentionDaysKey = nameof(MessageBoxOptions.RetentionDays);

    public const int PurgeBatchSize = 5_000;

    public async Task Execute(IJobExecutionContext context)
    {
        var table = (MessageTable)context.MergedJobDataMap[MessageTableKey];

        int retentionDays = context.MergedJobDataMap.GetInt(RetentionDaysKey);

        DateTime processedBeforeUtc = dateTimeProvider.UtcNow.AddDays(-retentionDays);

        var messageStore = new MessageStore(table, dbConnectionFactory);

        long purgedMessages = 0;

        long purgedConsumers = 0;

        PurgedMessages purgedBatch;

        do
        {
            purgedBatch = await messageStore.PurgeProcessedBatchAsync(processedBeforeUtc, PurgeBatchSize);

            purgedMessages += purgedBatch.Messages;

            purgedConsumers += purgedBatch.Consumers;
        }
        while (purgedBatch.Messages == PurgeBatchSize);

        logger.LogInformation(
            "{Module} - Purged {PurgedMessages} {Table} messages processed before {ProcessedBeforeUtc} and {PurgedConsumers} consumer records",
            table.ModuleName,
            purgedMessages,
            table.MessagesTable,
            processedBeforeUtc,
            purgedConsumers);
    }
}
