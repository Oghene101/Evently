using System.Reflection;
using Evently.Common.Application.EventBus;
using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.Serialization;
using Newtonsoft.Json;

namespace Evently.Common.Infrastructure.Inbox;

public sealed class IntegrationEventDispatcher(
    Assembly handlersAssembly) : IMessageDispatcher
{
    // The first failure stops the handlers after it; they run again on the retry, guarded by the idempotent decorator.
    public async Task<Result> DispatchAsync(
        string content,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        Result<IIntegrationEvent> readIntegrationEvent = Read(content);

        if (readIntegrationEvent.IsFailure)
        {
            return readIntegrationEvent;
        }

        IIntegrationEvent integrationEvent = readIntegrationEvent.Value;

        IEnumerable<IIntegrationEventHandler> integrationEventHandlers = IntegrationEventHandlersFactory.GetHandlers(
            integrationEvent.GetType(),
            scopedServices,
            handlersAssembly);

        foreach (IIntegrationEventHandler integrationEventHandler in integrationEventHandlers)
        {
            Result handled = await integrationEventHandler.Handle(integrationEvent, cancellationToken);

            if (handled.IsFailure)
            {
                return handled;
            }
        }

        return Result.Success();
    }

    private static Result<IIntegrationEvent> Read(string content)
    {
        try
        {
            IIntegrationEvent integrationEvent =
                JsonConvert.DeserializeObject<IIntegrationEvent>(content, SerializerSettings.Instance)
                ?? throw new JsonSerializationException("The content holds no integration event");

            return Result.Success(integrationEvent);
        }
        catch (JsonException exception)
        {
            return Result.Failure<IIntegrationEvent>(MessageErrors.Unreadable(exception));
        }
    }
}
