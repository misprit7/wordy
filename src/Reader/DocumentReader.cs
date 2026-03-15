using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Wordy.Reader;

public static class DocumentReader
{
    public static DocumentIR Read(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        return ReadDocument(doc);
    }

    public static DocumentIR Read(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        return ReadDocument(doc);
    }

    private static DocumentIR ReadDocument(WordprocessingDocument doc)
    {
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("Document has no body");

        var imports = ReadBibliographySources(doc);

        var elements = new List<DocumentElement>();
        var pendingListItems = new List<ListItemElement>();
        int? currentNumId = null;

        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case Paragraph p when IsListParagraph(p):
                    var numId = GetListNumId(p);
                    if (currentNumId is not null && numId != currentNumId)
                    {
                        // Different list — flush previous
                        elements.Add(new ListElement(pendingListItems));
                        pendingListItems = new List<ListItemElement>();
                    }
                    currentNumId = numId;
                    var ilvl = GetListIndentLevel(p);
                    var listRuns = new List<RunElement>();
                    foreach (var run in p.Elements<Run>())
                        listRuns.Add(ReadRun(run));
                    pendingListItems.Add(new ListItemElement(ilvl, listRuns));
                    break;
                case Paragraph p:
                    if (pendingListItems.Count > 0)
                    {
                        elements.Add(new ListElement(pendingListItems));
                        pendingListItems = new List<ListItemElement>();
                        currentNumId = null;
                    }
                    elements.Add(ReadParagraph(p));
                    break;
                case Table t:
                    if (pendingListItems.Count > 0)
                    {
                        elements.Add(new ListElement(pendingListItems));
                        pendingListItems = new List<ListItemElement>();
                        currentNumId = null;
                    }
                    elements.Add(ReadTable(t));
                    break;
                // Skip body-level SDT blocks (e.g. "Works Cited" bibliography section)
                case SdtBlock:
                    break;
            }
        }

        if (pendingListItems.Count > 0)
            elements.Add(new ListElement(pendingListItems));

        return new DocumentIR(elements, imports);
    }

    // --- Bibliography / Imports ---

    private static List<ImportInfo> ReadBibliographySources(WordprocessingDocument doc)
    {
        var imports = new List<ImportInfo>();
        var mainPart = doc.MainDocumentPart;
        if (mainPart is null) return imports;

        foreach (var xmlPart in mainPart.CustomXmlParts)
        {
            using var stream = xmlPart.GetStream();
            try
            {
                var xdoc = XDocument.Load(stream);
                var ns = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/bibliography");
                foreach (var source in xdoc.Descendants(ns + "Source"))
                {
                    var tag = source.Element(ns + "Tag")?.Value;
                    var title = source.Element(ns + "Title")?.Value;
                    if (tag is not null && title is not null)
                        imports.Add(new ImportInfo(tag, title));
                }
            }
            catch
            {
                // Not a bibliography XML part — skip
            }
        }

        return imports;
    }

    // --- List detection ---

    private static bool IsListParagraph(Paragraph p)
    {
        return p.ParagraphProperties?.NumberingProperties?
            .GetFirstChild<NumberingId>()?.Val?.Value is not null;
    }

    private static int GetListNumId(Paragraph p)
    {
        return p.ParagraphProperties!.NumberingProperties!
            .GetFirstChild<NumberingId>()!.Val!.Value;
    }

    private static int GetListIndentLevel(Paragraph p)
    {
        var ilvl = p.ParagraphProperties?.NumberingProperties?
            .GetFirstChild<NumberingLevelReference>()?.Val?.Value;
        return ilvl ?? 0;
    }

    // --- Paragraph reading ---

    private static ParagraphElement ReadParagraph(Paragraph p)
    {
        var style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var alignment = p.ParagraphProperties?.Justification?.Val?.InnerText;
        var framePr = p.ParagraphProperties?.GetFirstChild<FrameProperties>();
        var isDropCap = framePr?.DropCap?.InnerText is "drop" or "margin";

        var runs = new List<RunElement>();

        // Iterate all child elements to handle both regular runs and inline SDTs (citations)
        foreach (var child in p.ChildElements)
        {
            switch (child)
            {
                case Run run:
                    runs.Add(ReadRun(run));
                    break;
                case SdtRun sdtRun:
                    ReadCitationSdt(sdtRun, runs);
                    break;
            }
        }

        return new ParagraphElement(style, alignment, isDropCap, runs);
    }

    /// <summary>
    /// Reads an inline SDT. If it's a citation, extracts the function name from
    /// the CITATION field code's \p parameter and emits it as a synthetic run
    /// with the formatting from the SDT's runs.
    /// </summary>
    private static void ReadCitationSdt(SdtRun sdtRun, List<RunElement> runs)
    {
        // Check if this SDT is a citation
        var isCitation = sdtRun.SdtProperties?.GetFirstChild<SdtContentCitation>() is not null;
        if (!isCitation)
        {
            // Not a citation — read inner runs as normal
            foreach (var run in sdtRun.Descendants<Run>())
                runs.Add(ReadRun(run));
            return;
        }

        // Extract the function name from the CITATION field code
        string? functionName = null;
        RunProperties? fieldRunProps = null;

        foreach (var run in sdtRun.Descendants<Run>())
        {
            var fieldCode = run.GetFirstChild<FieldCode>();
            if (fieldCode is not null)
            {
                var instruction = fieldCode.Text;
                functionName = ParseCitationFieldPage(instruction);
                fieldRunProps = run.RunProperties;
            }
        }

        if (functionName is not null)
        {
            // Create a synthetic run with the function name, preserving formatting
            var props = fieldRunProps;
            runs.Add(new RunElement(
                Text: functionName,
                Bold: props?.Bold is not null && props.Bold.Val is null or { Value: true },
                Italic: props?.Italic is not null && props.Italic.Val is null or { Value: true },
                Strikethrough: props?.Strike is not null && props.Strike.Val is null or { Value: true },
                Superscript: props?.VerticalTextAlignment?.Val?.InnerText == "superscript",
                Subscript: props?.VerticalTextAlignment?.Val?.InnerText == "subscript",
                FontName: props?.RunFonts?.Ascii?.Value,
                FontSize: ParseFontSize(props?.FontSize?.Val?.Value),
                HighlightColor: props?.Highlight?.Val?.InnerText,
                Underline: props?.Underline?.Val?.InnerText,
                SmallCaps: props?.SmallCaps is not null && props.SmallCaps.Val is null or { Value: true }
            ));
        }
    }

    /// <summary>
    /// Parses the \p parameter from a CITATION field instruction.
    /// e.g. "CITATION Fac \p Factorial \n \l 1033" → "Factorial"
    /// </summary>
    private static string? ParseCitationFieldPage(string instruction)
    {
        var parts = instruction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == @"\p")
                return parts[i + 1];
        }
        return null;
    }

    // --- Run reading ---

    private static RunElement ReadRun(Run run)
    {
        var props = run.RunProperties;
        var text = run.InnerText;

        return new RunElement(
            Text: text,
            Bold: props?.Bold is not null && props.Bold.Val is null or { Value: true },
            Italic: props?.Italic is not null && props.Italic.Val is null or { Value: true },
            Strikethrough: props?.Strike is not null && props.Strike.Val is null or { Value: true },
            Superscript: props?.VerticalTextAlignment?.Val?.InnerText == "superscript",
            Subscript: props?.VerticalTextAlignment?.Val?.InnerText == "subscript",
            FontName: props?.RunFonts?.Ascii?.Value,
            FontSize: ParseFontSize(props?.FontSize?.Val?.Value),
            HighlightColor: props?.Highlight?.Val?.InnerText,
            Underline: props?.Underline?.Val?.InnerText,
            SmallCaps: props?.SmallCaps is not null && props.SmallCaps.Val is null or { Value: true },
            Glow: props?.GetFirstChild<DocumentFormat.OpenXml.Office2010.Word.Glow>() is not null,
            Reflection: props?.GetFirstChild<DocumentFormat.OpenXml.Office2010.Word.Reflection>() is not null
        );
    }

    private static double? ParseFontSize(string? val)
    {
        // OpenXML stores font size in half-points
        if (val is not null && double.TryParse(val, out var halfPoints))
            return halfPoints / 2.0;
        return null;
    }

    // --- Table reading ---

    private static TableElement ReadTable(Table table)
    {
        var rows = new List<TableRowElement>();
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = new List<TableCellElement>();
            foreach (var cell in row.Elements<TableCell>())
            {
                var gridSpan = cell.TableCellProperties?
                    .GetFirstChild<GridSpan>()?.Val?.Value ?? 1;

                var cellElements = new List<DocumentElement>();
                foreach (var child in cell.ChildElements)
                {
                    switch (child)
                    {
                        case Paragraph p:
                            cellElements.Add(ReadParagraph(p));
                            break;
                        case Table t:
                            cellElements.Add(ReadTable(t));
                            break;
                    }
                }

                cells.Add(new TableCellElement(gridSpan, cellElements));
            }
            rows.Add(new TableRowElement(cells));
        }

        return new TableElement(rows);
    }
}
