using Gedcom.Vector.Diagnostics;
using Gedcom.Vector.Model;

namespace Gedcom.Vector.Parsing;

/// <summary>What became of one input line.</summary>
internal enum LineDispositionKind
{
    /// <summary>The line became its own structure.</summary>
    Structure,

    /// <summary>The line's text was appended to the value of an existing structure.</summary>
    Folded,

    /// <summary>The line carried no data and is accounted for by a diagnostic.</summary>
    Diagnostic,
}

/// <summary>The disposition of one input line: the structure it became or fed, or the diagnostic that accounts for it.</summary>
internal readonly record struct LineDisposition(LineDispositionKind Kind, GedcomStructure? Structure, GedcomDiagnostic? Diagnostic);

/// <summary>Records exactly one disposition per input line. Grows as lines are recorded; the final count need not be known.</summary>
internal sealed class LineAccounting
{
    private LineDisposition?[] _dispositions = new LineDisposition?[256];

    /// <summary>Gets the highest line number recorded.</summary>
    public int LineCount { get; private set; }

    /// <summary>Gets the disposition of a 1-based line, or null when the line has none.</summary>
    public LineDisposition? this[int lineNumber] =>
        lineNumber >= 1 && lineNumber < _dispositions.Length ? _dispositions[lineNumber] : null;

    /// <summary>Records the disposition of a 1-based line.</summary>
    /// <exception cref="InvalidOperationException">The line already has a disposition.</exception>
    public void Record(int lineNumber, LineDisposition disposition)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lineNumber, 1);
        if (lineNumber >= _dispositions.Length)
        {
            var size = Math.Min(Math.Max((long)_dispositions.Length * 2, (long)lineNumber + 1), Array.MaxLength);
            Array.Resize(ref _dispositions, (int)size);
        }

        if (_dispositions[lineNumber] is not null)
        {
            throw new InvalidOperationException("Line " + lineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + " already has a disposition.");
        }

        _dispositions[lineNumber] = disposition;
        if (lineNumber > LineCount)
        {
            LineCount = lineNumber;
        }
    }
}
