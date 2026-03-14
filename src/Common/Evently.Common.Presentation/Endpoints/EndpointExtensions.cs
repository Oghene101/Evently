using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Evently.Common.Presentation.Endpoints;

#pragma warning disable CA1708
public static class EndpointExtensions
#pragma warning restore CA1708
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddEndpoints(params Assembly[] assemblies)
        {
            ServiceDescriptor[] serviceDescriptors =
            [
                .. assemblies
                    .SelectMany(a => a.GetTypes())
                    .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                                   type.IsAssignableTo(typeof(IEndpoint)))
                    .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            ];

            services.TryAddEnumerable(serviceDescriptors);

            return services;
        }
    }

    extension(WebApplication app)
    {
        public IApplicationBuilder MapEndpoints()
        {
            ApiVersionSet apiVersionSet = app.NewApiVersionSet()
                .HasApiVersion(new ApiVersion(1))
                .ReportApiVersions()
                .Build();

            RouteGroupBuilder routeGroup = app
                .MapGroup("api/v{apiVersion:apiVersion}/")
                .WithApiVersionSet(apiVersionSet);

            IEnumerable<IEndpoint> endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

            foreach (IEndpoint endpoint in endpoints)
            {
                endpoint.MapEndpoint(routeGroup);
            }

            return app;
        }
    }
}
