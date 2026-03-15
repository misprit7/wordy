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

        return new Program(functions);
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
            FontToType(r.FontName) != WordyType.Auto);
    }

    private static WordyType DetectReturnType(ParagraphElement heading)
    {
        // The heading's font determines the function's return type
        var firstRun = heading.Runs.FirstOrDefault();
        if (firstRun is null) return WordyType.Auto;
        return FontToType(firstRun.FontName);
    }

    private static Function? ParseEntryPoint(List<DocumentElement> elements, ref int index)
    {
        var body = new List<Stmt>();

        var dropCapPara = (ParagraphElement)elements[index];
        var dropCapText = GetText(dropCapPara);
        index++;

        // The next paragraph continues the drop cap text
        ParagraphElement? continuationPara = null;
        if (index < elements.Count && elements[index] is ParagraphElement next &&
            !IsHeading(next) && !next.IsDropCap)
        {
            continuationPara = next;
            index++;
        }

        // Merge text to check for "print"
        var allRuns = new List<RunElement>(dropCapPara.Runs);
        if (continuationPara is not null)
            allRuns.AddRange(continuationPara.Runs);

        var mergedText = string.Join("", allRuns.Select(r => r.Text)).Trim().ToLowerInvariant();

        if (mergedText.StartsWith("print"))
        {
            if (continuationPara is not null)
            {
                var argRuns = GetRunsAfterPrefix(continuationPara.Runs, "rint");
                if (argRuns.Count > 0)
                {
                    var argExpr = ParseExpression(argRuns);
                    if (argExpr is not null)
                        body.Add(new PrintStmt(argExpr));
                }
            }
        }

        // Continue collecting body elements
        while (index < elements.Count)
        {
            if (elements[index] is ParagraphElement p && (IsHeading(p) || p.IsDropCap))
                break;

            var stmt = ParseElement(elements[index]);
            if (stmt is not null)
                body.Add(stmt);

            index++;
        }

        index--;
        return new Function("Main", new List<Parameter>(), WordyType.Void, body, true);
    }

    private static List<RunElement> GetRunsAfterPrefix(List<RunElement> runs, string prefix)
    {
        var result = new List<RunElement>();
        var prefixRemaining = prefix.ToLowerInvariant();
        bool pastPrefix = false;

        foreach (var run in runs)
        {
            if (pastPrefix)
            {
                result.Add(run);
                continue;
            }

            var text = run.Text.ToLowerInvariant().TrimStart();
            if (text.StartsWith(prefixRemaining))
            {
                var remaining = run.Text.Substring(
                    run.Text.ToLowerInvariant().IndexOf(prefixRemaining) + prefixRemaining.Length);
                remaining = remaining.TrimStart();
                if (!string.IsNullOrEmpty(remaining))
                    result.Add(run with { Text = remaining });
                pastPrefix = true;
            }
            else if (prefixRemaining.StartsWith(text.TrimEnd()))
            {
                prefixRemaining = prefixRemaining.Substring(text.TrimEnd().Length);
            }
        }

        return result;
    }

    private static List<Parameter> ParseParameters(ParagraphElement para)
    {
        var parameters = new List<Parameter>();
        foreach (var run in para.Runs)
        {
            var text = run.Text.Trim();
            if (string.IsNullOrEmpty(text)) continue;

            var type = FontToType(run.FontName);
            parameters.Add(new Parameter(text.ToLowerInvariant(), type));
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
            _ => null
        };
    }

    private static Stmt? ParseParagraphStmt(ParagraphElement para)
    {
        if (para.Runs.Count == 0 || string.IsNullOrWhiteSpace(GetText(para)))
            return null;

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
            // 3-row format: row 1 = patterns, row 2 = bodies
            var patternRow = table.Rows[1];
            var bodyRow = table.Rows[2];

            if (patternRow.Cells.Count == 2 && bodyRow.Cells.Count == 2)
            {
                // If statement
                var trueBranch = ParseCellBody(bodyRow.Cells[0]);
                var falseBranch = ParseCellBody(bodyRow.Cells[1]);
                return new IfStmt(condition, trueBranch, falseBranch);
            }

            // Match with separate pattern and body rows
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
        return ParseLogicalOr(tokens, ref pos);
    }

    private record Token(TokenKind Kind, string Value, FormattingState Fmt, WordyType FontType, string OriginalText);

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
        var rawTokens = new List<(string Text, FormattingState Fmt, bool IsOperator, WordyType FontType, bool IsItalic)>();

        foreach (var run in runs)
        {
            var fmt = FormattingState.FromRun(run);
            var fontType = FontToType(run.FontName);
            var isItalic = run.Italic;
            var text = run.Text;

            if (isItalic)
            {
                // Italic text is a string literal — don't split by operators/whitespace,
                // preserve the raw text as-is
                var trimmed = text.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    rawTokens.Add((trimmed, fmt, false, fontType, true));
            }
            else
            {
                var i = 0;
                while (i < text.Length)
                {
                    while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                    if (i >= text.Length) break;

                    if (IsOperatorChar(text[i]))
                    {
                        var op = ExtractOperator(text, ref i);
                        rawTokens.Add((op, fmt, true, fontType, false));
                    }
                    else
                    {
                        var start = i;
                        while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsOperatorChar(text[i]))
                            i++;
                        var word = text.Substring(start, i - start);
                        rawTokens.Add((word, fmt, false, fontType, false));
                    }
                }
            }
        }

        // Step 2: Generate bracket tokens from formatting changes
        var tokens = new List<Token>();
        var fmtStack = new List<FormattingFlags>();
        var currentFmt = FormattingFlags.None;

        foreach (var (text, fmt, isOp, fontType, isItalic) in rawTokens)
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
                        tokens.Add(new Token(TokenKind.BracketClose, ")", fmt, WordyType.Auto, ")"));
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
                        tokens.Add(new Token(TokenKind.BracketOpen, "(", fmt, WordyType.Auto, "("));
                        fmtStack.Add(flag);
                    }
                }
            }

            currentFmt = newFmt;

            // Italic text = string literal
            if (isItalic)
            {
                tokens.Add(new Token(TokenKind.String, text, fmt, WordyType.String, text));
            }
            else if (isOp)
            {
                tokens.Add(new Token(TokenKind.Operator, text, fmt, fontType, text));
            }
            else if (double.TryParse(text, out _))
            {
                tokens.Add(new Token(TokenKind.Number, text, fmt, fontType, text));
            }
            else
            {
                tokens.Add(new Token(TokenKind.Identifier, text.ToLowerInvariant(), fmt, fontType, text));
            }
        }

        // Close any remaining open brackets
        for (int i = fmtStack.Count - 1; i >= 0; i--)
        {
            tokens.Add(new Token(TokenKind.BracketClose, ")", new FormattingState(FormattingFlags.None), WordyType.Auto, ")"));
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
        var left = ParsePrimary(tokens, ref pos);
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
            var right = ParsePrimary(tokens, ref pos);
            if (right is null) break;
            left = new BinaryExpr(left, op, right);
        }

        return left;
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

            // Font-based casting: only cast if the font implies a non-numeric type
            // (numbers are naturally int/float, so casting to int is a no-op)
            if (token.FontType != WordyType.Auto &&
                token.FontType != WordyType.Int &&
                token.FontType != WordyType.Float)
            {
                expr = new CastExpr(expr, token.FontType);
            }

            return expr;
        }

        if (token.Kind == TokenKind.Identifier)
        {
            pos++;
            var name = token.Value;

            // Identifier followed by bracket = function call
            // Parse entire bracket content as a single argument (with juxtaposition)
            if (pos < tokens.Count && tokens[pos].Kind == TokenKind.BracketOpen)
            {
                pos++; // consume open bracket
                var args = new List<Expr>();
                var content = ParseBracketContent(tokens, ref pos);
                if (content is not null)
                    args.Add(content);
                if (pos < tokens.Count && tokens[pos].Kind == TokenKind.BracketClose)
                    pos++;
                return new CallExpr(name, args);
            }

            // Font-based casting: variable in a type font = cast
            if (token.FontType != WordyType.Auto)
                return new CastExpr(new VariableRef(name), token.FontType);


            return new VariableRef(name);
        }

        return null;
    }

    private static Expr? ParseBracketContent(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count || tokens[pos].Kind == TokenKind.BracketClose)
            return null;

        // If first token is an identifier and there are more non-operator
        // tokens before the close bracket, it's a function call (juxtaposition)
        if (tokens[pos].Kind == TokenKind.Identifier)
        {
            var savedPos = pos;
            var name = tokens[pos].Value;
            pos++;

            if (pos < tokens.Count &&
                tokens[pos].Kind != TokenKind.BracketClose &&
                tokens[pos].Kind != TokenKind.Operator)
            {
                var arg = ParseLogicalOr(tokens, ref pos);
                if (arg is not null)
                    return new CallExpr(name, new List<Expr> { arg });
            }

            pos = savedPos;
        }

        return ParseLogicalOr(tokens, ref pos);
    }

    // --- Utilities ---

    private static string GetText(ParagraphElement para)
    {
        return string.Join("", para.Runs.Select(r => r.Text));
    }

    public static WordyType FontToType(string? fontName)
    {
        if (fontName is null) return WordyType.Auto;

        return fontName.ToLowerInvariant() switch
        {
            "times new roman" => WordyType.String,
            "courier new" => WordyType.Int,
            "comic sans ms" => WordyType.Bool,
            "brush script mt" or "lucida handwriting" or "segoe script" => WordyType.Float,
            "wingdings" => WordyType.Void,
            "symbol" => WordyType.Char,
            "impact" => WordyType.Error,
            "calibri" => WordyType.Auto,
            "cambria math" => WordyType.Auto, // equation font, not a type indicator
            _ => WordyType.Auto
        };
    }
}
