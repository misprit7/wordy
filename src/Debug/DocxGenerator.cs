using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Wordy.Debug;

/// <summary>
/// Generates valid .docx test programs for the Wordy compiler.
/// </summary>
public static class DocxGenerator
{
    public static void GenerateFibonacci(string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        var body = new Body();
        mainPart.Document = new Document(body);

        // --- Function: Fibonacci ---
        // Heading1 "Fibonacci" in Courier New (return type = int)
        body.Append(MakeHeading("Fibonacci", "Courier New", "Heading1"));

        // Subtitle "N" in Courier New (parameter type = int)
        body.Append(MakeStyledParagraph("N", "Courier New", "Subtitle"));

        // If table: N = 0 ∨ N = 1 → return N, else return fib(n−1) + fib(n−2)
        var table = new Table();

        // Table properties with borders
        var tblPr = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
            ),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
        );
        table.Append(tblPr);

        // Define grid: 2 equal columns
        var colWidth = "4500";
        var grid = new TableGrid(
            new GridColumn { Width = colWidth },
            new GridColumn { Width = colWidth });
        table.Append(grid);

        // Row 0: merged condition cell
        var condRow = new TableRow();
        var condCell = MakeCell("N = 0 ∨ N = 1", "Cambria Math", gridSpan: 2,
            widthTwips: int.Parse(colWidth) * 2);
        condRow.Append(condCell);
        table.Append(condRow);

        // Row 1: two branch cells
        var branchRow = new TableRow();

        // True branch: right-aligned "N" (return N)
        var trueCell = new TableCell();
        trueCell.Append(new TableCellProperties(
            new TableCellWidth { Width = colWidth, Type = TableWidthUnitValues.Dxa }));
        var truePara = new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
            MakeRun("N", "Cambria Math")
        );
        trueCell.Append(truePara);
        branchRow.Append(trueCell);

        // False branch: right-aligned "fibonacci(n−1) + fibonacci(n−2)"
        var falseCell = new TableCell();
        falseCell.Append(new TableCellProperties(
            new TableCellWidth { Width = colWidth, Type = TableWidthUnitValues.Dxa }));
        var falsePara = new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Right })
        );
        // fibonacci [highlight=yellow] (n − 1) [highlight+bold] + fibonacci [highlight] (n − 2) [highlight+bold]
        falsePara.Append(MakeRun("fibonacci ", "Cambria Math", highlight: HighlightColorValues.Yellow));
        falsePara.Append(MakeRun("n − 1", "Cambria Math", highlight: HighlightColorValues.Yellow, bold: true));
        falsePara.Append(MakeRun(" + ", "Cambria Math"));
        falsePara.Append(MakeRun("fibonacci ", "Cambria Math", highlight: HighlightColorValues.Yellow));
        falsePara.Append(MakeRun("n − 2", "Cambria Math", highlight: HighlightColorValues.Yellow, bold: true));
        falseCell.Append(falsePara);
        branchRow.Append(falseCell);

        table.Append(branchRow);
        body.Append(table);

        // Empty paragraph separator
        body.Append(new Paragraph());

        // --- Entry point: Print fibonacci(10) ---
        // Drop cap "P"
        var dropCapPara = new Paragraph();
        var framePr = new FrameProperties
        {
            DropCap = DropCapLocationValues.Margin,
            Lines = 3,
            Wrap = TextWrappingValues.Around,
            VerticalPosition = VerticalAnchorValues.Text,
            HorizontalPosition = HorizontalAnchorValues.Page
        };
        dropCapPara.Append(new ParagraphProperties(framePr));
        var pRun = MakeRun("P", "Cambria Math");
        // Set large font size for drop cap (87pt = 174 half-points)
        pRun.RunProperties!.Append(new FontSize { Val = "174" });
        dropCapPara.Append(pRun);
        body.Append(dropCapPara);

        // Continuation: "rint " + bold("fibonacci 10")
        var printPara = new Paragraph();
        printPara.Append(MakeRun("rint ", "Cambria Math"));
        printPara.Append(MakeRun("fibonacci 10", "Cambria Math", bold: true));
        body.Append(printPara);

        // Final empty paragraph (Word likes having one)
        body.Append(new Paragraph());

        mainPart.Document.Save();
        Console.WriteLine($"Generated: {path}");
    }

    // --- Helper methods ---

    private static Paragraph MakeHeading(string text, string font, string styleId)
    {
        var para = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
            MakeRun(text, font)
        );
        return para;
    }

    private static Paragraph MakeStyledParagraph(string text, string font, string styleId)
    {
        var para = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
            MakeRun(text, font)
        );
        return para;
    }

    private static Run MakeRun(string text, string? font = null,
        bool bold = false, bool italic = false,
        HighlightColorValues? highlight = null)
    {
        var run = new Run();
        var props = new RunProperties();

        if (font is not null)
            props.Append(new RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font });
        if (bold)
            props.Append(new Bold());
        if (italic)
            props.Append(new Italic());
        if (highlight is not null)
            props.Append(new Highlight { Val = highlight });

        run.Append(props);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static TableCell MakeCell(string text, string? font = null,
        int gridSpan = 1, int? widthTwips = null)
    {
        var cell = new TableCell();
        var cellProps = new TableCellProperties();
        if (gridSpan > 1)
            cellProps.Append(new GridSpan { Val = gridSpan });
        if (widthTwips is not null)
            cellProps.Append(new TableCellWidth
                { Width = widthTwips.Value.ToString(), Type = TableWidthUnitValues.Dxa });
        cell.Append(cellProps);
        cell.Append(new Paragraph(MakeRun(text, font)));
        return cell;
    }
}
