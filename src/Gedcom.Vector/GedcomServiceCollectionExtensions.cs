using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = services.AddOptions<GedcomImportOptions>()
            .Bind(configuration.GetSection(GedcomImportOptions.SectionName));
        return Register(services, builder);
    }

    /// <summary>
    /// Registers <see cref="IGedcomImportAdapter"/> and <see cref="IGedcomExportWriter"/> with the service collection,
    /// configuring <see cref="GedcomImportOptions"/> in code.
    /// </summary>
    /// <param name="services">The service collection to register the services in.</param>
    /// <param name="configure">The delegate that configures the import options.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGedcomImport(this IServiceCollection services, Action<GedcomImportOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = services.AddOptions<GedcomImportOptions>()
            .Configure(configure);
        return Register(services, builder);
    }

    private static IServiceCollection Register(IServiceCollection services, OptionsBuilder<GedcomImportOptions> builder)
    {
        builder
            .Validate(o => o.MaxFileSizeBytes > 0, "GedcomImport:MaxFileSizeBytes must be a positive number of bytes.")
            .ValidateOnStart();

        services.AddSingleton<IGedcomImportAdapter, GedcomImportAdapter>();
        services.AddSingleton<IGedcomExportWriter, GedcomExportWriter>();
        return services;
    }
}
