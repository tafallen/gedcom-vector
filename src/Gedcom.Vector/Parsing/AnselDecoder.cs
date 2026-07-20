using System.Text;

namespace Gedcom.Vector.Parsing;

internal static class AnselDecoder
{
    private static readonly char[] CombiningMarksArray = new char[256];
    private static readonly char[] SpacingCharactersArray = new char[256];

    static AnselDecoder()
    {
        // ANSEL non-spacing (combining) diacritics. In ANSEL these bytes appear
        // BEFORE the base character they modify, the reverse of Unicode combining
        // mark order.
        CombiningMarksArray[0xE1] = '̀'; // grave accent
        CombiningMarksArray[0xE2] = '́'; // acute accent
        CombiningMarksArray[0xE3] = '̂'; // circumflex accent
        CombiningMarksArray[0xE4] = '̃'; // tilde
        CombiningMarksArray[0xE5] = '̄'; // macron
        CombiningMarksArray[0xE6] = '̆'; // breve
        CombiningMarksArray[0xE7] = '̇'; // dot above
        CombiningMarksArray[0xE8] = '̈'; // umlaut (diaeresis)
        CombiningMarksArray[0xE9] = '̌'; // hacek
        CombiningMarksArray[0xEA] = '̊'; // circle above (angstrom)
        CombiningMarksArray[0xED] = '̕'; // high comma, off center
        CombiningMarksArray[0xEE] = '̋'; // double acute accent
        CombiningMarksArray[0xF0] = '̧'; // cedilla
        CombiningMarksArray[0xF1] = '̨'; // ogonek
        CombiningMarksArray[0xF6] = '̲'; // underscore
        CombiningMarksArray[0xFE] = '̓'; // high comma, centered

        // ANSEL spacing characters with no Unicode precomposed-with-diacritic
        // ambiguity - decode directly to their single Unicode codepoint.
        SpacingCharactersArray[0xA1] = 'Ł'; // L with stroke (uppercase)
        SpacingCharactersArray[0xA2] = 'Ø'; // O with stroke (uppercase)
        SpacingCharactersArray[0xA3] = 'Đ'; // D with stroke (uppercase)
        SpacingCharactersArray[0xA4] = 'Þ'; // thorn (uppercase)
        SpacingCharactersArray[0xA5] = 'Æ'; // ligature AE (uppercase)
        SpacingCharactersArray[0xA6] = 'Œ'; // ligature OE (uppercase)
        SpacingCharactersArray[0xA8] = '·'; // middle dot
        SpacingCharactersArray[0xA9] = '♭'; // musical flat
        SpacingCharactersArray[0xAA] = '®'; // registered trademark
        SpacingCharactersArray[0xAB] = '±'; // plus or minus
        SpacingCharactersArray[0xAE] = 'ʼ'; // alif
        SpacingCharactersArray[0xB0] = 'ʻ'; // ayn
        SpacingCharactersArray[0xB1] = 'ł'; // l with stroke (lowercase)
        SpacingCharactersArray[0xB2] = 'ø'; // o with stroke (lowercase)
        SpacingCharactersArray[0xB3] = 'đ'; // d with stroke (lowercase)
        SpacingCharactersArray[0xB4] = 'þ'; // thorn (lowercase)
        SpacingCharactersArray[0xB5] = 'æ'; // ligature ae (lowercase)
        SpacingCharactersArray[0xB6] = 'œ'; // ligature oe (lowercase)
        SpacingCharactersArray[0xB8] = 'ı'; // dotless i (lowercase)
        SpacingCharactersArray[0xB9] = '£'; // British pound
        SpacingCharactersArray[0xBA] = 'ð'; // eth
        SpacingCharactersArray[0xC3] = '©'; // copyright mark
        SpacingCharactersArray[0xC5] = '¿'; // inverted question mark
        SpacingCharactersArray[0xC6] = '¡'; // inverted exclamation mark
        SpacingCharactersArray[0xCF] = 'ß'; // ess-zed (sharp s)
    }

    private struct PendingMarks
    {
        private char _m0, _m1, _m2, _m3;
        private List<char>? _overflow;
        private int _count;

        public int Count => _count;

        public void Add(char c)
        {
            if (_count < 4)
            {
                switch (_count)
                {
                    case 0: _m0 = c; break;
                    case 1: _m1 = c; break;
                    case 2: _m2 = c; break;
                    case 3: _m3 = c; break;
                }
            }
            else
            {
                _overflow ??= new List<char>();
                _overflow.Add(c);
            }
            _count++;
        }

        public void AppendTo(StringBuilder builder)
        {
            if (_count > 0) builder.Append(_m0);
            if (_count > 1) builder.Append(_m1);
            if (_count > 2) builder.Append(_m2);
            if (_count > 3) builder.Append(_m3);
            if (_overflow != null)
            {
                for (int i = 0; i < _overflow.Count; i++)
                {
                    builder.Append(_overflow[i]);
                }
                _overflow.Clear();
            }
            _count = 0;
        }
    }

    public static string Decode(string latin1String)
    {
        var builder = new StringBuilder(latin1String.Length);
        var pendingMarks = new PendingMarks();

        for (int i = 0; i < latin1String.Length; i++)
        {
            var c = latin1String[i];
            var b = (byte)c;
            var mark = CombiningMarksArray[b];
            if (mark != '\0')
            {
                pendingMarks.Add(mark);
                continue;
            }

            builder.Append(DecodeBaseByte(b));
            if (pendingMarks.Count > 0)
            {
                pendingMarks.AppendTo(builder);
            }
        }

        if (pendingMarks.Count > 0)
        {
            pendingMarks.AppendTo(builder);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string Decode(ReadOnlySpan<char> latin1Span)
    {
        var builder = new StringBuilder(latin1Span.Length);
        var pendingMarks = new PendingMarks();

        for (int i = 0; i < latin1Span.Length; i++)
        {
            var c = latin1Span[i];
            var b = (byte)c;
            var mark = CombiningMarksArray[b];
            if (mark != '\0')
            {
                pendingMarks.Add(mark);
                continue;
            }

            builder.Append(DecodeBaseByte(b));
            if (pendingMarks.Count > 0)
            {
                pendingMarks.AppendTo(builder);
            }
        }

        if (pendingMarks.Count > 0)
        {
            pendingMarks.AppendTo(builder);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string Decode(byte[] anselBytes)
    {
        var builder = new StringBuilder(anselBytes.Length);
        var pendingMarks = new PendingMarks();

        for (int i = 0; i < anselBytes.Length; i++)
        {
            var b = anselBytes[i];
            var mark = CombiningMarksArray[b];
            if (mark != '\0')
            {
                pendingMarks.Add(mark);
                continue;
            }

            builder.Append(DecodeBaseByte(b));
            if (pendingMarks.Count > 0)
            {
                pendingMarks.AppendTo(builder);
            }
        }

        // Malformed input: trailing combining marks with no base character to
        // attach to. Append rather than drop, so no information is silently lost.
        if (pendingMarks.Count > 0)
        {
            pendingMarks.AppendTo(builder);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static char DecodeBaseByte(byte b)
    {
        if (b < 0x80)
        {
            return (char)b;
        }

        var ch = SpacingCharactersArray[b];
        return ch != '\0' ? ch : '\uFFFD';
    }
}
