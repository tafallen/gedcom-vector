using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gedcom.Vector;

/// <summary>
/// Extension methods for registering GEDCOM parsing and exporting services with Dependency Injection.
/// </summary>
public static class GedcomServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IGedcomImportAdapter"/> and <see cref="IGedcomExportWriter"/> with the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the services in.</param>
    /// <param name="configuration">The application configuration to bind configuration settings from.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGedcomImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GedcomImportOptions>()
            .Bind(configuration.GetSection(GedcomImportOptions.SectionName))
            .Validate(o => o.MaxFileSizeBytes > 0, "GedcomImport:MaxFileSizeBytes must be a positive number of bytes.")
            .ValidateOnStart();

        services.AddSingleton<IGedcomImportAdapter, GedcomImportAdapter>();
        services.AddSingleton<IGedcomExportWriter, GedcomExportWriter>();
        return services;
    }
}
