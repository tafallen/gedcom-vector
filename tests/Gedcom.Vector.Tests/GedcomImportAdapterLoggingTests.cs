using Gedcom.Vector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom;

public class GedcomImportAdapterLoggingTests
{
    [Fact]
    public void ParseSuccessfulParseLogsSummaryAtInformationLevel()
    {
        var logger = new CapturingLogger<GedcomImportAdapter>();
        var adapter = new GedcomImportAdapter(logger, Options.Create(new GedcomImportOptions()));

        adapter.Parse(GedcomTestData.ToStream("0 @I1@ INDI\n1 NAME John /Doe/\n"));

        Assert.Single(logger.Levels, LogLevel.Information);
    }

    [Fact]
    public void ParseEmptyInputDoesNotLogSummary()
    {
        var logger = new CapturingLogger<GedcomImportAdapter>();
        var adapter = new GedcomImportAdapter(logger, Options.Create(new GedcomImportOptions()));

        var result = adapter.Parse(GedcomTestData.RawToStream("0 HEAD\n0 TRLR\n"));

        Assert.NotEmpty(result.Errors);
        Assert.Empty(logger.Levels);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Levels.Add(logLevel);
    }
}
