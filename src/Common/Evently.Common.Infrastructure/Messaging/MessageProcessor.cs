using Evently.Common.Application.Clock;
using Evently.Common.Application.Data;
using Evently.Common.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Evently.Common.Infrastructure.Messaging;

// Handles one claimed message in its own scope and records the outcome. Nothing is held open over the handlers:
// the claim's lease is what keeps other processors away meanwhile.
public sealed class MessageProcessor(
    MessageTable table,
    IMessageDispatcher dispatcher,
    IServiceScopeFactory serviceScopeFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<MessageProcessor> logger)
{
    public async Task ProcessAsync(
        Guid messageId,
        MessageLaneSettings laneSettings,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        IDbConnectionFactory dbConnectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        var messageStore = new MessageStore(table, dbConnectionFactory);

        StoredMessage? storedMessage = await messageStore.GetUnprocessedAsync(messageId);

        if (storedMessage is null)
        {
            return;
        }

        Result outcome = await DispatchAsync(storedMessage, scope.ServiceProvider, cancellationToken);

        if (outcome.IsSuccess)
        {
            DateTime processedOnUtc = dateTimeProvider.UtcNow;

            await messageStore.MarkProcessedAsync(messageId, processedOnUtc);

            return;
        }

        await RecordFailureAsync(messageStore, storedMessage, outcome.Error, laneSettings);
    }

    private async Task<Result> DispatchAsync(
        StoredMessage storedMessage,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        try
        {
            return await dispatcher.DispatchAsync(storedMessage.Content, scopedServices, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "{Module} - Exception while handling {Table} message {MessageId}",
                table.ModuleName,
                table.MessagesTable,
                storedMessage.Id);

            return Result.Failure(MessageErrors.HandlerThrew(exception));
        }
    }

    private async Task RecordFailureAsync(
        MessageStore messageStore,
        StoredMessage storedMessage,
        Error failure,
        MessageLaneSettings laneSettings)
    {
        int retryCount = storedMessage.RetryCount + 1;

        bool retryable = failure.Type == ErrorType.Dependency && retryCount < laneSettings.MaxRetries;

        if (retryable)
        {
            DateTime nextRetryOnUtc = NextRetryOnUtc(retryCount, laneSettings);

            await messageStore.ScheduleRetryAsync(storedMessage.Id, retryCount, failure.Description, nextRetryOnUtc);

            logger.LogWarning(
                "{Module} - {Table} message {MessageId} failed with {ErrorCode}; retry {RetryCount}/{MaxRetries} at {NextRetryOnUtc}",
                table.ModuleName,
                table.MessagesTable,
                storedMessage.Id,
                failure.Code,
                retryCount,
                laneSettings.MaxRetries,
                nextRetryOnUtc);

            return;
        }

        DateTime deadLetteredOnUtc = dateTimeProvider.UtcNow;

        await messageStore.DeadLetterAsync(storedMessage.Id, retryCount, failure.Description, deadLetteredOnUtc);

        logger.LogCritical(
            "{Module} - {Table} message {MessageId} failed with {ErrorCode} after {RetryCount} attempts; dead-lettered",
            table.ModuleName,
            table.MessagesTable,
            storedMessage.Id,
            failure.Code,
            retryCount);
    }

    private DateTime NextRetryOnUtc(
        int retryCount,
        MessageLaneSettings laneSettings)
    {
        double delaySeconds = laneSettings.RetryDelaySeconds * Math.Pow(2, retryCount - 1);

        return dateTimeProvider.UtcNow.AddSeconds(delaySeconds);
    }
}
