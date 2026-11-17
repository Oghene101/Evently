using Evently.Common.Infrastructure.Messaging;

namespace Evently.Common.Infrastructure.Inbox;

public sealed class InboxWakeUps(
    IEnumerable<string> lanes) : LaneWakeUps(lanes);
