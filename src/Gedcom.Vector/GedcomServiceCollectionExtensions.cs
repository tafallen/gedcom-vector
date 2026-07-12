using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gedcom.Vector;

public static class GedcomServiceCollectionExtensions
{
    public static IServiceCollection AddGedcomImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GedcomImportOptions>()
            .Bind(configuration.GetSection(GedcomImportOptions.SectionName))
            .Validate(o => o.MaxFileSizeBytes > 0, "GedcomImport:MaxFileSizeBytes must be a positive number of bytes.")
            .ValidateOnStart();

        services.AddSingleton<IGedcomImportAdapter, GedcomImportAdapter>();
        return services;
    }
}
