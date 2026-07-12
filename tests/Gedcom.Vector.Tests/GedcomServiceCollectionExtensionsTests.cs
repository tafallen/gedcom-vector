using Gedcom.Vector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom;

public class GedcomServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGedcomImport_RegistersAdapterAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGedcomImport(new ConfigurationBuilder().Build());
        var provider = services.BuildServiceProvider();

        var adapter1 = provider.GetRequiredService<IGedcomImportAdapter>();
        var adapter2 = provider.GetRequiredService<IGedcomImportAdapter>();

        Assert.IsType<GedcomImportAdapter>(adapter1);
        Assert.Same(adapter1, adapter2);
    }

    [Fact]
    public void AddGedcomImport_BindsMaxFileSizeBytesFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GedcomImport:MaxFileSizeBytes"] = "12345" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddGedcomImport(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GedcomImportOptions>>().Value;

        Assert.Equal(12345, options.MaxFileSizeBytes);
    }

    [Fact]
    public void AddGedcomImport_ZeroOrNegativeMaxFileSizeBytes_FailsValidationOnStart()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GedcomImport:MaxFileSizeBytes"] = "0" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddGedcomImport(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GedcomImportOptions>>().Value);
    }
}

