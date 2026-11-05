using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;

namespace Evently.Modules.Events.Infrastructure.Outbox;

internal static class OutboxLanes
{
    public const string Default = MessageLanes.Default;

    public static IReadOnlyList<string> All { get; } = [Default];

    public static string Of(IDomainEvent _) => Default;
}
