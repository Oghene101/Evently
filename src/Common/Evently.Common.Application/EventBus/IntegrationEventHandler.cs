using Evently.Common.Domain;

namespace Evently.Common.Application.EventBus;

public abstract class IntegrationEventHandler<TIntegrationEvent> : IIntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    public abstract Task<Result> Handle(
        TIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);

    public Task<Result> Handle(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) =>
        Handle((TIntegrationEvent)integrationEvent, cancellationToken);
}
