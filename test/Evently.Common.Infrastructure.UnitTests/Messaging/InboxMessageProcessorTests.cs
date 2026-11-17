using Evently.Common.Domain;
using Evently.Common.Infrastructure.Inbox;
using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.UnitTests.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Evently.Common.Infrastructure.UnitTests.Messaging;

[Collection(nameof(MessagingDatabaseCollection))]
public sealed class InboxMessageProcessorTests(
    MessagingDatabase database) : IDisposable
{
    private static readonly Error DatabaseBusy = Error.Dependency("Database.Busy", "The database timed out");

    private static readonly Error AttendeeExists = Error.Conflict("Attendee.Exists", "The attendee already exists");

    private static readonly Result DatabaseBusyOutcome = Result.Failure(DatabaseBusy);

    private static readonly Result AttendeeExistsOutcome = Result.Failure(AttendeeExists);

    private static readonly TimeSpan TimestampPrecision = TimeSpan.FromMilliseconds(1);

    private static readonly MessageLaneSettings Lane = new()
    {
        Concurrency = 1,
        PollIntervalMilliseconds = 100,
        MaxPollIntervalMilliseconds = 100,
        MaxRetries = 3,
        RetryDelaySeconds = 10
    };

    private readonly FixedDateTimeProvider _clock = new();

    private readonly List<ServiceProvider> _serviceProviders = [];

    private MessageTable InboxTable => database.InboxTable;

    [Fact]
    public async Task HandledMessage_IsMarkedProcessed_AndItsContentCleared()
    {
        var registerAttendee = new HandlerScript();

        MessageProcessor processor = BuildProcessor(registerAttendee);

        Guid messageId = await WriteAttendeeRegisteredAsync();

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedInboxMessage = await database.StoredMessageAsync(InboxTable, messageId);

        storedInboxMessage!.ProcessedOnUtc.Should().BeCloseTo(_clock.UtcNow, TimestampPrecision);
        storedInboxMessage.Content.Should().Be("{}");

        registerAttendee.HandledEventIds.Should().Equal(messageId);
    }

    [Fact]
    public async Task DependencyFailure_IsScheduledForRetry_WithExponentialBackoff()
    {
        var registerAttendee = new HandlerScript { Outcome = DatabaseBusyOutcome };

        MessageProcessor processor = BuildProcessor(registerAttendee);

        Guid messageId = await WriteAttendeeRegisteredAsync(retryCount: 1);

        DateTime secondRetryOnUtc = _clock.UtcNow.AddSeconds(Lane.RetryDelaySeconds * 2);

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedInboxMessage = await database.StoredMessageAsync(InboxTable, messageId);

        storedInboxMessage!.ProcessedOnUtc.Should().BeNull();
        storedInboxMessage.RetryCount.Should().Be(2);
        storedInboxMessage.NextRetryOnUtc.Should().BeCloseTo(secondRetryOnUtc, TimestampPrecision);
    }

    [Fact]
    public async Task ThrowingHandler_IsRetried()
    {
        var registerAttendee = new HandlerScript { Throws = true };

        MessageProcessor processor = BuildProcessor(registerAttendee);

        Guid messageId = await WriteAttendeeRegisteredAsync();

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedInboxMessage = await database.StoredMessageAsync(InboxTable, messageId);

        storedInboxMessage!.RetryCount.Should().Be(1);
        storedInboxMessage.NextRetryOnUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task FinalFailure_IsDeadLetteredAtOnce()
    {
        var registerAttendee = new HandlerScript { Outcome = AttendeeExistsOutcome };

        MessageProcessor processor = BuildProcessor(registerAttendee);

        Guid messageId = await WriteAttendeeRegisteredAsync();

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        StoredMessageRow? storedInboxMessage = await database.StoredMessageAsync(InboxTable, messageId);

        DeadLetteredMessageRow? deadLetteredInboxMessage =
            await database.DeadLetteredMessageAsync(InboxTable, messageId);

        storedInboxMessage.Should().BeNull();

        deadLetteredInboxMessage!.Error.Should().Be(AttendeeExists.Description);
    }

    [Fact]
    public async Task ExhaustedRetries_AreDeadLettered()
    {
        var registerAttendee = new HandlerScript { Outcome = DatabaseBusyOutcome };

        MessageProcessor processor = BuildProcessor(registerAttendee);

        Guid messageId = await WriteAttendeeRegisteredAsync(retryCount: Lane.MaxRetries - 1);

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        DeadLetteredMessageRow? deadLetteredInboxMessage =
            await database.DeadLetteredMessageAsync(InboxTable, messageId);

        deadLetteredInboxMessage!.RetryCount.Should().Be(Lane.MaxRetries);
    }

    [Fact]
    public async Task UnreadableContent_IsDeadLettered_WithoutRunningAHandler()
    {
        var registerAttendee = new HandlerScript();

        MessageProcessor processor = BuildProcessor(registerAttendee);

        var messageId = Guid.NewGuid();

        await database.InsertAsync(InboxTable, messageId, """{"$type":"No.Such.Type, No.Such.Assembly"}""");

        await processor.ProcessAsync(messageId, Lane, CancellationToken.None);

        DeadLetteredMessageRow? deadLetteredInboxMessage =
            await database.DeadLetteredMessageAsync(InboxTable, messageId);

        deadLetteredInboxMessage.Should().NotBeNull();

        registerAttendee.HandledEventIds.Should().BeEmpty();
    }

    public void Dispose()
    {
        foreach (ServiceProvider serviceProvider in _serviceProviders)
        {
            serviceProvider.Dispose();
        }
    }

    private async Task<Guid> WriteAttendeeRegisteredAsync(int retryCount = 0)
    {
        var attendeeRegistered = new AttendeeRegisteredIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());

        string content = StoredRows.Serialize(attendeeRegistered);

        await database.InsertAsync(InboxTable, attendeeRegistered.Id, content, retryCount: retryCount);

        return attendeeRegistered.Id;
    }

    private MessageProcessor BuildProcessor(HandlerScript registerAttendee)
    {
        var registerAttendeeHandler = new RegisterAttendeeIntegrationEventHandler(registerAttendee);

        var services = new ServiceCollection();

        services.AddSingleton(registerAttendeeHandler);
        services.AddScoped(_ => database.ConnectionFactory);
        services.AddScoped<HandledMessageConsumers>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _serviceProviders.Add(serviceProvider);

        var integrationEventDispatcher =
            new IntegrationEventDispatcher(typeof(AttendeeRegisteredIntegrationEvent).Assembly);

        IServiceScopeFactory serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return new MessageProcessor(
            InboxTable,
            integrationEventDispatcher,
            serviceScopeFactory,
            _clock,
            NullLogger<MessageProcessor>.Instance);
    }
}
