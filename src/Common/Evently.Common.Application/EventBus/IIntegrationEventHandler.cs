using Evently.Common.Domain;

namespace Evently.Common.Application.EventBus;

public interface IIntegrationEventHandler<in TIntegrationEvent> : IIntegrationEventHandler
    where TIntegrationEvent : IIntegrationEvent
{
    Task<Result> Handle(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}

public interface IIntegrationEventHandler
{
    Task<Result> Handle(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
