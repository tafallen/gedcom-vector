# Smoke corpus

These files are hand-authored for this repository. All people, places and sources are fictional, and no third-party data is included. They are small on purpose: they exist to drive the AOT-versus-JIT comparison in `tools/Compare-AotJit.ps1`, not to test parsing in depth.

Every file has a header, three to five individuals, one family with a marriage, one `OBJE` record, one `SOUR` record and one `_CUSTOM` level-0 record.

| File | What it exercises |
| --- | --- |
| `ansel-551.ged` | GEDCOM 5.5.1, `CHAR ANSEL`. ANSEL combining diacritics (`E2` acute, `E8` umlaut) placed before the base letter, and `A2` (O-slash). LF line endings. |
| `ansi-551.ged` | GEDCOM 5.5.1, `CHAR ANSI`. Windows-1252 bytes `E9`, `FC` and `80` (euro sign) in names and places. CRLF line endings. |
| `utf16le-551.ged` | GEDCOM 5.5.1, UTF-16LE with a byte order mark, `CHAR UNICODE`. A non-BMP character (U+1F333) in a `NOTE`. CRLF line endings. |
| `utf8bom-551.ged` | GEDCOM 5.5.1, `CHAR UTF-8` with a UTF-8 byte order mark (`EF BB BF`), non-ASCII name and place. LF line endings. |
| `utf8-70.ged` | GEDCOM 7.0 (`VERS 7.0`), UTF-8 without a byte order mark, one `SCHMA` `TAG` declaration (`_TAG`) and its use. |
| `sample-70.gdz` | GEDZIP archive holding `gedcom.ged` (the same content as `utf8-70.ged`) and a tiny placeholder `media/photo.jpg`. Entry timestamps are fixed at 1980-01-01 so the archive is reproducible. |

The files are marked `binary` in `.gitattributes` so line endings and encodings are never normalised by git.