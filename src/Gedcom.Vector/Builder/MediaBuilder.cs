using System;
using System.Collections.Generic;

namespace Gedcom.Vector.Builder;

/// <summary>
/// Fluent builder for constructing a <see cref="MediaReferenceRecord"/>.
/// </summary>
public class MediaBuilder
{
    private readonly GedcomBuilder _root;
    private readonly string _xrefId;
    private string? _title;
    private string? _filePath;
    private string? _format;
    private readonly List<string> _linkedXrefs = new();

    internal MediaBuilder(GedcomBuilder root, string xrefId, string? title, string? filePath, string? format)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _xrefId = xrefId ?? throw new ArgumentNullException(nameof(xrefId));
        _title = title;
        _filePath = filePath;
        _format = format;
    }

    /// <summary>
    /// Configures the file path or URI of this media record.
    /// </summary>
    public MediaBuilder WithFilePath(string filePath)
    {
        _filePath = filePath;
        return this;
    }

    /// <summary>
    /// Configures the title of this media record.
    /// </summary>
    public MediaBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Configures the format (extension) of this media record.
    /// </summary>
    public MediaBuilder WithFormat(string format)
    {
        _format = format;
        return this;
    }

    /// <summary>
    /// Links this media record to a person or family identifier.
    /// </summary>
    public MediaBuilder LinkTo(string entityXref)
    {
        if (entityXref is not null)
        {
            _linkedXrefs.Add(entityXref);
        }
        return this;
    }

    private MediaReferenceRecord GetRecord()
    {
        // Automatically map MIME type based on format (similar to MediaMapper)
        string? mimeType = _format?.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            "pdf" => "application/pdf",
            _ => _format
        };

        return new MediaReferenceRecord(_xrefId, _title, _filePath, _format, mimeType, _linkedXrefs);
    }

    /// <summary>
    /// Adds a person to the GEDCOM builder.
    /// </summary>
    public PersonBuilder AddPerson(string xrefId, string? firstName = null, string? lastName = null, PersonSex sex = PersonSex.Unknown)
    {
        _root.AddMediaRecord(GetRecord());
        return _root.AddPerson(xrefId, firstName, lastName, sex);
    }

    /// <summary>
    /// Adds a family to the GEDCOM builder.
    /// </summary>
    public FamilyBuilder AddFamily(string xrefId, string? husbandXref = null, string? wifeXref = null)
    {
        _root.AddMediaRecord(GetRecord());
        return _root.AddFamily(xrefId, husbandXref, wifeXref);
    }

    /// <summary>
    /// Adds another media reference to the GEDCOM builder.
    /// </summary>
    public MediaBuilder AddMedia(string xrefId, string? title = null, string? filePath = null, string? format = null)
    {
        _root.AddMediaRecord(GetRecord());
        return _root.AddMedia(xrefId, title, filePath, format);
    }

    /// <summary>
    /// Builds the final <see cref="GedcomParseResult"/>.
    /// </summary>
    public GedcomParseResult Build()
    {
        _root.AddMediaRecord(GetRecord());
        return _root.Build();
    }
}
