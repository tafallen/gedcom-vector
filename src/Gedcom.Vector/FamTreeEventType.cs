namespace Gedcom.Vector;

/// <summary>
/// Specifies the type of individual events supported by the parser.
/// </summary>
public enum FamTreeEventType
{
    /// <summary>
    /// Birth event (BIRT).
    /// </summary>
    Birth,

    /// <summary>
    /// Death event (DEAT).
    /// </summary>
    Death,

    /// <summary>
    /// Census event (CENS).
    /// </summary>
    Census,

    /// <summary>
    /// Immigration event (IMMI).
    /// </summary>
    Immigration,

    /// <summary>
    /// Emigration event (EMIG).
    /// </summary>
    Emigration,

    /// <summary>
    /// Residence event (RESI).
    /// </summary>
    Residence,

    /// <summary>
    /// Christening event (CHR).
    /// </summary>
    Christening,

    /// <summary>
    /// Burial event (BURI).
    /// </summary>
    Burial,

    /// <summary>
    /// Baptism event (BAPM).
    /// </summary>
    Baptism,
}
