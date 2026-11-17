using Evently.Common.Infrastructure.Messaging;

namespace Evently.Common.Infrastructure.Outbox;

public sealed class OutboxWakeUps(
    IEnumerable<string> lanes) : LaneWakeUps(lanes);
