using Xunit;

namespace Gedcom.Vector.Tests.Corpus;

/// <summary>
/// Locates GEDCOM files for corpus tests. The private corpus is an explicit list of <c>.ged</c> files named by the
/// <c>GEDCOM_VECTOR_PRIVATE_CORPUS</c> environment variable, separated by <see cref="Path.PathSeparator"/>; nothing
/// else is ever read. Its files are never copied or printed, and failure messages built from them identify a file by
/// its index only (<c>file #1</c>) and otherwise name only line numbers, xrefs, tag paths and disposition kinds.
/// </summary>
internal static class PrivateCorpus
{
    /// <summary>The environment variable that lists the private corpus files.</summary>
    internal const string VariableName = "GEDCOM_VECTOR_PRIVATE_CORPUS";

    /// <summary>
    /// Returns the private corpus files in the order listed. Skips the test when the variable is unset or empty, and
    /// fails it when any entry is not an existing <c>.ged</c> file.
    /// </summary>
    internal static IReadOnlyList<string> Files()
    {
        var value = Environment.GetEnvironmentVariable(VariableName);
        Skip.If(string.IsNullOrWhiteSpace(value), VariableName + " is not set.");
        if (Select(value!, out var files) is { } error)
        {
            Assert.Fail(error);
        }

        return files;
    }

    /// <summary>
    /// Splits <paramref name="value"/> into file paths. Returns null when every entry is an existing file with a
    /// <c>.ged</c> extension (any case), otherwise a message naming the first bad entry by its 1-based index only.
    /// </summary>
    internal static string? Select(string value, out List<string> files)
    {
        files = [];
        var entries = value.Split(Path.PathSeparator);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i].Trim();
            if (!string.Equals(Path.GetExtension(entry), ".ged", StringComparison.OrdinalIgnoreCase) || !File.Exists(entry))
            {
                files.Clear();
                return VariableName + " entry #" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " is not an existing .ged file; the variable lists files separated by the path separator.";
            }

            files.Add(entry);
        }

        return null;
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
