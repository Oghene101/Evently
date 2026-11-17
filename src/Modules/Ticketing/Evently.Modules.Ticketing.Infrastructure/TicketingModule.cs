using Evently.Common.Infrastructure.Messaging;
using Evently.Common.Infrastructure.Outbox;
using Evently.Common.Presentation.Endpoints;
using Evently.Modules.Events.IntegrationEvents;
using Evently.Modules.Ticketing.Application.Abstractions.Authentication;
using Evently.Modules.Ticketing.Application.Abstractions.Data;
using Evently.Modules.Ticketing.Application.Abstractions.Payments;
using Evently.Modules.Ticketing.Application.Carts;
using Evently.Modules.Ticketing.Domain.Customers;
using Evently.Modules.Ticketing.Domain.Events;
using Evently.Modules.Ticketing.Domain.Orders;
using Evently.Modules.Ticketing.Domain.Payments;
using Evently.Modules.Ticketing.Domain.Tickets;
using Evently.Modules.Ticketing.Infrastructure.Authentication;
using Evently.Modules.Ticketing.Infrastructure.Customers;
using Evently.Modules.Ticketing.Infrastructure.Database;
using Evently.Modules.Ticketing.Infrastructure.Events;
using Evently.Modules.Ticketing.Infrastructure.Inbox;
using Evently.Modules.Ticketing.Infrastructure.Orders;
using Evently.Modules.Ticketing.Infrastructure.Outbox;
using Evently.Modules.Ticketing.Infrastructure.Payments;
using Evently.Modules.Ticketing.Infrastructure.Tickets;
using Evently.Modules.Ticketing.Presentation;
using Evently.Modules.Users.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Evently.Modules.Ticketing.Infrastructure;

public static class TicketingModule
{
    internal const string ModuleName = "Ticketing";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddTicketingModule(IConfiguration configuration)
        {
            services.AddInfrastructure(configuration);

            services.AddEndpoints(AssemblyReference.Assembly);

            return services;
        }

        private void AddInfrastructure(IConfiguration configuration)
        {
            services.AddDbContext<TicketingDbContext>((sp, options) =>
                options
                    .UseNpgsql(
                        configuration.GetConnectionString("Database"),
                        npgsqlOptions => npgsqlOptions
                            .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Ticketing))
                    .AddInterceptors(sp.GetRequiredKeyedService<InsertOutboxMessagesInterceptor>(ModuleName))
                    .UseSnakeCaseNamingConvention());

            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<ITicketTypeRepository, TicketTypeRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<ITicketRepository, TicketRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();

            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TicketingDbContext>());

            services.AddSingleton<CartService>();
            services.AddSingleton<IPaymentService, PaymentService>();

            services.AddScoped<ICustomerContext, CustomerContext>();

            var outbox = new OutboxDefinition(
                ModuleName,
                Schemas.Ticketing,
                Application.AssemblyReference.Assembly,
                OutboxLanes.All,
                OutboxLanes.Of);

            IConfigurationSection outboxSection = configuration.GetSection("Ticketing:Outbox");

            services.AddOutbox(outbox, outboxSection);

            var inbox = new InboxDefinition(
                ModuleName,
                Schemas.Ticketing,
                AssemblyReference.Assembly,
                InboxLanes.All);

            IConfigurationSection inboxSection = configuration.GetSection("Ticketing:Inbox");

            services.AddInbox(inbox, inboxSection);
        }
    }

    public static void ConfigureConsumers(IRegistrationConfigurator registrationConfigurator, string instanceId)
    {
        registrationConfigurator.AddConsumer<IntegrationEventConsumer<UserRegisteredIntegrationEvent>>()
            .Endpoint(c => c.InstanceId = instanceId);

        registrationConfigurator.AddConsumer<IntegrationEventConsumer<UserProfileUpdatedIntegrationEvent>>()
            .Endpoint(c => c.InstanceId = instanceId);

        registrationConfigurator.AddConsumer<IntegrationEventConsumer<EventPublishedIntegrationEvent>>()
            .Endpoint(c => c.InstanceId = instanceId);

        registrationConfigurator.AddConsumer<IntegrationEventConsumer<TicketTypePriceChangedIntegrationEvent>>()
            .Endpoint(c => c.InstanceId = instanceId);

        registrationConfigurator.AddConsumer<IntegrationEventConsumer<EventCancellationStartedIntegrationEvent>>()
            .Endpoint(c => c.InstanceId = instanceId);
    }
}
