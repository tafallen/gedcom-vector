using System.Globalization;

namespace Gedcom.Vector.Diagnostics;

/// <summary>A single observation raised while reading a GEDCOM document. Messages never contain payload values.</summary>
public sealed class GedcomDiagnostic
{
    /// <summary>Initializes a new instance of the <see cref="GedcomDiagnostic"/> class.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="message">The message text; must not be empty.</param>
    /// <param name="line">The 1-based source line, if known.</param>
    /// <param name="recordXref">The xref of the enclosing record, if any.</param>
    /// <param name="tagPath">Dot-separated tags from the record, for example <c>INDI.BIRT.DATE</c>.</param>
    public GedcomDiagnostic(
        GedcomDiagnosticCode code,
        GedcomDiagnosticSeverity severity,
        string message,
        int? line = null,
        string? recordXref = null,
        string? tagPath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        Code = code;
        Severity = severity;
        Message = message;
        Line = line;
        RecordXref = recordXref;
        TagPath = tagPath;
    }

    /// <summary>Gets the diagnostic code.</summary>
    public GedcomDiagnosticCode Code { get; }

    /// <summary>Gets the severity.</summary>
    public GedcomDiagnosticSeverity Severity { get; }

    /// <summary>Gets the message text.</summary>
    public string Message { get; }

    /// <summary>Gets the 1-based source line, if known.</summary>
    public int? Line { get; }

    /// <summary>Gets the xref of the enclosing record, if any.</summary>
    public string? RecordXref { get; }

    /// <summary>Gets the dot-separated tag path from the record, for example <c>INDI.BIRT.DATE</c>.</summary>
    public string? TagPath { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var line = Line is { } l ? string.Create(CultureInfo.InvariantCulture, $" line {l}") : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{Severity} GV{(int)Code}{line}: {Message}");
    }
}
