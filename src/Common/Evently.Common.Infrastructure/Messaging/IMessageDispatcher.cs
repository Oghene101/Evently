using Evently.Common.Domain;

namespace Evently.Common.Infrastructure.Messaging;

public interface IMessageDispatcher
{
    Task<Result> DispatchAsync(
        string content,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken);
}
