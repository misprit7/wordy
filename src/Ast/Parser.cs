using Wordy.Reader;

namespace Wordy.Ast;

public static class Parser
{
    // Context for the function currently being parsed
    private record FunctionContext(WordyType ReturnType, HashSet<string> ParameterNames);
    private static FunctionContext? _currentFunction;

    public static Program Parse(DocumentIR document)
    {
        var functions = new List<Function>();
        var elements = document.Elements;

        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i] is ParagraphElement para && IsHeading(para))
            {
                var func = ParseFunction(elements, ref i);
                if (func is not null)
                    functions.Add(func);
            }
            else if (elements[i] is ParagraphElement dropCapPara && dropCapPara.IsDropCap)
            {
                var main = ParseEntryPoint(elements, ref i);
                if (main is not null)
                    functions.Add(main);
            }
        }

        var imports = document.Imports
            .Select(i => new Import(i.Tag, i.FileName))
            .ToList();

        return new Program(functions, imports);
    }

    // --- Document structure parsing ---

    private static bool IsHeading(ParagraphElement para)
    {
        return para.Style is not null &&
               para.Style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubtitle(ParagraphElement para)
    {
        return para.Style is not null &&
               para.Style.Equals("Subtitle", StringComparison.OrdinalIgnoreCase);
    }

    private static Function? ParseFunction(List<DocumentElement> elements, ref int index)
    {
        var heading = (ParagraphElement)elements[index];
        var name = GetText(heading).Trim().ToLowerInvariant();
        var returnType = DetectReturnType(heading);
        var parameters = new List<Parameter>();
        var body = new List<Stmt>();

        index++;

        // Check for parameters: subtitle style OR plain paragraph with typed font
        if (index < elements.Count && elements[index] is ParagraphElement paramPara)
        {
            if (IsSubtitle(paramPara) || IsParameterParagraph(paramPara))
            {
                parameters = ParseParameters(paramPara);
                index++;
            }
        }

        // Set context for body parsing
        _currentFunction = new FunctionContext(
            returnType,
            new HashSet<string>(parameters.Select(p => p.Name)));

        // Collect body until next heading or drop cap or end
        while (index < elements.Count)
        {
            if (elements[index] is ParagraphElement p && (IsHeading(p) || p.IsDropCap))
                break;

            // Look-ahead: assignment paragraph + list = array assignment
            if (TryParseArrayAssignment(elements, ref index, body))
                continue;

            var stmt = ParseElement(elements[index]);
            if (stmt is not null)
                body.Add(stmt);

            index++;
        }

        index--;
        _currentFunction = null;
        return new Function(name, parameters, returnType, body, false);
    }

    private static bool IsParameterParagraph(ParagraphElement para)
    {
        // A paragraph right after a heading that has typed-font runs
        // (e.g., a single word in Courier New) is treated as parameters
        if (para.Style is not null) return false; // has a style = not a raw param
        if (para.Runs.Count == 0) return false;

        return para.Runs.Any(r =>
            !string.IsNullOrWhiteSpace(r.Text) &&
            FontToType(r.FontName) is not null);
    }

    private static WordyType DetectReturnType(ParagraphElement heading)
    {
        // The heading's font determines the function's return type
        var firstRun = heading.Runs.FirstOrDefault();
        if (firstRun is null) return WordyType.Void;
        return FontToType(firstRun.FontName) ?? WordyType.Void;
    }

    private static Function? ParseEntryPoint(List<DocumentElement> elements, ref int index)
    {
        var body = new List<Stmt>();

        var dropCapPara = (ParagraphElement)elements[index];
        index++;

        // The next paragraph continues the drop cap text — merge runs together
        // so the drop cap initial letter is treated as part of the first line
        var mergedRuns = new List<RunElement>(dropCapPara.Runs);
        if (index < elements.Count && elements[index] is ParagraphElement next &&
            !IsHeading(next) && !next.IsDropCap)
        {
            // Stitch the drop cap's last run with the continuation's first run
            // so "P" + "rint ..." becomes "Print ..."
            if (mergedRuns.Count > 0 && next.Runs.Count > 0)
            {
                var lastDrop = mergedRuns[^1];
                var firstCont = next.Runs[0];
                // Merge if the drop cap text doesn't end with whitespace
                // and the continuation doesn't start with whitespace
                if (!lastDrop.Text.EndsWith(' ') && !firstCont.Text.StartsWith(' '))
                {
                    mergedRuns[^1] = firstCont with { Text = lastDrop.Text + firstCont.Text };
                    mergedRuns.AddRange(next.Runs.Skip(1));
                }
                else
                {
                    mergedRuns.AddRange(next.Runs);
                }
            }
            else
            {
                mergedRuns.AddRange(next.Runs);
            }
            index++;
        }

        // Build a synthetic paragraph from the merged runs and parse it as a statement
        var mergedPara = new ParagraphElement(
            dropCapPara.Style, dropCapPara.Alignment, false, mergedRuns);

        // Check if the merged paragraph is a dangling assignment (ends with ←)
        // and the next element is a list (array literal)
        var mergedText = GetText(mergedPara).Trim();
        if (mergedText.EndsWith('←') && index < elements.Count && elements[index] is ListElement dropCapList)
        {
            var varName = mergedText.TrimEnd('←').Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(varName))
            {
                var arrayExpr = ParseArrayLiteral(dropCapList);
                if (arrayExpr is not null)
                {
                    body.Add(new AssignStmt(varName, arrayExpr));
                    index++;
                }
            }
        }
        else
        {
            var firstStmt = ParseParagraphStmt(mergedPara);
            if (firstStmt is not null)
                body.Add(firstStmt);
        }

        // Continue collecting body elements
        while (index < elements.Count)
        {
            if (elements[index] is ParagraphElement p && (IsHeading(p) || p.IsDropCap))
                break;

            // Look-ahead: assignment paragraph + list = array assignment
            if (TryParseArrayAssignment(elements, ref index, body))
                continue;

            var stmt = ParseElement(elements[index]);
            if (stmt is not null)
                body.Add(stmt);

            index++;
        }

        index--;
        return new Function("Main", new List<Parameter>(), WordyType.Void, body, true);
    }

    private static List<Parameter> ParseParameters(ParagraphElement para)
    {
        var parameters = new List<Parameter>();
        foreach (var run in para.Runs)
        {
            var text = run.Text.Trim();
            if (string.IsNullOrEmpty(text)) continue;

            var type = FontToType(run.FontName);
            if (type is null)
                throw new InvalidOperationException(
                    $"Parameter '{text}' has no type font. Use a type font (Courier New = int, Times New Roman = string, Comic Sans MS = bool, cursive = float, Symbol = char).");
            parameters.Add(new Parameter(text.ToLowerInvariant(), type.Value));
        }
        return parameters;
    }

    // --- Statement parsing ---

    private static Stmt? ParseElement(DocumentElement element)
    {
        return element switch
        {
            ParagraphElement para => ParseParagraphStmt(para),
            TableElement table => ParseTableStmt(table),
            ListElement list => ParseListElement(list),
            _ => null
        };
    }

    private static Stmt? ParseListElement(ListElement list)
    {
        // A standalone list = array literal expression statement
        var expr = ParseArrayLiteral(list);
        return expr is not null ? new ExprStmt(expr) : null;
    }

    private static Stmt? ParseParagraphStmt(ParagraphElement para)
    {
        if (para.Runs.Count == 0 || string.IsNullOrWhiteSpace(GetText(para)))
            return null;

        // Reflection on a char-typed variable (Symbol font) = scan (read character input)
        // No ← needed — the reflection formatting itself declares and assigns
        if (para.Runs.Any(r => r.Reflection))
        {
            var reflectionRuns = para.Runs.Where(r => r.Reflection && !string.IsNullOrWhiteSpace(r.Text)).ToList();
            if (reflectionRuns.Count > 0)
            {
                var varName = string.Join("", reflectionRuns.Select(r => r.Text)).Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(varName))
                    return new AssignStmt(varName, new ScanExpr());
            }
        }

        // Check for assignment (contains ←)
        var assignStmt = TryParseAssignment(para.Runs);
        if (assignStmt is not null)
            return assignStmt;

        // Right-aligned = return statement
        if (para.Alignment == "right")
        {
            var expr = ParseExpression(para.Runs);
            return expr is not null ? new ReturnStmt(expr) : null;
        }

        // Glow formatting = print (alternative to print function call)
        // If any runs have glow, the entire paragraph is a print statement.
        // The glow content is parsed as if inside an implicit bracket (enabling
        // OCaml-style juxtaposition for function calls without explicit bold).
        if (para.Runs.Any(r => r.Glow))
        {
            var tokens = Tokenize(para.Runs);
            if (tokens.Count > 0)
            {
                var pos = 0;
                // Try bracket-content parsing (juxtaposition = function call)
                var expr = ParseBracketContent(tokens, ref pos);
                if (expr is not null)
                    return new PrintStmt(expr);
            }
        }

        // Try to parse as expression, then check for print
        var e = ParseExpression(para.Runs);
        if (e is null) return null;

        // Convert print calls to PrintStmt
        if (e is CallExpr call && call.FunctionName == "print" && call.Arguments.Count > 0)
            return new PrintStmt(call.Arguments[0]);

        return new ExprStmt(e);
    }

    private static AssignStmt? TryParseAssignment(List<RunElement> runs)
    {
        // Check if the runs contain a ← character
        // Split into variable name (left) and value expression (right)
        var allText = string.Join("", runs.Select(r => r.Text));
        var arrowIdx = allText.IndexOf('←');
        if (arrowIdx < 0) return null;

        // Find which runs are before and after the arrow
        var leftRuns = new List<RunElement>();
        var rightRuns = new List<RunElement>();
        var charsSeen = 0;
        var pastArrow = false;

        foreach (var run in runs)
        {
            if (pastArrow)
            {
                rightRuns.Add(run);
                continue;
            }

            var nextEnd = charsSeen + run.Text.Length;
            if (arrowIdx >= charsSeen && arrowIdx < nextEnd)
            {
                // This run contains the arrow
                var localIdx = arrowIdx - charsSeen;
                var before = run.Text.Substring(0, localIdx).Trim();
                var after = run.Text.Substring(localIdx + 1).Trim();

                if (!string.IsNullOrEmpty(before))
                    leftRuns.Add(run with { Text = before });
                if (!string.IsNullOrEmpty(after))
                    rightRuns.Add(run with { Text = after });

                pastArrow = true;
            }
            else
            {
                leftRuns.Add(run);
            }
            charsSeen = nextEnd;
        }

        var varName = string.Join("", leftRuns.Select(r => r.Text)).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(varName)) return null;

        var valueExpr = ParseExpression(rightRuns);
        if (valueExpr is null) return null;

        return new AssignStmt(varName, valueExpr);
    }

    // --- Table parsing ---

    private static Stmt? ParseTableStmt(TableElement table)
    {
        if (table.Rows.Count < 2) return null;

        var firstRow = table.Rows[0];

        return firstRow.Cells.Count switch
        {
            1 => ParseIfOrMatch(table),
            3 => ParseForStmt(table),
            _ => null
        };
    }

    private static Stmt? ParseIfOrMatch(TableElement table)
    {
        var subjectCell = table.Rows[0].Cells[0];
        var condition = ParseCellExpression(subjectCell);
        if (condition is null) return null;

        if (table.Rows.Count == 2)
        {
            // 2-row format: row 1 cells contain both pattern and body
            var caseRow = table.Rows[1];

            if (caseRow.Cells.Count == 2)
            {
                // If statement (bool sugar)
                var trueBranch = ParseCellBody(caseRow.Cells[0]);
                var falseBranch = ParseCellBody(caseRow.Cells[1]);
                return new IfStmt(condition, trueBranch, falseBranch);
            }

            // Match with inline patterns+bodies
            return ParseMatchInline(condition, caseRow);
        }

        if (table.Rows.Count >= 3)
        {
            // 3-row format: row 1 = explicit patterns, row 2 = bodies
            // Always a match statement (patterns are explicit, not bool sugar)
            var patternRow = table.Rows[1];
            var bodyRow = table.Rows[2];
            return ParseMatchSeparateRows(condition, patternRow, bodyRow);
        }

        return null;
    }

    private static MatchStmt? ParseMatchInline(Expr subject, TableRowElement caseRow)
    {
        var cases = new List<MatchCase>();
        foreach (var cell in caseRow.Cells)
        {
            var content = cell.Content;
            var patterns = new List<Expr>();
            var body = new List<Stmt>();

            for (int i = 0; i < content.Count; i++)
            {
                if (i == 0 && content[i] is ParagraphElement firstPara)
                {
                    patterns = ParsePatterns(firstPara.Runs);
                }
                else
                {
                    var stmt = ParseElement(content[i]);
                    if (stmt is not null)
                        body.Add(stmt);
                }
            }

            cases.Add(new MatchCase(patterns, body));
        }

        return new MatchStmt(subject, cases);
    }

    private static MatchStmt? ParseMatchSeparateRows(Expr subject, TableRowElement patternRow, TableRowElement bodyRow)
    {
        var cases = new List<MatchCase>();
        var count = Math.Min(patternRow.Cells.Count, bodyRow.Cells.Count);

        for (int i = 0; i < count; i++)
        {
            var patterns = ParseCellPatterns(patternRow.Cells[i]);
            var body = ParseCellBody(bodyRow.Cells[i]);
            cases.Add(new MatchCase(patterns, body));
        }

        return new MatchStmt(subject, cases);
    }

    private static List<Expr> ParseCellPatterns(TableCellElement cell)
    {
        foreach (var element in cell.Content)
        {
            if (element is ParagraphElement para)
            {
                return ParsePatterns(para.Runs);
            }
        }
        return new List<Expr>();
    }

    private static List<Expr> ParsePatterns(List<RunElement> runs)
    {
        // Patterns can be comma-separated: "3,6,9,12"
        var allText = string.Join("", runs.Select(r => r.Text)).Trim();
        if (string.IsNullOrEmpty(allText)) return new List<Expr>();

        // "_" = wildcard/default case (empty patterns list)
        if (allText == "_")
            return new List<Expr>();

        var patterns = new List<Expr>();
        foreach (var part in allText.Split(','))
        {
            var trimmed = part.Trim();
            if (double.TryParse(trimmed, out var val))
                patterns.Add(new NumberLiteral(val));
            else if (!string.IsNullOrEmpty(trimmed))
                patterns.Add(new StringLiteral(trimmed));
        }
        return patterns;
    }

    private static List<Stmt> ParseCellBody(TableCellElement cell)
    {
        var body = new List<Stmt>();
        foreach (var element in cell.Content)
        {
            var stmt = ParseElement(element);
            if (stmt is not null)
                body.Add(stmt);
        }
        return body;
    }

    private static Stmt? ParseForStmt(TableElement table)
    {
        var topRow = table.Rows[0];
        if (topRow.Cells.Count != 3) return null;

        var init = ParseCellStatement(topRow.Cells[0]);
        var condition = ParseCellExpression(topRow.Cells[1]);
        var step = ParseCellStatement(topRow.Cells[2]);

        if (init is null || condition is null || step is null) return null;

        // Remaining rows form the body
        var bodyRows = table.Rows.Skip(1).ToList();
        var body = ParseBodyRows(bodyRows);

        return new ForStmt(init, condition, step, body);
    }

    private static List<Stmt> ParseBodyRows(List<TableRowElement> rows)
    {
        if (rows.Count == 0) return new List<Stmt>();

        // Check if the body rows form a nested if/match:
        // Row 0 has 1 cell (merged) and Row 1 has 2+ cells
        if (rows.Count >= 2 && rows[0].Cells.Count == 1 && rows[1].Cells.Count >= 2)
        {
            var nestedTable = new TableElement(rows);
            var stmt = ParseIfOrMatch(nestedTable);
            if (stmt is not null)
                return new List<Stmt> { stmt };
        }

        // Otherwise parse each row's cells as sequential statements
        var body = new List<Stmt>();
        foreach (var row in rows)
        {
            foreach (var cell in row.Cells)
            {
                foreach (var element in cell.Content)
                {
                    var stmt = ParseElement(element);
                    if (stmt is not null)
                        body.Add(stmt);
                }
            }
        }
        return body;
    }

    private static Expr? ParseCellExpression(TableCellElement cell)
    {
        // Collect all runs from all paragraphs in the cell
        var allRuns = new List<RunElement>();
        foreach (var element in cell.Content)
        {
            if (element is ParagraphElement para)
                allRuns.AddRange(para.Runs);
        }
        if (allRuns.Count == 0) return null;
        return ParseExpression(allRuns);
    }

    private static Stmt? ParseCellStatement(TableCellElement cell)
    {
        foreach (var element in cell.Content)
        {
            if (element is ParagraphElement para)
            {
                var stmt = ParseParagraphStmt(para);
                if (stmt is not null) return stmt;
            }
        }
        return null;
    }

    // --- Expression parsing with formatting brackets ---

    private static Expr? ParseExpression(List<RunElement> runs)
    {
        var tokens = Tokenize(runs);
        if (tokens.Count == 0) return null;

        var pos = 0;
        var expr = ParseLogicalOr(tokens, ref pos);
        if (expr is null) return null;

        // Expression-level font casting: if all value tokens share a single
        // non-Auto font type and there are multiple value tokens, the font
        // applies to the whole expression, not individual tokens.
        // e.g., "n % 2" all in Comic Sans = Convert.ToBoolean(n % 2)
        var valueTokens = tokens
            .Where(t => t.Kind is TokenKind.Identifier or TokenKind.Number)
            .ToList();
        if (valueTokens.Count > 1)
        {
            var fontTypes = valueTokens
                .Select(t => t.FontType)
                .Where(t => t is not null)
                .Distinct()
                .ToList();

            if (fontTypes.Count == 1)
            {
                expr = new CastExpr(UnwrapCasts(expr), fontTypes[0]!.Value);
            }
        }

        return expr;
    }

    private static Expr UnwrapCasts(Expr expr) => expr switch
    {
        CastExpr c => UnwrapCasts(c.Value),
        BinaryExpr b => new BinaryExpr(UnwrapCasts(b.Left), b.Op, UnwrapCasts(b.Right)),
        UnaryExpr u => new UnaryExpr(u.Op, UnwrapCasts(u.Operand)),
        _ => expr
    };

    private record Token(TokenKind Kind, string Value, FormattingState Fmt, WordyType? FontType, string OriginalText, bool IsSuperscript = false, bool IsSubscript = false);

    private enum TokenKind
    {
        Number,
        String,
        Identifier,
        Operator,
        BracketOpen,
        BracketClose
    }

    [Flags]
    private enum FormattingFlags
    {
        None = 0,
        Bold = 1,
        Highlight = 4,
        // Italic is reserved for string literals, not brackets
    }

    private record FormattingState(FormattingFlags Flags)
    {
        public static FormattingState FromRun(RunElement run)
        {
            var flags = FormattingFlags.None;
            if (run.Bold) flags |= FormattingFlags.Bold;
            if (run.HighlightColor is not null) flags |= FormattingFlags.Highlight;
            return new FormattingState(flags);
        }
    }

    private static List<Token> Tokenize(List<RunElement> runs)
    {
        // Step 1: Split runs into individual word/operator tokens with formatting + font type
        // HadSpace tracks whether whitespace preceded this token (to prevent merging across word boundaries)
        var rawTokens = new List<(string Text, FormattingState Fmt, bool IsOperator, WordyType? FontType, bool IsItalic, bool IsSuperscript, bool IsSubscript, bool HadSpace)>();

        var nextRunHadSpace = true; // first token always counts as preceded by space
        foreach (var run in runs)
        {
            var fmt = FormattingState.FromRun(run);
            var fontType = FontToType(run.FontName);
            var isItalic = run.Italic;
            var isSuperscript = run.Superscript;
            var isSubscript = run.Subscript;
            var text = run.Text;

            if (isItalic)
            {
                var trimmed = text.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    rawTokens.Add((trimmed, fmt, false, fontType, true, isSuperscript, isSubscript, true));
                nextRunHadSpace = true;
            }
            else
            {
                var i = 0;
                var isFirstToken = true;
                while (i < text.Length)
                {
                    var hadSpace = false;
                    while (i < text.Length && char.IsWhiteSpace(text[i])) { i++; hadSpace = true; }
                    if (i >= text.Length) { nextRunHadSpace = true; break; }
                    // First token: use hadSpace OR carried-over space from previous run
                    var precededBySpace = hadSpace || (isFirstToken ? nextRunHadSpace : false);

                    if (IsOperatorChar(text[i]))
                    {
                        var op = ExtractOperator(text, ref i);
                        rawTokens.Add((op, fmt, true, fontType, false, isSuperscript, isSubscript, true));
                    }
                    else if (isSubscript && text[i] == ',')
                    {
                        // Comma in subscript = multidimensional index separator
                        i++;
                        rawTokens.Add((",", fmt, false, fontType, false, false, isSubscript, true));
                    }
                    else
                    {
                        var start = i;
                        while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsOperatorChar(text[i])
                               && !(isSubscript && text[i] == ','))
                            i++;
                        var word = text.Substring(start, i - start);
                        rawTokens.Add((word, fmt, false, fontType, false, isSuperscript, isSubscript, precededBySpace));
                    }
                    isFirstToken = false;
                }
                // If run ended with whitespace or was empty, next run starts a new word
                if (isFirstToken)
                    nextRunHadSpace = text.Length > 0 && text.All(char.IsWhiteSpace) ? true : nextRunHadSpace;
                else
                    nextRunHadSpace = text.Length > 0 && char.IsWhiteSpace(text[^1]);
            }
        }

        // Step 1.5: Merge adjacent word fragments split across runs with same formatting.
        // Word sometimes splits a single word like "number" across multiple runs (e.g. "nu"+"m"+"ber").
        // Only merge when the second token had no preceding whitespace (i.e. it continues the same word).
        for (int i = rawTokens.Count - 1; i > 0; i--)
        {
            var prev = rawTokens[i - 1];
            var curr = rawTokens[i];
            if (!curr.HadSpace && !prev.IsOperator && !curr.IsOperator
                && !prev.IsItalic && !curr.IsItalic
                && prev.Fmt.Flags == curr.Fmt.Flags && prev.FontType == curr.FontType
                && prev.IsSuperscript == curr.IsSuperscript
                && prev.IsSubscript == curr.IsSubscript
                && !double.TryParse(prev.Text, out _) && !double.TryParse(curr.Text, out _))
            {
                rawTokens[i - 1] = (prev.Text + curr.Text, prev.Fmt, false, prev.FontType, false, prev.IsSuperscript, prev.IsSubscript, prev.HadSpace);
                rawTokens.RemoveAt(i);
            }
        }

        // Step 2: Generate bracket tokens from formatting changes
        var tokens = new List<Token>();
        var fmtStack = new List<FormattingFlags>();
        var currentFmt = FormattingFlags.None;

        foreach (var (text, fmt, isOp, fontType, isItalic, isSuperscript, isSubscript, _) in rawTokens)
        {
            var newFmt = fmt.Flags;

            var added = newFmt & ~currentFmt;
            var removed = currentFmt & ~newFmt;

            // Close brackets for removed formatting (reverse order)
            if (removed != FormattingFlags.None)
            {
                for (int i = fmtStack.Count - 1; i >= 0; i--)
                {
                    if ((removed & fmtStack[i]) != 0)
                    {
                        tokens.Add(new Token(TokenKind.BracketClose, ")", fmt, null, ")"));
                        removed &= ~fmtStack[i];
                        fmtStack.RemoveAt(i);
                    }
                }
            }

            // Open brackets for added formatting
            if (added != FormattingFlags.None)
            {
                foreach (FormattingFlags flag in Enum.GetValues<FormattingFlags>())
                {
                    if (flag != FormattingFlags.None && (added & flag) != 0)
                    {
                        tokens.Add(new Token(TokenKind.BracketOpen, "(", fmt, null, "("));
                        fmtStack.Add(flag);
                    }
                }
            }

            currentFmt = newFmt;

            // Italic text = string literal
            if (isItalic)
            {
                tokens.Add(new Token(TokenKind.String, text, fmt, WordyType.String, text, isSuperscript, isSubscript));
            }
            else if (isOp)
            {
                tokens.Add(new Token(TokenKind.Operator, text, fmt, fontType, text, isSuperscript, isSubscript));
            }
            else if (!text.Contains(',') && double.TryParse(text, out _))
            {
                tokens.Add(new Token(TokenKind.Number, text, fmt, fontType, text, isSuperscript, isSubscript));
            }
            else
            {
                tokens.Add(new Token(TokenKind.Identifier, text.ToLowerInvariant(), fmt, fontType, text, isSuperscript, isSubscript));
            }
        }

        // Close any remaining open brackets
        for (int i = fmtStack.Count - 1; i >= 0; i--)
        {
            tokens.Add(new Token(TokenKind.BracketClose, ")", new FormattingState(FormattingFlags.None), null, ")"));
        }

        return tokens;
    }

    private static bool IsOperatorChar(char c)
    {
        return c == '+' || c == '−' || c == '-' || c == '×' || c == '÷' ||
               c == '=' || c == '<' || c == '>' || c == '!' ||
               c == '∨' || c == '∧' || c == '%' || c == '←';
    }

    private static string ExtractOperator(string text, ref int i)
    {
        var c = text[i];
        i++;

        if (i < text.Length)
        {
            if (c == '<' && text[i] == '=') { i++; return "<="; }
            if (c == '>' && text[i] == '=') { i++; return ">="; }
            if (c == '!' && text[i] == '=') { i++; return "!="; }
        }

        return c.ToString();
    }

    // --- Recursive descent parser ---

    private static Expr? ParseLogicalOr(List<Token> tokens, ref int pos)
    {
        var left = ParseLogicalAnd(tokens, ref pos);
        if (left is null) return null;

        while (pos < tokens.Count &&
               tokens[pos].Kind == TokenKind.Operator &&
               tokens[pos].Value == "∨")
        {
            pos++;
            var right = ParseLogicalAnd(tokens, ref pos);
            if (right is null) break;
            left = new BinaryExpr(left, BinaryOp.LogicalOr, right);
        }

        return left;
    }

    private static Expr? ParseLogicalAnd(List<Token> tokens, ref int pos)
    {
        var left = ParseComparison(tokens, ref pos);
        if (left is null) return null;

        while (pos < tokens.Count &&
               tokens[pos].Kind == TokenKind.Operator &&
               tokens[pos].Value == "∧")
        {
            pos++;
            var right = ParseComparison(tokens, ref pos);
            if (right is null) break;
            left = new BinaryExpr(left, BinaryOp.LogicalAnd, right);
        }

        return left;
    }

    private static Expr? ParseComparison(List<Token> tokens, ref int pos)
    {
        var left = ParseAdditive(tokens, ref pos);
        if (left is null) return null;

        if (pos < tokens.Count &&
            tokens[pos].Kind == TokenKind.Operator &&
            tokens[pos].Value is "=" or "!=" or "<" or ">" or "<=" or ">=")
        {
            var opStr = tokens[pos].Value;
            var op = opStr switch
            {
                "=" => BinaryOp.Equal,
                "!=" => BinaryOp.NotEqual,
                "<" => BinaryOp.LessThan,
                ">" => BinaryOp.GreaterThan,
                "<=" => BinaryOp.LessThanOrEqual,
                ">=" => BinaryOp.GreaterThanOrEqual,
                _ => BinaryOp.Equal
            };
            pos++;
            var right = ParseAdditive(tokens, ref pos);
            if (right is not null)
                left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private static Expr? ParseAdditive(List<Token> tokens, ref int pos)
    {
        var left = ParseMultiplicative(tokens, ref pos);
        if (left is null) return null;

        while (pos < tokens.Count &&
               tokens[pos].Kind == TokenKind.Operator &&
               tokens[pos].Value is "+" or "−" or "-")
        {
            var op = tokens[pos].Value == "+" ? BinaryOp.Add : BinaryOp.Subtract;
            pos++;
            var right = ParseMultiplicative(tokens, ref pos);
            if (right is null) break;
            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private static Expr? ParseMultiplicative(List<Token> tokens, ref int pos)
    {
        var left = ParseUnary(tokens, ref pos);
        if (left is null) return null;

        while (pos < tokens.Count &&
               tokens[pos].Kind == TokenKind.Operator &&
               tokens[pos].Value is "×" or "÷" or "%")
        {
            var op = tokens[pos].Value switch
            {
                "×" => BinaryOp.Multiply,
                "÷" => BinaryOp.Divide,
                "%" => BinaryOp.Modulo,
                _ => BinaryOp.Multiply
            };
            pos++;
            var right = ParseUnary(tokens, ref pos);
            if (right is null) break;
            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private static Expr? ParseUnary(List<Token> tokens, ref int pos)
    {
        // Unary negation: − or - at the start of an expression
        if (pos < tokens.Count &&
            tokens[pos].Kind == TokenKind.Operator &&
            tokens[pos].Value is "−" or "-")
        {
            pos++;
            var operand = ParseUnary(tokens, ref pos);
            if (operand is null) return null;
            return new UnaryExpr(UnaryOp.Negate, operand);
        }
        return ParseExponent(tokens, ref pos);
    }

    private static Expr? ParseExponent(List<Token> tokens, ref int pos)
    {
        var baseExpr = ParsePrimary(tokens, ref pos);
        if (baseExpr is null) return null;

        // If the next token(s) are superscript, they form the exponent
        if (pos < tokens.Count && tokens[pos].IsSuperscript)
        {
            var superTokens = new List<Token>();
            while (pos < tokens.Count && tokens[pos].IsSuperscript)
            {
                superTokens.Add(tokens[pos]);
                pos++;
            }
            var superPos = 0;
            var exponent = ParseLogicalOr(superTokens, ref superPos);
            if (exponent is not null)
                baseExpr = new ExponentExpr(baseExpr, exponent);
        }

        // If the next token(s) are subscript, they form an array index
        while (pos < tokens.Count && tokens[pos].IsSubscript)
        {
            var subTokens = new List<Token>();
            while (pos < tokens.Count && tokens[pos].IsSubscript)
            {
                subTokens.Add(tokens[pos]);
                pos++;
            }
            // Check for comma-separated indices (multidimensional access)
            var commaIndices = new List<int>();
            for (int i = 0; i < subTokens.Count; i++)
            {
                if (subTokens[i].Kind == TokenKind.Identifier && subTokens[i].Value == ",")
                    commaIndices.Add(i);
            }
            // Split subscript tokens on commas by checking for comma operators
            // A simpler approach: parse the full subscript, then check if there are commas
            // Actually, commas aren't operators in our tokenizer. Let's check raw text for commas.
            // We need to handle "i,j" as two indices. The tokenizer won't split on commas,
            // so we check if any token text contains a comma and split accordingly.
            var indexGroups = SplitSubscriptOnCommas(subTokens);
            if (indexGroups.Count == 1)
            {
                var subPos = 0;
                var index = ParseLogicalOr(indexGroups[0], ref subPos);
                if (index is not null)
                    baseExpr = new ArrayAccessExpr(baseExpr, index);
            }
            else if (indexGroups.Count > 1)
            {
                var indices = new List<Expr>();
                foreach (var group in indexGroups)
                {
                    var subPos = 0;
                    var idx = ParseLogicalOr(group, ref subPos);
                    if (idx is not null)
                        indices.Add(idx);
                }
                if (indices.Count > 0)
                    baseExpr = new MultiDimArrayAccessExpr(baseExpr, indices);
            }
        }

        return baseExpr;
    }

    private static Expr? ParsePrimary(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count) return null;

        var token = tokens[pos];

        // String literal (italic text)
        if (token.Kind == TokenKind.String)
        {
            pos++;
            return new StringLiteral(token.OriginalText);
        }

        if (token.Kind == TokenKind.BracketOpen)
        {
            pos++;
            var inner = ParseBracketContent(tokens, ref pos);
            if (pos < tokens.Count && tokens[pos].Kind == TokenKind.BracketClose)
                pos++;
            return inner;
        }

        if (token.Kind == TokenKind.Number)
        {
            pos++;
            double.TryParse(token.Value, out var val);
            Expr expr = new NumberLiteral(val);

            if (token.FontType is not null &&
                token.FontType != WordyType.Int &&
                token.FontType != WordyType.Float)
            {
                expr = new CastExpr(expr, token.FontType.Value);
            }

            return expr;
        }

        if (token.Kind == TokenKind.Identifier)
        {
            pos++;
            var name = token.Value;

            // Identifier followed by bracket = function call
            // Delegate to ParseBracketContent which handles juxtaposition + multi-arg
            if (pos < tokens.Count && tokens[pos].Kind == TokenKind.BracketOpen)
            {
                pos++; // consume open bracket
                var content = ParseBracketContent(tokens, ref pos);
                if (pos < tokens.Count && tokens[pos].Kind == TokenKind.BracketClose)
                    pos++;
                // Bracket content is the argument(s) to this function
                var args = new List<Expr>();
                if (content is not null)
                    args.Add(content);
                return new CallExpr(name, args);
            }

            // Font-based casting: variable in a type font = cast
            if (token.FontType is not null)
                return new CastExpr(new VariableRef(name), token.FontType.Value);

            return new VariableRef(name);
        }

        return null;
    }

    /// <summary>
    /// Parses content inside a formatting bracket. If the bracket contains
    /// an identifier followed by values (not operators), it's a function call
    /// with multiple arguments (OCaml-style juxtaposition).
    /// e.g., bold("max a b") = max(a, b)
    /// </summary>
    private static Expr? ParseBracketContent(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count || tokens[pos].Kind == TokenKind.BracketClose)
            return null;

        if (tokens[pos].Kind == TokenKind.Identifier)
        {
            var savedPos = pos;
            var name = tokens[pos].Value;
            pos++;

            // Check if this looks like a function call (not array access via subscript)
            if (pos < tokens.Count &&
                tokens[pos].Kind != TokenKind.BracketClose &&
                tokens[pos].Kind != TokenKind.Operator &&
                !tokens[pos].IsSubscript)
            {
                var args = new List<Expr>();
                while (pos < tokens.Count && tokens[pos].Kind != TokenKind.BracketClose)
                {
                    var arg = ParseLogicalOr(tokens, ref pos);
                    if (arg is null) break;
                    args.Add(arg);
                }
                if (args.Count > 0)
                    return new CallExpr(name, args);
            }

            pos = savedPos;
        }

        return ParseLogicalOr(tokens, ref pos);
    }

    // --- Subscript helpers ---

    private static List<List<Token>> SplitSubscriptOnCommas(List<Token> tokens)
    {
        var groups = new List<List<Token>>();
        var current = new List<Token>();

        foreach (var token in tokens)
        {
            // Comma token = dimension separator
            if (token.Kind == TokenKind.Identifier && token.Value == ",")
            {
                if (current.Count > 0)
                    groups.Add(current);
                current = new List<Token>();
            }
            // Check if this token's text contains a comma (tokenizer may keep "i,j" as one token)
            else if (token.Kind == TokenKind.Identifier && token.Value.Contains(','))
            {
                var parts = token.Value.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Trim();
                    if (!string.IsNullOrEmpty(part))
                        current.Add(token with { Value = part, OriginalText = part });
                    if (i < parts.Length - 1)
                    {
                        if (current.Count > 0)
                            groups.Add(current);
                        current = new List<Token>();
                    }
                }
            }
            else
            {
                current.Add(token);
            }
        }

        if (current.Count > 0)
            groups.Add(current);

        return groups;
    }

    /// <summary>
    /// Look-ahead: if current element is a paragraph ending with ← and next element is a list,
    /// parse as array assignment: variable ← [list]
    /// </summary>
    private static bool TryParseArrayAssignment(List<DocumentElement> elements, ref int index, List<Stmt> body)
    {
        if (elements[index] is not ParagraphElement assignPara) return false;
        var text = GetText(assignPara).Trim();
        if (!text.EndsWith('←')) return false;
        if (index + 1 >= elements.Count) return false;
        if (elements[index + 1] is not ListElement list) return false;

        var varName = text.TrimEnd('←').Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(varName)) return false;

        var arrayExpr = ParseArrayLiteral(list);
        if (arrayExpr is null) return false;

        body.Add(new AssignStmt(varName, arrayExpr));
        index += 2; // skip both paragraph and list
        return true;
    }

    // --- Array literal parsing ---

    private static Expr? ParseArrayLiteral(ListElement list)
    {
        bool hasSubItems = list.Items.Any(i => i.IndentLevel > 0);

        if (!hasSubItems)
        {
            // 1D array
            var elements = new List<Expr>();
            WordyType? elementType = null;
            foreach (var item in list.Items)
            {
                var expr = ParseExpression(item.Runs);
                if (expr is not null) elements.Add(expr);
                elementType ??= item.Runs
                    .Where(r => !string.IsNullOrWhiteSpace(r.Text))
                    .Select(r => FontToType(r.FontName))
                    .FirstOrDefault(t => t is not null);
            }
            return new ArrayLiteralExpr(elementType ?? WordyType.Int, elements);
        }
        else
        {
            // 2D array: indent-0 items are row separators (blank), indent-1+ items are values
            var rows = new List<List<Expr>>();
            List<Expr>? currentRow = null;
            WordyType? elementType = null;
            foreach (var item in list.Items)
            {
                if (item.IndentLevel == 0)
                {
                    currentRow = new List<Expr>();
                    rows.Add(currentRow);
                }
                else if (currentRow is not null)
                {
                    var expr = ParseExpression(item.Runs);
                    if (expr is not null) currentRow.Add(expr);
                    elementType ??= item.Runs
                        .Where(r => !string.IsNullOrWhiteSpace(r.Text))
                        .Select(r => FontToType(r.FontName))
                        .FirstOrDefault(t => t is not null);
                }
            }
            return new ArrayLiteral2DExpr(elementType ?? WordyType.Int, rows);
        }
    }

    // --- Utilities ---

    private static string GetText(ParagraphElement para)
    {
        return string.Join("", para.Runs.Select(r => r.Text));
    }

    public static WordyType? FontToType(string? fontName)
    {
        if (fontName is null) return null;

        return fontName.ToLowerInvariant() switch
        {
            "times new roman" => WordyType.String,
            "courier new" => WordyType.Int,
            "comic sans ms" => WordyType.Bool,
            "brush script mt" or "lucida handwriting" or "segoe script" => WordyType.Float,
            "symbol" => WordyType.Char,
            _ => null
        };
    }
}
