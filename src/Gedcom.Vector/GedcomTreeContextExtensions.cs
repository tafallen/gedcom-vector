using System;

namespace Gedcom.Vector;

/// <summary>
/// Exposes extension methods on <see cref="GedcomParseResult"/> for relationship context conversion.
/// </summary>
public static class GedcomTreeContextExtensions
{
    /// <summary>
    /// Wraps a <see cref="GedcomParseResult"/> in a query-optimized <see cref="GedcomTreeContext"/>.
    /// </summary>
    /// <param name="result">The parsed GEDCOM result structure.</param>
    /// <returns>A query-optimized <see cref="GedcomTreeContext"/>.</returns>
    public static GedcomTreeContext ToContext(this GedcomParseResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        return new GedcomTreeContext(result);
    }
}
