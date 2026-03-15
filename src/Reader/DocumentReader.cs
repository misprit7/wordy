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
            }
        }

        if (pendingListItems.Count > 0)
            elements.Add(new ListElement(pendingListItems));

        return new DocumentIR(elements);
    }

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

    private static ParagraphElement ReadParagraph(Paragraph p)
    {
        var style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var alignment = p.ParagraphProperties?.Justification?.Val?.InnerText;
        var framePr = p.ParagraphProperties?.GetFirstChild<FrameProperties>();
        var isDropCap = framePr?.DropCap?.InnerText is "drop" or "margin";

        var runs = new List<RunElement>();
        foreach (var run in p.Elements<Run>())
        {
            runs.Add(ReadRun(run));
        }

        return new ParagraphElement(style, alignment, isDropCap, runs);
    }

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
            SmallCaps: props?.SmallCaps is not null && props.SmallCaps.Val is null or { Value: true }
        );
    }

    private static double? ParseFontSize(string? val)
    {
        // OpenXML stores font size in half-points
        if (val is not null && double.TryParse(val, out var halfPoints))
            return halfPoints / 2.0;
        return null;
    }

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
