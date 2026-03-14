using System.Reflection;
using Evently.Common.Application.Behaviours;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Evently.Common.Application;

public static class ApplicationConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication(Assembly[] moduleAssemblies)
        {
            services.AddMediatR(config =>
            {
                config.RegisterServicesFromAssemblies(moduleAssemblies);
                config.AddOpenBehavior(typeof(ExceptionHandlingPipelineBehaviour<,>));
                config.AddOpenBehavior(typeof(RequestLoggingPipelineBehaviour<,>));
                config.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
            });

            services.AddValidatorsFromAssemblies(moduleAssemblies, includeInternalTypes: true);

            return services;
        }
    }
}
