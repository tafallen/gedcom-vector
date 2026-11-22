namespace Gedcom.Vector.Diagnostics;

/// <summary>How serious a diagnostic is.</summary>
public enum GedcomDiagnosticSeverity
{
    /// <summary>Informational; the input was handled without loss.</summary>
    Info,

    /// <summary>The input was unusual or recoverable; the reader repaired or kept it.</summary>
    Warning,

    /// <summary>The input could not be handled correctly.</summary>
    Error,
}
