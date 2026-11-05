using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.UnitTests.Abstractions;
using FluentAssertions;

namespace Evently.Common.Infrastructure.UnitTests.Messaging;

[Collection(nameof(MessagingDatabaseCollection))]
public sealed class MessageStoreTests(
    MessagingDatabase database)
{
    private static readonly TimeSpan TimestampPrecision = TimeSpan.FromMilliseconds(1);

    private readonly string _lane = Guid.NewGuid().ToString();

    private readonly DateTime _nowUtc = DateTime.UtcNow;

    private MessageTable OutboxTable => database.OutboxTable;

    [Fact]
    public async Task Claim_TakesOnlyClaimableMessagesOfItsLane_OldestFirst()
    {
        var oldest = Guid.NewGuid();
        var leaseLapsed = Guid.NewGuid();
        var dueForRetry = Guid.NewGuid();
        var newest = Guid.NewGuid();

        DateTime nineMinutesAgoUtc = _nowUtc.AddMinutes(-9);
        DateTime fiveMinutesAgoUtc = _nowUtc.AddMinutes(-5);
        DateTime fourMinutesAgoUtc = _nowUtc.AddMinutes(-4);
        DateTime oneMinuteAgoUtc = _nowUtc.AddMinutes(-1);
        DateTime oneSecondAgoUtc = _nowUtc.AddSeconds(-1);
        DateTime inOneMinuteUtc = _nowUtc.AddMinutes(1);
        DateTime claimedUntilUtc = _nowUtc.AddMinutes(2);

        string otherLane = Guid.NewGuid().ToString();

        await database.InsertAsync(OutboxTable, newest, "{}", _lane, occurredOnUtc: oneMinuteAgoUtc);
        await database.InsertAsync(OutboxTable, oldest, "{}", _lane, occurredOnUtc: nineMinutesAgoUtc);
        await database.InsertAsync(OutboxTable, leaseLapsed, "{}", _lane, occurredOnUtc: fiveMinutesAgoUtc, claimedUntilUtc: oneSecondAgoUtc);
        await database.InsertAsync(OutboxTable, dueForRetry, "{}", _lane, occurredOnUtc: fourMinutesAgoUtc, nextRetryOnUtc: oneSecondAgoUtc);
        await database.InsertAsync(OutboxTable, Guid.NewGuid(), "{}", _lane, claimedUntilUtc: inOneMinuteUtc);
        await database.InsertAsync(OutboxTable, Guid.NewGuid(), "{}", _lane, nextRetryOnUtc: inOneMinuteUtc);
        await database.InsertAsync(OutboxTable, Guid.NewGuid(), "{}", _lane, processedOnUtc: _nowUtc);
        await database.InsertAsync(OutboxTable, Guid.NewGuid(), "{}", otherLane);

        var outboxStore = new MessageStore(OutboxTable, database.ConnectionFactory);

        IReadOnlyList<Guid> claimedIds = await outboxStore.ClaimAsync(_lane, 10, _nowUtc, claimedUntilUtc);

        StoredMessageRow? claimedOutboxMessage = await database.StoredMessageAsync(OutboxTable, oldest);

        claimedIds.Should().Equal(oldest, leaseLapsed, dueForRetry, newest);

        claimedOutboxMessage!.ClaimedUntilUtc.Should().BeCloseTo(claimedUntilUtc, TimestampPrecision);
    }

    [Fact]
    public async Task Claim_TakesNoMoreThanItsLimit_AndNeverAMessageWhoseLeaseHolds()
    {
        foreach (int _ in Enumerable.Range(0, 3))
        {
            await database.InsertAsync(OutboxTable, Guid.NewGuid(), "{}", _lane);
        }

        DateTime claimedUntilUtc = _nowUtc.AddMinutes(2);

        var outboxStore = new MessageStore(OutboxTable, database.ConnectionFactory);

        IReadOnlyList<Guid> firstClaim = await outboxStore.ClaimAsync(_lane, 2, _nowUtc, claimedUntilUtc);

        IReadOnlyList<Guid> secondClaim = await outboxStore.ClaimAsync(_lane, 10, _nowUtc, claimedUntilUtc);

        IReadOnlyList<Guid> thirdClaim = await outboxStore.ClaimAsync(_lane, 10, _nowUtc, claimedUntilUtc);

        firstClaim.Should().HaveCount(2);

        secondClaim.Should().ContainSingle();

        firstClaim.Should().NotIntersectWith(secondClaim);

        thirdClaim.Should().BeEmpty();
    }
}
