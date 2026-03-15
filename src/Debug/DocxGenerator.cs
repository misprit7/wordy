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
        using var doc = CreateDoc(path);
        var body = doc.MainDocumentPart!.Document!.Body!;

        // Function: Fibonacci (int return, int param)
        body.Append(MakeHeading("Fibonacci", "Courier New"));
        body.Append(MakeSubtitle("N", "Courier New"));

        // If table: N = 0 ∨ N = 1 → return N, else return fib(n−1) + fib(n−2)
        body.Append(MakeIfTable(
            condition: new[] { R("N = 0 ∨ N = 1") },
            trueBranch: new[] { R("N") },
            falseBranch: new[] {
                R("fibonacci ", highlight: HighlightColorValues.Yellow),
                R("n − 1", highlight: HighlightColorValues.Yellow, bold: true),
                R(" + "),
                R("fibonacci ", highlight: HighlightColorValues.Yellow),
                R("n − 2", highlight: HighlightColorValues.Yellow, bold: true),
            },
            rightAlignBranches: true
        ));

        body.Append(new Paragraph());
        AppendDropCapPrint(body, new[] { R("fibonacci 10", bold: true) });
        body.Append(new Paragraph());

        Save(doc);
        Console.WriteLine($"Generated: {path}");
    }

    public static void GenerateComprehensive(string path)
    {
        using var doc = CreateDoc(path);
        var body = doc.MainDocumentPart!.Document!.Body!;

        // ── Function 1: Abs(N: int) → int ──
        // Tests: if statement, comparison (<), subtraction (−), return
        body.Append(MakeHeading("Abs", "Courier New"));
        body.Append(MakeSubtitle("N", "Courier New"));
        body.Append(MakeIfTable(
            condition: new[] { R("N < 0") },
            trueBranch: new[] { R("0 − N") },
            falseBranch: new[] { R("N") },
            rightAlignBranches: true
        ));
        body.Append(new Paragraph());

        // ── Function 2: Classify(N: int) → string ──
        // Tests: match (3-row), comma-separated patterns, wildcard _, italic strings, font casting
        body.Append(MakeHeading("Classify", "Times New Roman"));
        body.Append(MakeSubtitle("N", "Courier New"));
        body.Append(MakeMatchTable(
            subject: new[] { R("N % 3") },
            patterns: new[] { "0", "1", "2", "_" },
            bodies: new Run[][] {
                new[] { R("Fizz", italic: true) },
                new[] { R("one", italic: true) },
                new[] { R("two", italic: true) },
                new[] { R("N", font: "Times New Roman") },  // font cast: N.ToString()
            },
            rightAlignBodies: true
        ));
        body.Append(new Paragraph());

        // ── Function 3: SumTo(Limit: int) → int ──
        // Tests: for loop, assignment (←), reassignment, addition (+), comparison (<=)
        body.Append(MakeHeading("SumTo", "Courier New"));
        body.Append(MakeSubtitle("Limit", "Courier New"));
        body.Append(MakeParagraph(R("Total ← 0")));  // assignment
        body.Append(MakeForTable(
            init: new[] { R("I ← 1") },
            condition: new[] { R("I <= Limit") },
            step: new[] { R("I ← I + 1") },
            bodyParagraphs: new Run[][] {
                new[] { R("Total ← Total + I") },  // reassignment
            }
        ));
        body.Append(MakeRightAligned(R("Total")));  // return
        body.Append(new Paragraph());

        // ── Function 4: Collatz(N: int) → int ──
        // Tests: match (2 cases), modulo (%), division (÷), multiplication (×)
        body.Append(MakeHeading("Collatz", "Courier New"));
        body.Append(MakeSubtitle("N", "Courier New"));
        body.Append(MakeMatchTable(
            subject: new[] { R("N % 2") },
            patterns: new[] { "0", "_" },
            bodies: new Run[][] {
                new[] { R("N ÷ 2") },
                new[] { R("N × 3 + 1") },
            },
            rightAlignBodies: true
        ));
        body.Append(new Paragraph());

        // ── Function 5: CollatzSteps(Start: int) → int ──
        // Tests: for loop with nested function call in body, formatting brackets for call
        body.Append(MakeHeading("CollatzSteps", "Courier New"));
        body.Append(MakeSubtitle("Start", "Courier New"));
        body.Append(MakeParagraph(R("N ← Start")));  // assignment
        body.Append(MakeForTable(
            init: new[] { R("Steps ← 0") },
            condition: new[] { R("N > 1") },
            step: new[] { R("Steps ← Steps + 1") },
            bodyParagraphs: new Run[][] {
                // N ← Collatz(N) — with formatting brackets for function call
                new[] {
                    R("N ← "),
                    R("Collatz ", highlight: HighlightColorValues.Yellow),
                    R("N", highlight: HighlightColorValues.Yellow, bold: true),
                },
            }
        ));
        body.Append(MakeRightAligned(R("Steps")));  // return
        body.Append(new Paragraph());

        // ── Function 6: FormatResult(N: int) → string ──
        // Tests: logical AND (∧), if statement with string return, nested brackets
        body.Append(MakeHeading("FormatResult", "Times New Roman"));
        body.Append(MakeSubtitle("N", "Courier New"));
        body.Append(MakeIfTable(
            condition: new[] { R("N > 0 ∧ N < 100") },
            trueBranch: new[] { R("small", italic: true) },
            falseBranch: new[] { R("big", italic: true) },
            rightAlignBranches: true
        ));
        body.Append(new Paragraph());

        // ── Entry point ──
        // Print abs(0 − 5) → 5
        AppendDropCapPrint(body, new[] {
            R("abs ", bold: true),
            R("0 − 5", bold: true, highlight: HighlightColorValues.Yellow),
        });

        // Print sumto(10) → 55
        body.Append(MakePrintParagraph(new[] {
            R("sumto 10", bold: true),
        }));

        // Print collatzsteps(27) → 111
        body.Append(MakePrintParagraph(new[] {
            R("collatzsteps 27", bold: true),
        }));

        // Print formatresult(42) → "small"
        body.Append(MakePrintParagraph(new[] {
            R("formatresult 42", bold: true),
        }));

        // Print formatresult(200) → "big"
        body.Append(MakePrintParagraph(new[] {
            R("formatresult 200", bold: true),
        }));

        // For loop: print classify(i) for i = 0..3
        // Tests: for loop in main, function call inside print inside loop body
        body.Append(MakeForTable(
            init: new[] { R("I ← 0") },
            condition: new[] { R("I < 4") },
            step: new[] { R("I ← I + 1") },
            bodyParagraphs: new Run[][] {
                new[] {
                    R("Print "),
                    R("classify I", bold: true),
                },
            }
        ));

        body.Append(new Paragraph());
        Save(doc);
        Console.WriteLine($"Generated: {path}");
    }

    // ── Document helpers ──

    private static WordprocessingDocument CreateDoc(string path)
    {
        var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        return doc;
    }

    private static void Save(WordprocessingDocument doc)
    {
        doc.MainDocumentPart!.Document!.Save();
    }

    // ── Run shorthand ──

    private static Run R(string text, string? font = null,
        bool bold = false, bool italic = false,
        HighlightColorValues? highlight = null)
    {
        return MakeRun(text, font ?? "Cambria Math", bold, italic, highlight);
    }

    // ── Paragraph builders ──

    private static Paragraph MakeHeading(string text, string font)
    {
        return new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
            MakeRun(text, font));
    }

    private static Paragraph MakeSubtitle(string text, string font)
    {
        return new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Subtitle" }),
            MakeRun(text, font));
    }

    private static Paragraph MakeParagraph(params Run[] runs)
    {
        var para = new Paragraph();
        foreach (var run in runs)
            para.Append(run);
        return para;
    }

    private static Paragraph MakeRightAligned(params Run[] runs)
    {
        var para = new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Right }));
        foreach (var run in runs)
            para.Append(run);
        return para;
    }

    private static Paragraph MakePrintParagraph(Run[] argRuns)
    {
        var para = new Paragraph();
        para.Append(R("Print "));
        foreach (var run in argRuns)
            para.Append(run);
        return para;
    }

    private static void AppendDropCapPrint(Body body, Run[] argRuns)
    {
        var dropCapPara = new Paragraph();
        dropCapPara.Append(new ParagraphProperties(new FrameProperties
        {
            DropCap = DropCapLocationValues.Margin,
            Lines = 3,
            Wrap = TextWrappingValues.Around,
            VerticalPosition = VerticalAnchorValues.Text,
            HorizontalPosition = HorizontalAnchorValues.Page
        }));
        var pRun = R("P");
        pRun.RunProperties!.Append(new FontSize { Val = "174" });
        dropCapPara.Append(pRun);
        body.Append(dropCapPara);

        var printPara = new Paragraph();
        printPara.Append(R("rint "));
        foreach (var run in argRuns)
            printPara.Append(run);
        body.Append(printPara);
    }

    // ── Table builders ──

    private static TableProperties MakeTableProps(int numCols)
    {
        return new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed });
    }

    private static Table MakeIfTable(Run[] condition, Run[] trueBranch, Run[] falseBranch,
        bool rightAlignBranches = false)
    {
        var table = new Table();
        table.Append(MakeTableProps(2));

        var colW = "4500";
        table.Append(new TableGrid(
            new GridColumn { Width = colW },
            new GridColumn { Width = colW }));

        // Row 0: merged condition
        var condRow = new TableRow();
        condRow.Append(MakeCellFromRuns(condition, gridSpan: 2,
            widthTwips: int.Parse(colW) * 2));
        table.Append(condRow);

        // Row 1: branches
        var branchRow = new TableRow();
        branchRow.Append(MakeCellFromRuns(trueBranch,
            rightAlign: rightAlignBranches, widthTwips: int.Parse(colW)));
        branchRow.Append(MakeCellFromRuns(falseBranch,
            rightAlign: rightAlignBranches, widthTwips: int.Parse(colW)));
        table.Append(branchRow);

        return table;
    }

    private static Table MakeMatchTable(Run[] subject, string[] patterns,
        Run[][] bodies, bool rightAlignBodies = false)
    {
        int n = patterns.Length;
        var table = new Table();
        table.Append(MakeTableProps(n));

        var colW = 9000 / n;
        var grid = new TableGrid();
        for (int i = 0; i < n; i++)
            grid.Append(new GridColumn { Width = colW.ToString() });
        table.Append(grid);

        // Row 0: merged subject
        var subjectRow = new TableRow();
        subjectRow.Append(MakeCellFromRuns(subject, gridSpan: n,
            widthTwips: colW * n));
        table.Append(subjectRow);

        // Row 1: patterns
        var patternRow = new TableRow();
        for (int i = 0; i < n; i++)
            patternRow.Append(MakeCellFromRuns(new[] { R(patterns[i]) },
                widthTwips: colW));
        table.Append(patternRow);

        // Row 2: bodies
        var bodyRow = new TableRow();
        for (int i = 0; i < n; i++)
            bodyRow.Append(MakeCellFromRuns(bodies[i],
                rightAlign: rightAlignBodies, widthTwips: colW));
        table.Append(bodyRow);

        return table;
    }

    private static Table MakeForTable(Run[] init, Run[] condition, Run[] step,
        Run[][] bodyParagraphs)
    {
        var table = new Table();
        table.Append(MakeTableProps(3));

        var colW = 3000;
        table.Append(new TableGrid(
            new GridColumn { Width = colW.ToString() },
            new GridColumn { Width = colW.ToString() },
            new GridColumn { Width = colW.ToString() }));

        // Row 0: init | condition | step
        var headerRow = new TableRow();
        headerRow.Append(MakeCellFromRuns(init, widthTwips: colW));
        headerRow.Append(MakeCellFromRuns(condition, widthTwips: colW));
        headerRow.Append(MakeCellFromRuns(step, widthTwips: colW));
        table.Append(headerRow);

        // Row 1: body (merged across all columns)
        var bodyRow = new TableRow();
        var bodyCell = new TableCell();
        bodyCell.Append(new TableCellProperties(
            new GridSpan { Val = 3 },
            new TableCellWidth { Width = (colW * 3).ToString(), Type = TableWidthUnitValues.Dxa }));
        foreach (var runs in bodyParagraphs)
        {
            var para = new Paragraph();
            foreach (var run in runs)
                para.Append(run);
            bodyCell.Append(para);
        }
        bodyRow.Append(bodyCell);
        table.Append(bodyRow);

        return table;
    }

    // ── Low-level helpers ──

    private static TableCell MakeCellFromRuns(Run[] runs, int gridSpan = 1,
        bool rightAlign = false, int? widthTwips = null)
    {
        var cell = new TableCell();
        var cellProps = new TableCellProperties();
        if (gridSpan > 1)
            cellProps.Append(new GridSpan { Val = gridSpan });
        if (widthTwips is not null)
            cellProps.Append(new TableCellWidth
                { Width = widthTwips.Value.ToString(), Type = TableWidthUnitValues.Dxa });
        cell.Append(cellProps);

        var para = new Paragraph();
        if (rightAlign)
            para.Append(new ParagraphProperties(
                new Justification { Val = JustificationValues.Right }));
        foreach (var run in runs)
            para.Append(run);
        cell.Append(para);
        return cell;
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
}
