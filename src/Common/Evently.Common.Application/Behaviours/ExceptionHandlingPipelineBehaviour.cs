using Evently.Common.Application.Exceptions;
using Evently.Common.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Evently.Common.Application.Behaviours;

internal sealed class ExceptionHandlingPipelineBehaviour<TRequest, TResponse>(
    ILogger<ExceptionHandlingPipelineBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for {RequestName}", typeof(TRequest).Name);
            throw new EventlyException(typeof(TRequest).Name, innerException: exception);
        }
    }
}
