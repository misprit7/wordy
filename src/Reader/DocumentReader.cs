using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Wordy.Reader;

public static class DocumentReader
{
    public static DocumentIR Read(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("Document has no body");

        var elements = new List<DocumentElement>();

        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case Paragraph p:
                    elements.Add(ReadParagraph(p));
                    break;
                case Table t:
                    elements.Add(ReadTable(t));
                    break;
            }
        }

        return new DocumentIR(elements);
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
