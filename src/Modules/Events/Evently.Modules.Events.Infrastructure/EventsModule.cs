using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.Outbox;
using Evently.Common.Presentation.Endpoints;
using Evently.Modules.Events.Application.Abstractions.Data;
using Evently.Modules.Events.Domain.Categories;
using Evently.Modules.Events.Domain.Events;
using Evently.Modules.Events.Domain.TicketTypes;
using Evently.Modules.Events.Infrastructure.Categories;
using Evently.Modules.Events.Infrastructure.Database;
using Evently.Modules.Events.Infrastructure.Events;
using Evently.Modules.Events.Infrastructure.Inbox;
using Evently.Modules.Events.Infrastructure.Outbox;
using Evently.Modules.Events.Infrastructure.TicketTypes;
using Evently.Modules.Events.Presentation;
using Evently.Modules.Events.Presentation.Events.CancelEventSaga;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Evently.Modules.Events.Infrastructure;

public static class EventsModule
{
    internal const string ModuleName = "Events";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddEventsModule(IConfiguration configuration)
        {
            services.AddInfrastructure(configuration);

            services.AddEndpoints(AssemblyReference.Assembly);

            return services;
        }

        private void AddInfrastructure(IConfiguration configuration)
        {
            services.AddDbContext<EventsDbContext>((sp, options) =>
                options
                    .UseNpgsql(
                        configuration.GetConnectionString("Database"),
                        npgsqlOptions => npgsqlOptions
                            .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Events))
                    .UseSnakeCaseNamingConvention()
                    .AddInterceptors(sp.GetRequiredKeyedService<InsertOutboxMessagesInterceptor>(ModuleName)));

            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<EventsDbContext>());

            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<ITicketTypeRepository, TicketTypeRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();

            var outbox = new OutboxDefinition(
                ModuleName,
                Schemas.Events,
                Application.AssemblyReference.Assembly,
                OutboxLanes.All,
                OutboxLanes.Of);

            IConfigurationSection outboxSection = configuration.GetSection("Events:Outbox");

            services.AddOutbox(outbox, outboxSection);

            var inbox = new InboxDefinition(
                ModuleName,
                Schemas.Events,
                AssemblyReference.Assembly,
                InboxLanes.All);

            IConfigurationSection inboxSection = configuration.GetSection("Events:Inbox");

            services.AddInbox(inbox, inboxSection);
        }
    }

    public static Action<IRegistrationConfigurator, string> ConfigureConsumers(string redisConnectionString)
    {
        return (registration, instanceId) => registration
            .AddSagaStateMachine<CancelEventSaga, CancelEventState>()
            .Endpoint(c => c.InstanceId = instanceId)
            .RedisRepository(redisConnectionString);
    }
}
