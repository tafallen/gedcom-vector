namespace Gedcom.Vector.Diagnostics;

/// <summary>Collects diagnostics up to a cap, then records a single truncation entry.</summary>
internal sealed class DiagnosticBag
{
    private readonly int _max;
    private readonly List<GedcomDiagnostic> _items = [];
    private GedcomDiagnostic? _truncation;

    internal DiagnosticBag(int maxDiagnostics)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDiagnostics, 1);
        _max = maxDiagnostics;
    }

    internal IReadOnlyList<GedcomDiagnostic> Items => _items;

    internal int SuppressedCount { get; private set; }

    internal GedcomDiagnostic Add(
        GedcomDiagnosticCode code,
        int? line = null,
        string? recordXref = null,
        string? tagPath = null,
        string? tag = null)
    {
        if (_truncation is null && _items.Count >= _max)
        {
            var (tSeverity, tTemplate) = DiagnosticMessages.For(GedcomDiagnosticCode.DiagnosticsTruncated);
            _truncation = new GedcomDiagnostic(GedcomDiagnosticCode.DiagnosticsTruncated, tSeverity, tTemplate);
            _items.Add(_truncation);
        }

        if (_truncation is not null)
        {
            SuppressedCount++;
            return _truncation;
        }

        var (severity, template) = DiagnosticMessages.For(code);
        var diagnostic = new GedcomDiagnostic(code, severity, DiagnosticMessages.Format(template, tag), line, recordXref, tagPath);
        _items.Add(diagnostic);
        return diagnostic;
    }
}
