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
        // Tests: if statement, comparison (<), unary negation (−N), return
        body.Append(MakeHeading("Abs", "Courier New"));
        body.Append(MakeSubtitle("N", "Courier New"));
        body.Append(MakeIfTable(
            condition: new[] { R("N < 0") },
            trueBranch: new[] { R("−N") },  // unary negation
            falseBranch: new[] { R("N") },
            rightAlignBranches: true
        ));
        body.Append(new Paragraph());

        // ── Function 2: Max(A B: int) → int ──
        // Tests: multi-argument function, comparison (>)
        body.Append(MakeHeading("Max", "Courier New"));
        // Two separate parameter runs so they're parsed as individual params
        body.Append(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Subtitle" }),
            MakeRun("A ", "Courier New"),
            MakeRun("B", "Courier New")));
        body.Append(MakeIfTable(
            condition: new[] { R("A > B") },
            trueBranch: new[] { R("A") },
            falseBranch: new[] { R("B") },
            rightAlignBranches: true
        ));
        body.Append(new Paragraph());

        // ── Function 3: Square(N: int) → int ──
        // Tests: superscript exponentiation
        body.Append(MakeHeading("Square", "Courier New"));
        body.Append(MakeSubtitle("N", "Courier New"));
        // Return N² (N with superscript 2)
        body.Append(MakeRightAligned(R("N"), R("2", superscript: true)));
        body.Append(new Paragraph());

        // ── Function 4: Classify(N: int) → string ──
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
        // Tests: if with Comic Sans bool cast (n%2 in Comic Sans = Convert.ToBoolean),
        //        division (÷), multiplication (×)
        body.Append(MakeHeading("Collatz", "Courier New"));
        body.Append(MakeSubtitle("N", "Courier New"));
        body.Append(MakeIfTable(
            // n % 2 in Comic Sans = cast to bool (truthy = odd, falsy = even)
            condition: new[] { R("N % 2", font: "Comic Sans MS") },
            // true (odd): N × 3 + 1
            trueBranch: new[] { R("N × 3 + 1") },
            // false (even): N ÷ 2
            falseBranch: new[] { R("N ÷ 2") },
            rightAlignBranches: true
        ));
        body.Append(new Paragraph());

        // ── Function 5: CollatzSteps(Start: int) → int ──
        // Tests: for loop with nested function call in body, formatting brackets for call
        body.Append(MakeHeading("CollatzSteps", "Courier New"));
        body.Append(MakeSubtitle("Start", "Courier New"));
        body.Append(MakeParagraph(R("N ← Start")));
        body.Append(MakeForTable(
            init: new[] { R("Steps ← 0") },
            condition: new[] { R("N > 1") },
            step: new[] { R("Steps ← Steps + 1") },
            bodyParagraphs: new Run[][] {
                new[] {
                    R("N ← "),
                    R("Collatz ", highlight: HighlightColorValues.Yellow),
                    R("N", highlight: HighlightColorValues.Yellow, bold: true),
                },
            }
        ));
        body.Append(MakeRightAligned(R("Steps")));
        body.Append(new Paragraph());

        // ── Function 6: SumOdds(Limit: int) → int ──
        // Tests: for loop with nested if in body rows (same table), Comic Sans bool cast
        body.Append(MakeHeading("SumOdds", "Courier New"));
        body.Append(MakeSubtitle("Limit", "Courier New"));
        body.Append(MakeParagraph(R("Total ← 0")));
        body.Append(MakeForWithNestedIf(
            init: new[] { R("I ← 1") },
            condition: new[] { R("I <= Limit") },
            step: new[] { R("I ← I + 1") },
            // Nested if: I % 2 in Comic Sans (bool cast)
            ifCondition: new[] { R("I % 2", font: "Comic Sans MS") },
            // true (odd): add to total
            ifTrue: new[] { R("Total ← Total + I") },
            // false (even): do nothing (empty cell)
            ifFalse: null
        ));
        body.Append(MakeRightAligned(R("Total")));
        body.Append(new Paragraph());

        // ── Function 7: CalcRange(N: int) → int ──
        // Tests: for loop INSIDE an if statement (nested table in cell)
        body.Append(MakeHeading("CalcRange", "Courier New"));
        body.Append(MakeSubtitle("N", "Courier New"));
        body.Append(MakeIfTableWithNestedFor(
            condition: new[] { R("N > 0") },
            // true branch: for loop summing 1 to N, then return Result
            trueInit: new[] { R("Result ← 0") },
            trueForInit: new[] { R("I ← 1") },
            trueForCond: new[] { R("I <= N") },
            trueForStep: new[] { R("I ← I + 1") },
            trueForBody: new Run[][] { new[] { R("Result ← Result + I") } },
            trueReturn: new[] { R("Result") },
            // false branch: return 0
            falseReturn: new[] { R("0") }
        ));
        body.Append(new Paragraph());

        // ── Function 8: FormatResult(N: int) → string ──
        // Tests: logical AND (∧), if statement with string return
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
        // Print abs(−5) → 5 (tests unary negation as argument)
        // Inner highlight bracket groups "−5" as the argument
        AppendDropCapPrint(body, new[] {
            R("abs ", bold: true),
            R("−5", bold: true, highlight: HighlightColorValues.Yellow),
        });

        // Print max(3, 7) → 7 (tests multi-arg function call)
        body.Append(MakePrintParagraph(new[] { R("max 3 7", bold: true) }));

        // Print square(6) → 36 (tests superscript exponentiation)
        body.Append(MakePrintParagraph(new[] { R("square 6", bold: true) }));

        body.Append(MakePrintParagraph(new[] { R("sumto 10", bold: true) }));
        body.Append(MakePrintParagraph(new[] { R("collatzsteps 27", bold: true) }));
        body.Append(MakePrintParagraph(new[] { R("sumodds 10", bold: true) }));
        body.Append(MakePrintParagraph(new[] { R("calcrange 5", bold: true) }));
        body.Append(MakePrintParagraph(new[] { R("calcrange 0", bold: true) }));
        body.Append(MakePrintParagraph(new[] { R("formatresult 42", bold: true) }));
        body.Append(MakePrintParagraph(new[] { R("formatresult 200", bold: true) }));

        // For loop: print classify(i) for i = 0..3
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
        HighlightColorValues? highlight = null, bool superscript = false)
    {
        return MakeRun(text, font ?? "Cambria Math", bold, italic, highlight, superscript);
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
                new TopBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" }),
            new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto });
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

    /// <summary>
    /// For loop table where the body rows form a nested if statement.
    /// Row 0: init | cond | step, Row 1: merged if-condition, Row 2: true | false branches
    /// </summary>
    private static Table MakeForWithNestedIf(Run[] init, Run[] condition, Run[] step,
        Run[] ifCondition, Run[] ifTrue, Run[]? ifFalse)
    {
        var table = new Table();
        table.Append(MakeTableProps(3));

        // Use 2 columns for the if branches, so gridSpan math works:
        // for header has 3 cells but if branches have 2
        // We need the grid to accommodate both: use a common grid
        // Actually simplest: use 2 columns. For header merges as needed.
        var colW = 4500;
        table.Append(new TableGrid(
            new GridColumn { Width = colW.ToString() },
            new GridColumn { Width = colW.ToString() }));

        // Row 0: for header — we'll use 2 cells but pack init+cond+step
        // Actually for 3-cell header with 2-col grid, we need to rethink.
        // Let's use a 4-column grid: init | cond | step maps to cols, if branches use 2 each
        table = new Table();
        table.Append(MakeTableProps(4));
        var cw = 2250;
        table.Append(new TableGrid(
            new GridColumn { Width = cw.ToString() },
            new GridColumn { Width = cw.ToString() },
            new GridColumn { Width = cw.ToString() },
            new GridColumn { Width = cw.ToString() }));

        // Row 0: for header — 3 cells (first spans 2 grid cols to keep 3 visible cells)
        var headerRow = new TableRow();
        headerRow.Append(MakeCellFromRuns(init, widthTwips: cw));
        headerRow.Append(MakeCellFromRuns(condition, gridSpan: 2, widthTwips: cw * 2));
        headerRow.Append(MakeCellFromRuns(step, widthTwips: cw));
        table.Append(headerRow);

        // Row 1: if condition (merged across all 4 cols)
        var condRow = new TableRow();
        condRow.Append(MakeCellFromRuns(ifCondition, gridSpan: 4, widthTwips: cw * 4));
        table.Append(condRow);

        // Row 2: if branches (2 cells, each spanning 2 grid cols)
        var branchRow = new TableRow();
        branchRow.Append(MakeCellFromRuns(ifTrue, gridSpan: 2, widthTwips: cw * 2));
        branchRow.Append(MakeCellFromRuns(ifFalse ?? Array.Empty<Run>(),
            gridSpan: 2, widthTwips: cw * 2));
        table.Append(branchRow);

        return table;
    }

    /// <summary>
    /// If table where the true branch contains a for loop (nested table inside cell).
    /// </summary>
    private static Table MakeIfTableWithNestedFor(Run[] condition,
        Run[] trueInit, Run[] trueForInit, Run[] trueForCond, Run[] trueForStep,
        Run[][] trueForBody, Run[] trueReturn,
        Run[] falseReturn)
    {
        var table = new Table();
        table.Append(MakeTableProps(2));

        var colW = 4500;
        table.Append(new TableGrid(
            new GridColumn { Width = colW.ToString() },
            new GridColumn { Width = colW.ToString() }));

        // Row 0: merged condition
        var condRow = new TableRow();
        condRow.Append(MakeCellFromRuns(condition, gridSpan: 2, widthTwips: colW * 2));
        table.Append(condRow);

        // Row 1: branches
        var branchRow = new TableRow();

        // True branch cell: assignment + nested for loop table + return
        var trueCell = new TableCell();
        trueCell.Append(new TableCellProperties(
            new TableCellWidth { Width = colW.ToString(), Type = TableWidthUnitValues.Dxa }));
        // Assignment before the loop
        trueCell.Append(MakeParagraph(trueInit));
        // Nested for loop table
        trueCell.Append(MakeForTable(trueForInit, trueForCond, trueForStep, trueForBody));
        // Return value (right-aligned)
        var retPara = new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Right }));
        foreach (var run in trueReturn)
            retPara.Append(run);
        trueCell.Append(retPara);
        branchRow.Append(trueCell);

        // False branch cell: just return
        branchRow.Append(MakeCellFromRuns(falseReturn, rightAlign: true, widthTwips: colW));
        table.Append(branchRow);

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
        HighlightColorValues? highlight = null, bool superscript = false)
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
        if (superscript)
            props.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript });

        run.Append(props);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }
}
