using Serilog;

namespace Evently.Api.Infrastructure.Observability;

internal static class OtlpLogExport
{
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    extension(LoggerConfiguration loggerConfiguration)
    {
        // The sink reads the OTEL_* settings from the process environment only, and falls back to localhost when they
        // are missing, so it is handed the app's configuration and left out when no collector is named.
        public LoggerConfiguration WriteToOtlpCollector(
            IConfiguration configuration,
            string serviceName)
        {
            string? otlpEndpoint = configuration[OtlpEndpointKey];

            if (string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                return loggerConfiguration;
            }

            return loggerConfiguration.WriteTo.OpenTelemetry(
                sink => sink.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = serviceName },
                otlpSetting => configuration[otlpSetting]);
        }
    }
}
