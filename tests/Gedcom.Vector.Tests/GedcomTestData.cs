using System.Text;

namespace Gedcom.Vector.Tests;

internal static class GedcomTestData
{
    private const string Header = "0 HEAD\n1 GEDC\n2 VERS 5.5.1\n2 FORM LINEAGE-LINKED\n1 CHAR UTF-8\n";
    private const string Trailer = "0 TRLR\n";

    public static Stream ToStream(string gedcomBody)
    {
        var normalizedBody = gedcomBody.EndsWith('\n') ? gedcomBody : gedcomBody + "\n";
        var bytes = Encoding.UTF8.GetBytes(Header + normalizedBody + Trailer);
        return new MemoryStream(bytes);
    }

    public static Stream RawToStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));
}

