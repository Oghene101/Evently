using Evently.Common.Application.Authorization;
using Evently.Common.Application.EventBus;
using Evently.Common.Application.Messaging;
using Evently.Common.Infrastructure.Outbox;
using Evently.Common.Presentation.Endpoints;
using Evently.Modules.Users.Application.Abstractions.Data;
using Evently.Modules.Users.Application.Abstractions.Identity;
using Evently.Modules.Users.Domain.Users;
using Evently.Modules.Users.Infrastructure.Authorization;
using Evently.Modules.Users.Infrastructure.Database;
using Evently.Modules.Users.Infrastructure.Identity;
using Evently.Modules.Users.Infrastructure.Inbox;
using Evently.Modules.Users.Infrastructure.Outbox;
using Evently.Modules.Users.Infrastructure.Users;
using Evently.Modules.Users.Presentation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Evently.Modules.Users.Infrastructure;

public static class UsersModule
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUsersModule(IConfiguration configuration)
        {
            services.AddDomainEventHandlers();

            services.AddIntegrationEventHandlers();

            services.AddInfrastructure(configuration);

            services.AddEndpoints(AssemblyReference.Assembly);

            return services;
        }

        private void AddInfrastructure(IConfiguration configuration)
        {
            services.AddScoped<IPermissionService, PermissionService>();

            services.Configure<KeyCloakOptions>(configuration.GetSection("Users:KeyCloak"));

            services.AddTransient<KeyCloakAuthDelegatingHandler>();

            services
                .AddHttpClient<KeyCloakClient>((serviceProvider, httpClient) =>
                {
                    KeyCloakOptions keycloakOptions = serviceProvider
                        .GetRequiredService<IOptions<KeyCloakOptions>>().Value;

                    httpClient.BaseAddress = new Uri(keycloakOptions.AdminUrl);
                })
                .AddHttpMessageHandler<KeyCloakAuthDelegatingHandler>();

            services.AddTransient<IIdentityProviderService, IdentityProviderService>();

            services.AddDbContext<UsersDbContext>((sp, options) =>
                options
                    .UseNpgsql(
                        configuration.GetConnectionString("Database"),
                        npgsqlOptions => npgsqlOptions
                            .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Users))
                    .AddInterceptors(sp.GetRequiredService<InsertOutboxMessagesInterceptor>())
                    .UseSnakeCaseNamingConvention());

            services.AddScoped<IUserRepository, UserRepository>();

            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UsersDbContext>());

            services.Configure<OutboxOptions>(configuration.GetSection("Users:Outbox"));

            services.ConfigureOptions<ConfigureProcessOutboxJob>();

            services.Configure<InboxOptions>(configuration.GetSection("Users:Inbox"));

            services.ConfigureOptions<ConfigureProcessInboxJob>();
        }

        private void AddDomainEventHandlers()
        {
            Type[] domainEventHandlers = Application.AssemblyReference.Assembly
                .GetTypes()
                .Where(t => t.IsAssignableTo(typeof(IDomainEventHandler)))
                .ToArray();

            foreach (Type domainEventHandler in domainEventHandlers)
            {
                services.TryAddScoped(domainEventHandler);

                Type domainEvent = domainEventHandler
                    .GetInterfaces()
                    .Single(i => i.IsGenericType)
                    .GetGenericArguments()
                    .Single();

                Type closedIdempotentHandler = typeof(IdempotentDomainEventHandler<>).MakeGenericType(domainEvent);

                services.Decorate(domainEventHandler, closedIdempotentHandler);
            }
        }

        private void AddIntegrationEventHandlers()
        {
            Type[] integrationEventHandlers = AssemblyReference.Assembly
                .GetTypes()
                .Where(t => t.IsAssignableTo(typeof(IIntegrationEventHandler)))
                .ToArray();

            foreach (Type integrationEventHandler in integrationEventHandlers)
            {
                services.TryAddScoped(integrationEventHandler);

                Type integrationEvent = integrationEventHandler
                    .GetInterfaces()
                    .Single(i => i.IsGenericType)
                    .GetGenericArguments()
                    .Single();

                Type closedIdempotentHandler =
                    typeof(IdempotentIntegrationEventHandler<>).MakeGenericType(integrationEvent);

                services.Decorate(integrationEventHandler, closedIdempotentHandler);
            }
        }
    }
}
