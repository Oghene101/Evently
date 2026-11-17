using Evently.Common.Application.Messaging;

namespace Evently.Modules.Events.Application.Events.GetEvents;

public sealed record GetEventsQuery : IQuery<IReadOnlyList<EventResponse>>;
