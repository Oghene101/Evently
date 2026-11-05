using Evently.Common.Domain;

namespace Evently.Common.Application.Messaging;

public interface IDomainEventHandler<in TDomainEvent> : IDomainEventHandler
    where TDomainEvent : IDomainEvent
{
    Task<Result> Handle(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IDomainEventHandler
{
    Task<Result> Handle(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
