using Dapper;
using Evently.Common.Application.Caching;
using Evently.Common.Application.Clock;
using Evently.Common.Application.Data;
using Evently.Common.Application.EventBus;
using Evently.Common.Infrastructure.Authentication;
using Evently.Common.Infrastructure.Authorization;
using Evently.Common.Infrastructure.Caching;
using Evently.Common.Infrastructure.Clock;
using Evently.Common.Infrastructure.Data;
using Evently.Common.Infrastructure.EventBus;
using Evently.Common.Infrastructure.Outbox;
using MassTransit;
using MassTransit.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;
using StackExchange.Redis;

namespace Evently.Common.Infrastructure;

public static class InfrastructureConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure(string serviceName,
            Action<IRegistrationConfigurator, string>[] moduleConfigureConsumers,
            RabbitMqSettings rabbitMqSettings,
            string databaseConnectionString,
            string redisConnectionString,
            string mongoConnectionString)
        {
            services.AddAuthenticationInternal();

            services.AddAuthorizationInternal();

            services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();

            services.TryAddSingleton<IEventBus, EventBus.EventBus>();

            services.TryAddSingleton<InsertOutboxMessagesInterceptor>();

            NpgsqlDataSource npgsqlDataSource = new NpgsqlDataSourceBuilder(databaseConnectionString).Build();
            services.TryAddSingleton(npgsqlDataSource);

            services.TryAddScoped<IDbConnectionFactory, DbConnectionFactory>();

            SqlMapper.AddTypeHandler(new GenericArrayHandler<string>());

            services.AddQuartz(configurator =>
            {
                var scheduler = Guid.NewGuid();
                configurator.SchedulerId = $"default-id-{scheduler}";
                configurator.SchedulerName = $"default-name-{scheduler}";
            });

            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

            try
            {
                IConnectionMultiplexer connectionMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
                services.AddSingleton(connectionMultiplexer);
                services.AddStackExchangeRedisCache(options =>
                    options.ConnectionMultiplexerFactory = () => Task.FromResult(connectionMultiplexer));
            }
            catch
            {
                services.AddDistributedMemoryCache();
            }

            services.TryAddSingleton<ICacheService, CacheService>();

            services.AddMassTransit(configure =>
            {
                string instanceId = serviceName.ToUpperInvariant().Replace('.', '-');
                foreach (Action<IRegistrationConfigurator, string> configureConsumers in moduleConfigureConsumers)
                {
                    configureConsumers(configure, instanceId);
                }

                configure.SetKebabCaseEndpointNameFormatter();

                configure.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(new Uri(rabbitMqSettings.Host), h =>
                    {
                        h.Username(rabbitMqSettings.Username);
                        h.Password(rabbitMqSettings.Password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            });

            services
                .AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddEntityFrameworkCoreInstrumentation()
                        .AddRedisInstrumentation()
                        .AddNpgsql()
                        .AddSource(DiagnosticHeaders.DefaultListenerName)
                        .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources");

                    tracing.AddOtlpExporter();
                });

            services.AddSingleton<IMongoClient>(sp =>
            {
                var mongoClientSettings = MongoClientSettings.FromConnectionString(mongoConnectionString);

                mongoClientSettings.ClusterConfigurator = c => c.Subscribe(
                    new DiagnosticsActivityEventSubscriber(
                        new InstrumentationOptions
                        {
                            CaptureCommandText = true
                        }));

                return new MongoClient(mongoClientSettings);
            });

            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            return services;
        }
    }
}
