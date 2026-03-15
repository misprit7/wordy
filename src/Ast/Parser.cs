using Wordy.Reader;

namespace Wordy.Ast;

public static class Parser
{
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
        var name = GetText(heading).Trim();
        var parameters = new List<Parameter>();
        var body = new List<Stmt>();

        index++;

        // Check for subtitle (parameters)
        if (index < elements.Count &&
            elements[index] is ParagraphElement subtitle && IsSubtitle(subtitle))
        {
            parameters = ParseParameters(subtitle);
            index++;
        }

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

        // Back up so the outer loop sees the next element
        index--;

        return new Function(name, parameters, WordyType.Auto, body, false);
    }

    private static Function? ParseEntryPoint(List<DocumentElement> elements, ref int index)
    {
        // Drop cap paragraph + following paragraph(s) form the entry point
        // The drop cap "P" + next paragraph "rint ..." = "Print ..."
        // Collect all text from drop cap paragraph and subsequent non-heading paragraphs
        var body = new List<Stmt>();

        // Merge the drop cap paragraph with the next paragraph
        var dropCapPara = (ParagraphElement)elements[index];
        var dropCapText = GetText(dropCapPara);
        index++;

        // The next paragraph continues the text
        ParagraphElement? continuationPara = null;
        if (index < elements.Count && elements[index] is ParagraphElement next &&
            !IsHeading(next) && !next.IsDropCap)
        {
            continuationPara = next;
            index++;
        }

        // Merge runs: drop cap runs + continuation runs
        var allRuns = new List<RunElement>(dropCapPara.Runs);
        if (continuationPara is not null)
            allRuns.AddRange(continuationPara.Runs);

        // Parse the merged content as a statement
        var mergedText = string.Join("", allRuns.Select(r => r.Text)).Trim().ToLowerInvariant();

        // Check if it starts with "print"
        if (mergedText.StartsWith("print"))
        {
            // The argument to print is everything after "print" — use the continuation runs
            // which contain the actual formatted content
            if (continuationPara is not null)
            {
                // Skip the "rint " prefix in the continuation, get the real argument runs
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
        // Skip runs that are part of the prefix text, return the rest
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
                // This run contains the end of the prefix
                var remaining = run.Text.Substring(
                    run.Text.ToLowerInvariant().IndexOf(prefixRemaining) + prefixRemaining.Length);
                remaining = remaining.TrimStart();
                if (!string.IsNullOrEmpty(remaining))
                {
                    result.Add(run with { Text = remaining });
                }
                pastPrefix = true;
            }
            else if (prefixRemaining.StartsWith(text.TrimEnd()))
            {
                prefixRemaining = prefixRemaining.Substring(text.TrimEnd().Length);
            }
        }

        return result;
    }

    private static List<Parameter> ParseParameters(ParagraphElement subtitle)
    {
        var parameters = new List<Parameter>();
        foreach (var run in subtitle.Runs)
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

        // Right-aligned = return statement
        if (para.Alignment == "right")
        {
            var expr = ParseExpression(para.Runs);
            return expr is not null ? new ReturnStmt(expr) : null;
        }

        // Default: try to parse as expression statement
        var e = ParseExpression(para.Runs);
        return e is not null ? new ExprStmt(e) : null;
    }

    private static Stmt? ParseTableStmt(TableElement table)
    {
        if (table.Rows.Count < 2) return null;

        var firstRow = table.Rows[0];

        // 1 cell in top row = match/if statement
        // 3 cells in top row = for loop
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

        var caseRow = table.Rows[1];

        // 2 cells in bottom row = if statement (bool sugar)
        if (caseRow.Cells.Count == 2)
        {
            var trueBranch = ParseCellBody(caseRow.Cells[0]);
            var falseBranch = ParseCellBody(caseRow.Cells[1]);
            return new IfStmt(condition, trueBranch, falseBranch);
        }

        // More cells = match statement
        var cases = new List<MatchCase>();
        foreach (var cell in caseRow.Cells)
        {
            var content = cell.Content;
            Expr? pattern = null;
            var body = new List<Stmt>();

            for (int i = 0; i < content.Count; i++)
            {
                if (i == 0 && content[i] is ParagraphElement firstPara)
                {
                    pattern = ParseExpression(firstPara.Runs);
                }
                else
                {
                    var stmt = ParseElement(content[i]);
                    if (stmt is not null)
                        body.Add(stmt);
                }
            }

            cases.Add(new MatchCase(pattern, body));
        }

        return new MatchStmt(condition, cases);
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

        var body = new List<Stmt>();
        if (table.Rows.Count >= 2)
        {
            foreach (var cell in table.Rows[1].Cells)
            {
                foreach (var element in cell.Content)
                {
                    var stmt = ParseElement(element);
                    if (stmt is not null)
                        body.Add(stmt);
                }
            }
        }

        return new ForStmt(init, condition, step, body);
    }

    private static Expr? ParseCellExpression(TableCellElement cell)
    {
        foreach (var element in cell.Content)
        {
            if (element is ParagraphElement para)
            {
                var expr = ParseExpression(para.Runs);
                if (expr is not null) return expr;
            }
        }
        return null;
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

    // Tokenizer: splits runs into tokens, inserting bracket open/close
    // tokens when formatting changes between adjacent words.

    private record Token(TokenKind Kind, string Value, FormattingState Fmt);

    private enum TokenKind
    {
        Number,
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
        Italic = 2,
        Highlight = 4,
        // Add more as needed
    }

    private record FormattingState(FormattingFlags Flags)
    {
        public static FormattingState FromRun(RunElement run)
        {
            var flags = FormattingFlags.None;
            if (run.Bold) flags |= FormattingFlags.Bold;
            if (run.Italic) flags |= FormattingFlags.Italic;
            if (run.HighlightColor is not null) flags |= FormattingFlags.Highlight;
            return new FormattingState(flags);
        }
    }

    private static List<Token> Tokenize(List<RunElement> runs)
    {
        // Step 1: Split runs into individual word/operator tokens with formatting
        var rawTokens = new List<(string Text, FormattingState Fmt, bool IsOperator)>();

        foreach (var run in runs)
        {
            var fmt = FormattingState.FromRun(run);
            var text = run.Text;

            // Split by spaces and operators, preserving operators as separate tokens
            var i = 0;
            while (i < text.Length)
            {
                // Skip whitespace
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                if (i >= text.Length) break;

                // Check for operator characters
                if (IsOperatorChar(text[i]))
                {
                    var op = ExtractOperator(text, ref i);
                    rawTokens.Add((op, fmt, true));
                }
                else
                {
                    // Read a word
                    var start = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsOperatorChar(text[i]))
                        i++;
                    var word = text.Substring(start, i - start);
                    rawTokens.Add((word, fmt, false));
                }
            }
        }

        // Step 2: Generate bracket tokens from formatting changes
        var tokens = new List<Token>();
        var fmtStack = new List<FormattingFlags>(); // formatting layers currently open
        var currentFmt = FormattingFlags.None;

        foreach (var (text, fmt, isOp) in rawTokens)
        {
            var newFmt = fmt.Flags;

            // Detect formatting that was added
            var added = newFmt & ~currentFmt;
            // Detect formatting that was removed
            var removed = currentFmt & ~newFmt;

            // Close brackets for removed formatting (in reverse order)
            if (removed != FormattingFlags.None)
            {
                for (int i = fmtStack.Count - 1; i >= 0; i--)
                {
                    if ((removed & fmtStack[i]) != 0)
                    {
                        tokens.Add(new Token(TokenKind.BracketClose, ")", fmt));
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
                        tokens.Add(new Token(TokenKind.BracketOpen, "(", fmt));
                        fmtStack.Add(flag);
                    }
                }
            }

            currentFmt = newFmt;

            // Add the actual token
            if (isOp)
            {
                tokens.Add(new Token(TokenKind.Operator, text, fmt));
            }
            else if (double.TryParse(text, out var numVal))
            {
                tokens.Add(new Token(TokenKind.Number, text, fmt));
            }
            else
            {
                tokens.Add(new Token(TokenKind.Identifier, text.ToLowerInvariant(), fmt));
            }
        }

        // Close any remaining open brackets
        for (int i = fmtStack.Count - 1; i >= 0; i--)
        {
            tokens.Add(new Token(TokenKind.BracketClose, ")", new FormattingState(FormattingFlags.None)));
        }

        return tokens;
    }

    private static bool IsOperatorChar(char c)
    {
        return c == '+' || c == '−' || c == '-' || c == '×' || c == '÷' ||
               c == '=' || c == '<' || c == '>' || c == '!' ||
               c == '∨' || c == '∧';
    }

    private static string ExtractOperator(string text, ref int i)
    {
        var c = text[i];
        i++;

        // Two-character operators
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
               tokens[pos].Value is "×" or "÷")
        {
            var op = tokens[pos].Value == "×" ? BinaryOp.Multiply : BinaryOp.Divide;
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

        // Bracketed expression: could be a grouping or the start of a
        // function call if the first thing inside is an identifier followed
        // by more tokens.
        if (token.Kind == TokenKind.BracketOpen)
        {
            pos++;
            var inner = ParseBracketContent(tokens, ref pos);
            // Consume matching close bracket
            if (pos < tokens.Count && tokens[pos].Kind == TokenKind.BracketClose)
                pos++;
            return inner;
        }

        // Number literal
        if (token.Kind == TokenKind.Number)
        {
            pos++;
            double.TryParse(token.Value, out var val);
            return new NumberLiteral(val);
        }

        // Identifier — possibly a function call if followed by a bracket
        if (token.Kind == TokenKind.Identifier)
        {
            pos++;
            var name = token.Value;

            // Check if followed by a bracket = function call
            if (pos < tokens.Count && tokens[pos].Kind == TokenKind.BracketOpen)
            {
                pos++; // consume open bracket
                var args = new List<Expr>();
                while (pos < tokens.Count && tokens[pos].Kind != TokenKind.BracketClose)
                {
                    var arg = ParseLogicalOr(tokens, ref pos);
                    if (arg is null) break;
                    args.Add(arg);
                }
                if (pos < tokens.Count && tokens[pos].Kind == TokenKind.BracketClose)
                    pos++;
                return new CallExpr(name, args);
            }

            return new VariableRef(name);
        }

        return null;
    }

    /// <summary>
    /// Parses content inside a formatting bracket. If the bracket contains
    /// an identifier followed by more tokens (not an operator at the same level),
    /// treat it as a function call (juxtaposition = application).
    /// e.g., bold("factorial 10") = factorial(10)
    /// </summary>
    private static Expr? ParseBracketContent(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count || tokens[pos].Kind == TokenKind.BracketClose)
            return null;

        // If first token is an identifier and there are more non-operator
        // tokens before the close bracket, it's a function call
        if (tokens[pos].Kind == TokenKind.Identifier)
        {
            var savedPos = pos;
            var name = tokens[pos].Value;
            pos++;

            // Check if there are argument tokens (not just operators or close bracket)
            if (pos < tokens.Count &&
                tokens[pos].Kind != TokenKind.BracketClose &&
                tokens[pos].Kind != TokenKind.Operator)
            {
                // Parse the rest as the argument expression
                var arg = ParseLogicalOr(tokens, ref pos);
                if (arg is not null)
                    return new CallExpr(name, new List<Expr> { arg });
            }

            // Not a call, restore and parse normally
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
            _ => WordyType.Auto
        };
    }
}
