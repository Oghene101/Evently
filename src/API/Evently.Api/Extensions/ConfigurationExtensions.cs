using Asp.Versioning;
using Evently.Api.OpenApi;

namespace Evently.Api.Extensions;

internal static class ConfigurationExtensions
{
    extension(IConfigurationBuilder configurationBuilder)
    {
        internal void AddModuleConfiguration(string[] modules)
        {
            foreach (string module in modules)
            {
                configurationBuilder.AddJsonFile($"modules.{module}.json", false, true);
                configurationBuilder.AddJsonFile($"modules.{module}.Development.json", true, true);
            }
        }
    }

    extension(IServiceCollection services)
    {
        internal IServiceCollection AddOpenApiInternal()
        {
            services.AddOpenApi("v1", options =>
            {
                options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();

                options.CreateSchemaReferenceId = jsonTypeInfo =>
                {
                    string fullName = jsonTypeInfo.Type.FullName?.Replace("+", ".") ?? jsonTypeInfo.Type.Name;

                    string[] parts = fullName.Split('.');

                    if (parts.Length < 2)
                    {
                        return parts[^1];
                    }

                    string className = parts[^1];
                    string folderName = parts[^2];

                    return $"{folderName}.{className}";
                };
            });

            return services;
        }

        internal IServiceCollection AddApiVersioningInternal()
        {
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            }).AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            });

            return services;
        }
    }
}
