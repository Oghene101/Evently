using Evently.Api.Extensions;
using Evently.Api.Middleware;
using Evently.Api.OpenTelemetry;
using Evently.Common.Application;
using Evently.Common.Infrastructure;
using Evently.Common.Infrastructure.Configuration;
using Evently.Common.Infrastructure.EventBus;
using Evently.Common.Presentation.Endpoints;
using Evently.Modules.Attendance.Infrastructure;
using Evently.Modules.Events.Infrastructure;
using Evently.Modules.Ticketing.Infrastructure;
using Evently.Modules.Users.Application;
using Evently.Modules.Users.Infrastructure;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using Scalar.AspNetCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApiInternal();
builder.Services.AddApiVersioningInternal();

builder.Services.AddApplication([
    AssemblyReference.Assembly,
    Evently.Modules.Events.Application.AssemblyReference.Assembly,
    Evently.Modules.Ticketing.Application.AssemblyReference.Assembly,
    Evently.Modules.Attendance.Application.AssemblyReference.Assembly
]);

string databaseConnectionString = builder.Configuration.GetConnectionString("Database")!;
string redisConnectionString = builder.Configuration.GetConnectionString("Cache")!;
string mongoConnectionString = builder.Configuration.GetConnectionString("Mongo")!;
var rabbitMqSettings = new RabbitMqSettings(builder.Configuration.GetConnectionStringOrThrow("Queue"));

builder.Services.AddInfrastructure(
    DiagnosticsConfig.ServiceName,
    [
        EventsModule.ConfigureConsumers(redisConnectionString),
        TicketingModule.ConfigureConsumers,
        AttendanceModule.ConfigureConsumers
    ],
    rabbitMqSettings,
    databaseConnectionString,
    redisConnectionString,
    mongoConnectionString);

builder.Configuration.AddModuleConfiguration(["events", "users", "ticketing", "attendance"]);

Uri keyCloakHealthUrl = builder.Configuration.GetKeyCloakHealthUrl();

builder.Services.AddHealthChecks()
    .AddNpgSql(databaseConnectionString)
    .AddRedis(redisConnectionString)
    .AddRabbitMQ(async sp =>
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqSettings.Host) };
        return await factory.CreateConnectionAsync();
    })
    .AddKeyCloak(keyCloakHealthUrl);

builder.Services.AddEventsModule(builder.Configuration);
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddTicketingModule(builder.Configuration);
builder.Services.AddAttendanceModule(builder.Configuration);

WebApplication app = builder.Build();

app.UseLogContextTraceLogging();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Evently")
            .WithTheme(ScalarTheme.Laserwave) //kepler, deepspace, laserwave, mars
            .EnableDarkMode()
            .SortTagsAlphabetically()
            .SortOperationsByMethod()
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
#pragma warning disable S3878
            .AddPreferredSecuritySchemes(["Bearer"]);
#pragma warning restore S3878

        options.AddDocument("v1", "Evently API v1", "/openapi/v1.json");
    });

    app.ApplyMigrations();
}

app.MapEndpoints();

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseAuthentication();

app.UseAuthorization();

await app.RunAsync();
