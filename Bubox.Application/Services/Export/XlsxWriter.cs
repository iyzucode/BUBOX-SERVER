using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Bubox.Application.Services.Export;

public enum XlsxType
{
    Text,
    Number,
    Date,
    DateTime
}

public sealed record XlsxColumn(string Header, XlsxType Type = XlsxType.Text, double Width = 16, bool Sum = false);

/// <summary>Satu lembar kerja: header + baris data. Kolom ber-<see cref="XlsxColumn.Sum"/> mendapat baris total (rumus SUM).</summary>
public sealed class XlsxSheet(string name, IReadOnlyList<XlsxColumn> columns)
{
    public string Name { get; } = name;
    public IReadOnlyList<XlsxColumn> Columns { get; } = columns;
    public List<object?[]> Rows { get; } = [];

    public void AddRow(params object?[] values)
    {
        if (values.Length != Columns.Count)
        {
            throw new ArgumentException($"Jumlah nilai ({values.Length}) tidak sama dengan jumlah kolom ({Columns.Count}).");
        }
        Rows.Add(values);
    }
}

/// <summary>
/// Penulis .xlsx minimal (OOXML SpreadsheetML) tanpa dependensi: teks inline, angka, tanggal, rumus SUM,
/// header tebal, baris header dibekukan, dan AutoFilter. Cukup untuk ekspor laporan dari layar admin.
/// Teks selalu ditulis sebagai inline string (bukan rumus), sehingga isian seperti "=..." aman.
/// </summary>
public static class XlsxWriter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Indeks style di styles.xml (cellXfs)
    private const int StyleHeader = 1;
    private const int StyleDate = 2;
    private const int StyleDateTime = 3;
    private const int StyleNumber = 4;
    private const int StyleTotalLabel = 5;
    private const int StyleTotalNumber = 6;

    public static byte[] Build(XlsxSheet sheet)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", ContentTypesXml);
            Add(zip, "_rels/.rels", RootRelsXml);
            Add(zip, "xl/workbook.xml", WorkbookXml(sheet.Name));
            Add(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            Add(zip, "xl/styles.xml", StylesXml);
            Add(zip, "xl/worksheets/sheet1.xml", SheetXml(sheet));
        }
        return stream.ToArray();
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string SheetXml(XlsxSheet sheet)
    {
        var sb = new StringBuilder();
        var lastCol = ColumnName(sheet.Columns.Count - 1);
        var dataLastRow = sheet.Rows.Count + 1;

        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        sb.Append("""<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        sb.Append("<cols>");
        for (var i = 0; i < sheet.Columns.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"""<col min="{i + 1}" max="{i + 1}" width="{sheet.Columns[i].Width.ToString(CultureInfo.InvariantCulture)}" customWidth="1"/>""");
        }
        sb.Append("</cols><sheetData>");

        // Header
        sb.Append("""<row r="1">""");
        for (var c = 0; c < sheet.Columns.Count; c++)
        {
            AppendText(sb, $"{ColumnName(c)}1", sheet.Columns[c].Header, StyleHeader);
        }
        sb.Append("</row>");

        // Data
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var rowNumber = r + 2;
            sb.Append(CultureInfo.InvariantCulture, $"""<row r="{rowNumber}">""");
            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                AppendValue(sb, $"{ColumnName(c)}{rowNumber}", sheet.Columns[c].Type, sheet.Rows[r][c]);
            }
            sb.Append("</row>");
        }

        // Total
        if (sheet.Rows.Count > 0 && sheet.Columns.Any(c => c.Sum))
        {
            var totalRow = dataLastRow + 1;
            sb.Append(CultureInfo.InvariantCulture, $"""<row r="{totalRow}">""");
            AppendText(sb, $"A{totalRow}", "TOTAL", StyleTotalLabel);
            for (var c = 1; c < sheet.Columns.Count; c++)
            {
                if (!sheet.Columns[c].Sum)
                {
                    continue;
                }
                var col = ColumnName(c);
                sb.Append(CultureInfo.InvariantCulture,
                    $"""<c r="{col}{totalRow}" s="{StyleTotalNumber}"><f>SUM({col}2:{col}{dataLastRow})</f></c>""");
            }
            sb.Append("</row>");
        }

        sb.Append("</sheetData>");
        sb.Append(CultureInfo.InvariantCulture, $"""<autoFilter ref="A1:{lastCol}{Math.Max(1, dataLastRow)}"/>""");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static void AppendValue(StringBuilder sb, string cellRef, XlsxType type, object? value)
    {
        if (value == null || (value is string s && s.Length == 0))
        {
            return;
        }

        switch (type)
        {
            case XlsxType.Number when TryNumber(value, out var number):
                sb.Append(CultureInfo.InvariantCulture, $"""<c r="{cellRef}" s="{StyleNumber}"><v>{number.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                return;
            case XlsxType.Date when value is DateOnly date:
                sb.Append(CultureInfo.InvariantCulture, $"""<c r="{cellRef}" s="{StyleDate}"><v>{ToSerial(date.ToDateTime(TimeOnly.MinValue))}</v></c>""");
                return;
            case XlsxType.DateTime when value is DateTime dateTime:
                sb.Append(CultureInfo.InvariantCulture, $"""<c r="{cellRef}" s="{StyleDateTime}"><v>{ToSerial(dateTime)}</v></c>""");
                return;
            default:
                AppendText(sb, cellRef, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, 0);
                return;
        }
    }

    private static bool TryNumber(object value, out decimal number)
    {
        switch (value)
        {
            case decimal d: number = d; return true;
            case int i: number = i; return true;
            case long l: number = l; return true;
            case double db: number = (decimal)db; return true;
            default: number = 0; return false;
        }
    }

    private static void AppendText(StringBuilder sb, string cellRef, string text, int style)
    {
        var styleAttr = style == 0 ? string.Empty : $" s=\"{style}\"";
        sb.Append($"""<c r="{cellRef}" t="inlineStr"{styleAttr}><is><t xml:space="preserve">{Escape(text)}</t></is></c>""");
    }

    /// <summary>Nomor seri tanggal Excel (sistem 1900; benar untuk tanggal setelah Maret 1900).</summary>
    private static string ToSerial(DateTime value) =>
        (value - new DateTime(1899, 12, 30)).TotalDays.ToString("0.########", CultureInfo.InvariantCulture);

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        index++;
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            name = (char)('A' + rem) + name;
            index = (index - 1) / 26;
        }
        return name;
    }

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                // Karakter kontrol tidak sah di XML 1.0 (kecuali tab & baris baru) dibuang
                case < ' ' when ch is not ('\t' or '\n' or '\r'): break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    private static string WorkbookXml(string sheetName)
    {
        // Nama sheet maks 31 karakter dan tanpa []:*?/\
        var clean = new string(sheetName.Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray());
        clean = clean.Length > 31 ? clean[..31] : clean;
        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Escape(clean)}" sheetId="1" r:id="rId1"/></sheets></workbook>""";
    }

    private const string ContentTypesXml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""";

    private const string RootRelsXml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private const string WorkbookRelsXml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""";

    // cellXfs: 0 normal · 1 header · 2 tanggal · 3 tanggal+jam · 4 angka ribuan · 5 label total · 6 angka total
    private const string StylesXml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="2"><numFmt numFmtId="164" formatCode="dd/mm/yyyy"/><numFmt numFmtId="165" formatCode="dd/mm/yyyy hh:mm"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Arial"/><family val="2"/></font><font><b/><sz val="11"/><name val="Arial"/><family val="2"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFF3E8DC"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="7"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="3" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";
}
