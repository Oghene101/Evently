using Evently.Common.Domain;

namespace Evently.Common.Application.Messaging;

public abstract class DomainEventHandler<TDomainEvent> : IDomainEventHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public abstract Task<Result> Handle(TDomainEvent domainEvent, CancellationToken cancellationToken = default);

    public Task<Result> Handle(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        return Handle((TDomainEvent)domainEvent, cancellationToken);
    }
}
