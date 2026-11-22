using Gedcom.Vector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom;

public class GedcomServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGedcomImportRegistersAdapterAsSingleton()
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
    public void AddGedcomImportBindsMaxFileSizeBytesFromConfiguration()
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
    public void AddGedcomImportZeroOrNegativeMaxFileSizeBytesFailsValidationOnStart()
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

    [Fact]
    public void AddGedcomImportNoSectionKeepsDefaults()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGedcomImport(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GedcomImportOptions>>().Value;

        Assert.Equal(100L * 1024 * 1024, options.MaxFileSizeBytes);
    }

    [Fact]
    public void AddGedcomImportNonNumericValueThrowsOnAccess()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GedcomImport:MaxFileSizeBytes"] = "abc" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGedcomImport(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GedcomImportOptions>>();

        Assert.Throws<InvalidOperationException>(() => options.Value);
    }

    [Fact]
    public void AddGedcomImportActionSetsOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGedcomImport(o => o.MaxFileSizeBytes = 42);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GedcomImportOptions>>().Value;
        var adapter1 = provider.GetRequiredService<IGedcomImportAdapter>();
        var adapter2 = provider.GetRequiredService<IGedcomImportAdapter>();

        Assert.Equal(42, options.MaxFileSizeBytes);
        Assert.Same(adapter1, adapter2);
    }

    [Fact]
    public void AddGedcomImportActionZeroSizeFailsValidation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGedcomImport(o => o.MaxFileSizeBytes = 0);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GedcomImportOptions>>().Value);
    }
}

