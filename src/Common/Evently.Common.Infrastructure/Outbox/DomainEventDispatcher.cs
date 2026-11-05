using System.Reflection;
using Evently.Common.Application.Messaging;
using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.Serialization;
using Newtonsoft.Json;

namespace Evently.Common.Infrastructure.Outbox;

public sealed class DomainEventDispatcher(
    Assembly handlersAssembly) : IMessageDispatcher
{
    // The first failure stops the handlers after it; they run again on the retry, guarded by the idempotent decorator.
    public async Task<Result> DispatchAsync(
        string content,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        Result<IDomainEvent> readDomainEvent = Read(content);

        if (readDomainEvent.IsFailure)
        {
            return readDomainEvent;
        }

        IDomainEvent domainEvent = readDomainEvent.Value;

        IEnumerable<IDomainEventHandler> domainEventHandlers = DomainEventHandlersFactory.GetHandlers(
            domainEvent.GetType(),
            scopedServices,
            handlersAssembly);

        foreach (IDomainEventHandler domainEventHandler in domainEventHandlers)
        {
            Result handled = await domainEventHandler.Handle(domainEvent, cancellationToken);

            if (handled.IsFailure)
            {
                return handled;
            }
        }

        return Result.Success();
    }

    private static Result<IDomainEvent> Read(string content)
    {
        try
        {
            IDomainEvent domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(content, SerializerSettings.Instance)
                                       ?? throw new JsonSerializationException("The content holds no domain event");

            return Result.Success(domainEvent);
        }
        catch (JsonException exception)
        {
            return Result.Failure<IDomainEvent>(MessageErrors.Unreadable(exception));
        }
    }
}
