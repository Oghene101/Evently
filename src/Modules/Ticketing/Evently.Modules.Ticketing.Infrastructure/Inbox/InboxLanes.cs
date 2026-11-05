using Evently.Common.Application.EventBus;
using Evently.Common.Infrastructure.Messaging;

namespace Evently.Modules.Ticketing.Infrastructure.Inbox;

internal static class InboxLanes
{
    public const string Default = MessageLanes.Default;

    public static IReadOnlyList<string> All { get; } = [Default];

    public static string Of(IIntegrationEvent _) => Default;
}
