using System.Text;

namespace Gedcom.Vector.Parsing;

internal static class AnselDecoder
{
    // ANSEL non-spacing (combining) diacritics. In ANSEL these bytes appear
    // BEFORE the base character they modify, the reverse of Unicode combining
    // mark order. Hex codes verified against a corrected ANSEL table (the
    // original GEDCOM 5.5.1 spec's own Appendix C graphic column is known to
    // contain transcription errors).
    private static readonly Dictionary<byte, char> CombiningMarks = new()
    {
        [0xE1] = '̀', // grave accent
        [0xE2] = '́', // acute accent
        [0xE3] = '̂', // circumflex accent
        [0xE4] = '̃', // tilde
        [0xE5] = '̄', // macron
        [0xE6] = '̆', // breve
        [0xE7] = '̇', // dot above
        [0xE8] = '̈', // umlaut (diaeresis)
        [0xE9] = '̌', // hacek
        [0xEA] = '̊', // circle above (angstrom)
        [0xED] = '̕', // high comma, off center
        [0xEE] = '̋', // double acute accent
        [0xF0] = '̧', // cedilla
        [0xF1] = '̨', // ogonek
        [0xF6] = '̲', // underscore
        [0xFE] = '̓', // high comma, centered
    };

    // ANSEL spacing characters with no Unicode precomposed-with-diacritic
    // ambiguity - decode directly to their single Unicode codepoint.
    private static readonly Dictionary<byte, char> SpacingCharacters = new()
    {
        [0xA1] = 'Ł', // L with stroke (uppercase)
        [0xA2] = 'Ø', // O with stroke (uppercase)
        [0xA3] = 'Đ', // D with stroke (uppercase)
        [0xA4] = 'Þ', // thorn (uppercase)
        [0xA5] = 'Æ', // ligature AE (uppercase)
        [0xA6] = 'Œ', // ligature OE (uppercase)
        [0xA8] = '·', // middle dot
        [0xA9] = '♭', // musical flat
        [0xAA] = '®', // registered trademark
        [0xAB] = '±', // plus or minus
        [0xAE] = 'ʼ', // alif
        [0xB0] = 'ʻ', // ayn
        [0xB1] = 'ł', // l with stroke (lowercase)
        [0xB2] = 'ø', // o with stroke (lowercase)
        [0xB3] = 'đ', // d with stroke (lowercase)
        [0xB4] = 'þ', // thorn (lowercase)
        [0xB5] = 'æ', // ligature ae (lowercase)
        [0xB6] = 'œ', // ligature oe (lowercase)
        [0xB8] = 'ı', // dotless i (lowercase)
        [0xB9] = '£', // British pound
        [0xBA] = 'ð', // eth
        [0xC3] = '©', // copyright mark
        [0xC5] = '¿', // inverted question mark
        [0xC6] = '¡', // inverted exclamation mark
        [0xCF] = 'ß', // ess-zed (sharp s)
    };

    public static string Decode(byte[] anselBytes)
    {
        var builder = new StringBuilder(anselBytes.Length);
        var pendingMarks = new List<char>();

        foreach (var b in anselBytes)
        {
            if (CombiningMarks.TryGetValue(b, out var mark))
            {
                pendingMarks.Add(mark);
                continue;
            }

            builder.Append(DecodeBaseByte(b));
            AppendPendingMarks(builder, pendingMarks);
        }

        // Malformed input: trailing combining marks with no base character to
        // attach to. Append rather than drop, so no information is silently lost.
        AppendPendingMarks(builder, pendingMarks);

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static void AppendPendingMarks(StringBuilder builder, List<char> pendingMarks)
    {
        foreach (var mark in pendingMarks)
        {
            builder.Append(mark);
        }

        pendingMarks.Clear();
    }

    private static char DecodeBaseByte(byte b)
    {
        if (b < 0x80)
        {
            return (char)b;
        }

        return SpacingCharacters.TryGetValue(b, out var ch) ? ch : '�';
    }
}
