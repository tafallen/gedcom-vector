using Xunit;

namespace Gedcom.Vector.Tests.Corpus;

/// <summary>
/// Locates GEDCOM files for corpus tests. The private corpus is a directory named by the
/// <c>GEDCOM_VECTOR_PRIVATE_CORPUS</c> environment variable; its files are never copied or printed, and
/// failure messages built from them name only file names, line numbers, xrefs, tag paths and disposition kinds.
/// </summary>
internal static class PrivateCorpus
{
    /// <summary>The environment variable that names the private corpus directory.</summary>
    internal const string VariableName = "GEDCOM_VECTOR_PRIVATE_CORPUS";

    /// <summary>Returns the <c>.ged</c> files of the private corpus, or skips the test when it is not configured.</summary>
    internal static IReadOnlyList<string> Files()
    {
        var directory = Environment.GetEnvironmentVariable(VariableName);
        Skip.If(string.IsNullOrWhiteSpace(directory), VariableName + " is not set.");
        Skip.IfNot(Directory.Exists(directory), VariableName + " does not name an existing directory.");
        var files = Directory.GetFiles(directory!, "*.ged");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        Skip.If(files.Length == 0, VariableName + " names a directory with no .ged files.");
        return files;
    }

    /// <summary>Returns the <c>.ged</c> files of the committed smoke corpus.</summary>
    internal static IReadOnlyList<string> SmokeFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gedcom.Vector.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var files = Directory.GetFiles(Path.Combine(dir.FullName, "tests", "corpus", "smoke"), "*.ged");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        return files;
    }
}
