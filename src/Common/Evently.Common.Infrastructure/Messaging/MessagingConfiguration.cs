using System.Reflection;
using Evently.Common.Application.EventBus;
using Evently.Common.Application.Messaging;
using Evently.Common.Infrastructure.Inbox;
using Evently.Common.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;

namespace Evently.Common.Infrastructure.Messaging;

public static class MessagingConfiguration
{
    extension(IServiceCollection services)
    {
        // The module's DbContext adds the interceptor registered here under the module's name.
        public IServiceCollection AddOutbox(
            OutboxDefinition outbox,
            IConfiguration outboxSection)
        {
            var outboxTable = MessageTable.Outbox(outbox.ModuleName, outbox.Schema);

            var outboxWakeUps = new OutboxWakeUps(outbox.Lanes);

            var outboxMessagesInterceptor = new InsertOutboxMessagesInterceptor(outbox.LaneOf, outboxWakeUps);

            var domainEventDispatcher = new DomainEventDispatcher(outbox.HandlersAssembly);

            services.AddMessageBoxOptions<OutboxOptions>(outbox.ModuleName, outboxSection, outbox.Lanes);

            services.AddKeyedSingleton(outbox.ModuleName, outboxMessagesInterceptor);

            services.AddDomainEventHandlers(outbox.HandlersAssembly, outboxTable);

            services.AddMessageProcessing<OutboxOptions>(outboxTable, domainEventDispatcher, outboxWakeUps, outbox.Lanes);

            return services;
        }

        public IServiceCollection AddInbox(
            InboxDefinition inbox,
            IConfiguration inboxSection)
        {
            var inboxTable = MessageTable.Inbox(inbox.ModuleName, inbox.Schema);

            var inboxWakeUps = new InboxWakeUps(inbox.Lanes);

            var integrationEventDispatcher = new IntegrationEventDispatcher(inbox.HandlersAssembly);

            services.AddMessageBoxOptions<InboxOptions>(inbox.ModuleName, inboxSection, inbox.Lanes);

            // The module's inbox consumer resolves this under the module's name to wake a lane once it stores a message.
            services.AddKeyedSingleton(inbox.ModuleName, inboxWakeUps);

            services.AddIntegrationEventHandlers(inbox.HandlersAssembly, inboxTable);

            services.AddMessageProcessing<InboxOptions>(inboxTable, integrationEventDispatcher, inboxWakeUps, inbox.Lanes);

            return services;
        }

        private void AddMessageBoxOptions<TOptions>(
            string moduleName,
            IConfiguration messageBoxSection,
            IReadOnlyList<string> lanes)
            where TOptions : MessageBoxOptions
        {
            services.AddOptions<TOptions>(moduleName)
                .Bind(messageBoxSection)
                .Validate(
                    messageBoxOptions => messageBoxOptions.HasValidSettingsFor(lanes),
                    $"{moduleName} needs settings for the {string.Join(", ", lanes)} lanes, each with a positive "
                    + "Concurrency, PollIntervalMilliseconds and MaxRetries, a RetryDelaySeconds of zero or more, and "
                    + "a MaxPollIntervalMilliseconds no shorter than PollIntervalMilliseconds.")
                .ValidateOnStart();
        }

        private void AddDomainEventHandlers(
            Assembly handlersAssembly,
            MessageTable outboxTable)
        {
            Type[] domainEventHandlers = handlersAssembly
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

                services.Decorate(
                    domainEventHandler,
                    (decorated, serviceProvider) =>
                        ActivatorUtilities.CreateInstance(serviceProvider, closedIdempotentHandler, decorated, outboxTable));
            }
        }

        private void AddIntegrationEventHandlers(
            Assembly handlersAssembly,
            MessageTable inboxTable)
        {
            Type[] integrationEventHandlers = handlersAssembly
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

                services.Decorate(
                    integrationEventHandler,
                    (decorated, serviceProvider) =>
                        ActivatorUtilities.CreateInstance(serviceProvider, closedIdempotentHandler, decorated, inboxTable));
            }
        }

        // One processor per lane, so a slow or failing kind of message never holds up another.
        private void AddMessageProcessing<TOptions>(
            MessageTable table,
            IMessageDispatcher dispatcher,
            LaneWakeUps laneWakeUps,
            IReadOnlyList<string> lanes)
            where TOptions : MessageBoxOptions
        {
            services.TryAddScoped<HandledMessageConsumers>();

            foreach (string lane in lanes)
            {
                services.AddSingleton<IHostedService>(serviceProvider =>
                {
                    TOptions messageBoxOptions = serviceProvider
                        .GetRequiredService<IOptionsMonitor<TOptions>>()
                        .Get(table.ModuleName);

                    MessageLaneSettings laneSettings = messageBoxOptions.LaneSettings(lane);

                    MessageProcessor messageProcessor =
                        ActivatorUtilities.CreateInstance<MessageProcessor>(serviceProvider, table, dispatcher);

                    return ActivatorUtilities.CreateInstance<MessageLaneProcessor>(
                        serviceProvider,
                        table,
                        lane,
                        laneSettings,
                        messageBoxOptions.LeaseSeconds,
                        messageProcessor,
                        laneWakeUps);
                });
            }

            services.AddSingleton<IConfigureOptions<QuartzOptions>>(serviceProvider =>
            {
                TOptions messageBoxOptions = serviceProvider
                    .GetRequiredService<IOptionsMonitor<TOptions>>()
                    .Get(table.ModuleName);

                return new ConfigurePurgeProcessedMessagesJob(table, messageBoxOptions);
            });
        }
    }
}
