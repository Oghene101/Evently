using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume();

// The resource name is the connection-string key every module reads; the second argument is the database name.
IResourceBuilder<PostgresDatabaseResource> database = postgres.AddDatabase("Database", "evently");

IResourceBuilder<RedisResource> cache = builder.AddRedis("Cache");

// MassTransit takes the host from the connection string but always signs in with RabbitMqSettings' guest/guest.
IResourceBuilder<ParameterResource> queueUsername = builder.AddParameter("queue-username", "guest");

IResourceBuilder<ParameterResource> queuePassword = builder.AddParameter("queue-password", "guest", secret: true);

IResourceBuilder<RabbitMQServerResource> queue = builder
    .AddRabbitMQ("Queue", queueUsername, queuePassword)
    .WithManagementPlugin();

// Newer mongo images refuse Docker Desktop's kernel.
IResourceBuilder<MongoDBServerResource> mongo = builder
    .AddMongoDB("Mongo")
    .WithImageTag("7.0");

IResourceBuilder<KeycloakResource> keycloak = builder
    .AddKeycloak("identity", port: 18080)
    .WithDataVolume()
    .WithRealmImport("../../.files");

// Aspire serves this endpoint over HTTPS with the dev certificate despite its "http" name.
EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");

EndpointReference keycloakManagement = keycloak.GetEndpoint("management");

var realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/evently");

var metadataAddress = ReferenceExpression.Create($"{realmUrl}/.well-known/openid-configuration");

var keycloakHealthUrl = ReferenceExpression.Create($"{keycloakManagement}/health/ready");

var keycloakAdminUrl = ReferenceExpression.Create($"{keycloakEndpoint}/admin/realms/evently/");

var keycloakTokenUrl = ReferenceExpression.Create($"{realmUrl}/protocol/openid-connect/token");

IResourceBuilder<ProjectResource> api = builder.AddProject<Evently_Api>("evently-api")
    .WithReference(database)
    .WithReference(cache)
    .WithReference(queue)
    .WithReference(mongo)
    .WithEnvironment("Authentication__MetadataAddress", metadataAddress)
    .WithEnvironment("Authentication__TokenValidationParameters__ValidIssuer", realmUrl)
    .WithEnvironment("KeyCloak__HealthUrl", keycloakHealthUrl)
    .WithEnvironment("Users__KeyCloak__AdminUrl", keycloakAdminUrl)
    .WithEnvironment("Users__KeyCloak__TokenUrl", keycloakTokenUrl)
    .WithHttpHealthCheck("/health")
    .WaitFor(database)
    .WaitFor(cache)
    .WaitFor(queue)
    .WaitFor(mongo)
    .WaitFor(keycloak);

EndpointReference apiHttp = api.GetEndpoint("http");

builder.AddProject<Evently_Gateway>("evently-gateway")
    .WithEnvironment("Authentication__MetadataAddress", metadataAddress)
    .WithEnvironment("Authentication__TokenValidationParameters__ValidIssuer", realmUrl)
    .WithEnvironment("ReverseProxy__Clusters__evently-cluster__Destinations__destination1__Address", apiHttp)
    .WaitFor(api)
    .WaitFor(keycloak);

await builder.Build().RunAsync();
