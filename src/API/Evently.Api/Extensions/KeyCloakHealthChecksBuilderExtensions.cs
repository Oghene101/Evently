using Evently.Common.Infrastructure.Configuration;

namespace Evently.Api.Extensions;

internal static class KeyCloakHealthChecksBuilderExtensions
{
    private const string KeyCloakHealthCheck = "KeyCloak";
    private const string KeyCloakHealthUrl = "KeyCloak:HealthUrl";

    extension(IHealthChecksBuilder builder)
    {
        internal IHealthChecksBuilder AddKeyCloak(Uri healthUri)
        {
            builder.AddUrlGroup(healthUri, HttpMethod.Get, KeyCloakHealthCheck);

            return builder;
        }
    }

    extension(IConfiguration configuration)
    {
        internal Uri GetKeyCloakHealthUrl()
        {
            return new Uri(configuration.GetValueOrThrow<string>(KeyCloakHealthUrl));
        }
    }
}
