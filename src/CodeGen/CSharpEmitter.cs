using System.Text;
using Wordy.Ast;

namespace Wordy.CodeGen;

public static class CSharpEmitter
{
    public static string Emit(Wordy.Ast.Program program)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("public static class WordyProgram");
        sb.AppendLine("{");

        foreach (var func in program.Functions)
        {
            EmitFunction(sb, func, indent: 1);
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitFunction(StringBuilder sb, Function func, int indent)
    {
        var returnType = TypeToCSharp(func.ReturnType);
        var methodName = func.IsEntryPoint ? "Main" : SanitizeIdentifier(func.Name);

        Indent(sb, indent);
        sb.Append($"public static {returnType} {methodName}(");

        // Track declared variables (parameters are pre-declared)
        var declared = new HashSet<string>();

        if (func.IsEntryPoint)
        {
            sb.Append("string[] args");
        }
        else
        {
            sb.Append(string.Join(", ", func.Parameters.Select(p =>
                $"{TypeToCSharp(p.Type)} {SanitizeIdentifier(p.Name)}")));
            foreach (var p in func.Parameters)
                declared.Add(p.Name);
        }

        sb.AppendLine(")");
        Indent(sb, indent);
        sb.AppendLine("{");

        foreach (var stmt in func.Body)
            EmitStatement(sb, stmt, indent + 1, declared);

        Indent(sb, indent);
        sb.AppendLine("}");
    }

    private static void EmitStatement(StringBuilder sb, Stmt stmt, int indent, HashSet<string> declared)
    {
        switch (stmt)
        {
            case ReturnStmt ret:
                Indent(sb, indent);
                sb.Append("return ");
                EmitExpr(sb, ret.Value);
                sb.AppendLine(";");
                break;

            case PrintStmt print:
                Indent(sb, indent);
                sb.Append("Console.WriteLine(");
                EmitExpr(sb, print.Value);
                sb.AppendLine(");");
                break;

            case AssignStmt assign:
                Indent(sb, indent);
                var varName = SanitizeIdentifier(assign.Variable);
                if (declared.Add(assign.Variable))
                    sb.Append($"var {varName} = ");
                else
                    sb.Append($"{varName} = ");
                EmitExpr(sb, assign.Value);
                sb.AppendLine(";");
                break;

            case IfStmt ifStmt:
                EmitIf(sb, ifStmt, indent, declared);
                break;

            case MatchStmt match:
                EmitMatch(sb, match, indent, declared);
                break;

            case ForStmt forStmt:
                EmitFor(sb, forStmt, indent, declared);
                break;

            case ExprStmt expr:
                Indent(sb, indent);
                EmitExpr(sb, expr.Expression);
                sb.AppendLine(";");
                break;
        }
    }

    private static void EmitIf(StringBuilder sb, IfStmt ifStmt, int indent, HashSet<string> declared)
    {
        Indent(sb, indent);
        sb.Append("if (");
        EmitExpr(sb, ifStmt.Condition);
        sb.AppendLine(")");
        Indent(sb, indent);
        sb.AppendLine("{");
        foreach (var s in ifStmt.TrueBranch)
            EmitStatement(sb, s, indent + 1, declared);
        Indent(sb, indent);
        sb.AppendLine("}");
        if (ifStmt.FalseBranch.Count > 0)
        {
            Indent(sb, indent);
            sb.AppendLine("else");
            Indent(sb, indent);
            sb.AppendLine("{");
            foreach (var s in ifStmt.FalseBranch)
                EmitStatement(sb, s, indent + 1, declared);
            Indent(sb, indent);
            sb.AppendLine("}");
        }
    }

    private static void EmitMatch(StringBuilder sb, MatchStmt match, int indent, HashSet<string> declared)
    {
        Indent(sb, indent);
        sb.Append("switch (");
        EmitExpr(sb, match.Subject);
        sb.AppendLine(")");
        Indent(sb, indent);
        sb.AppendLine("{");

        foreach (var c in match.Cases)
        {
            if (c.Patterns.Count > 0)
            {
                foreach (var pattern in c.Patterns)
                {
                    Indent(sb, indent + 1);
                    sb.Append("case ");
                    EmitExpr(sb, pattern);
                    sb.AppendLine(":");
                }
            }
            else
            {
                Indent(sb, indent + 1);
                sb.AppendLine("default:");
            }

            foreach (var s in c.Body)
                EmitStatement(sb, s, indent + 2, declared);

            // Only emit break if the body doesn't end with a return
            if (!c.Body.Any(s => s is ReturnStmt))
            {
                Indent(sb, indent + 2);
                sb.AppendLine("break;");
            }
        }

        Indent(sb, indent);
        sb.AppendLine("}");
    }

    private static void EmitFor(StringBuilder sb, ForStmt forStmt, int indent, HashSet<string> declared)
    {
        // If the init declares a new variable, hoist it before the for loop
        // so it remains accessible after the loop (Wordy has no block scoping)
        if (forStmt.Init is AssignStmt initAssign && !declared.Contains(initAssign.Variable))
        {
            EmitStatement(sb, initAssign, indent, declared);
            Indent(sb, indent);
            sb.Append("for (; ");
        }
        else
        {
            Indent(sb, indent);
            sb.Append("for (");
            EmitStatementInline(sb, forStmt.Init, declared);
            sb.Append("; ");
        }

        EmitExpr(sb, forStmt.Condition);
        sb.Append("; ");
        EmitStatementInline(sb, forStmt.Step, declared);
        sb.AppendLine(")");
        Indent(sb, indent);
        sb.AppendLine("{");

        foreach (var s in forStmt.Body)
            EmitStatement(sb, s, indent + 1, declared);

        Indent(sb, indent);
        sb.AppendLine("}");
    }

    private static void EmitStatementInline(StringBuilder sb, Stmt stmt, HashSet<string> declared)
    {
        switch (stmt)
        {
            case AssignStmt assign:
                var varName = SanitizeIdentifier(assign.Variable);
                if (declared.Add(assign.Variable))
                    sb.Append($"var {varName} = ");
                else
                    sb.Append($"{varName} = ");
                EmitExpr(sb, assign.Value);
                break;
            case ExprStmt expr:
                EmitExpr(sb, expr.Expression);
                break;
            default:
                sb.Append("/* TODO */");
                break;
        }
    }

    private static void EmitExpr(StringBuilder sb, Expr expr)
    {
        switch (expr)
        {
            case NumberLiteral num:
                if (num.Value == Math.Floor(num.Value) && !double.IsInfinity(num.Value))
                    sb.Append((long)num.Value);
                else
                    sb.Append(num.Value);
                break;
            case StringLiteral str:
                sb.Append($"\"{str.Value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"");
                break;
            case BoolLiteral b:
                sb.Append(b.Value ? "true" : "false");
                break;
            case VariableRef v:
                sb.Append(SanitizeIdentifier(v.Name));
                break;
            case BinaryExpr bin:
                sb.Append('(');
                EmitExpr(sb, bin.Left);
                sb.Append($" {BinaryOpToCSharp(bin.Op)} ");
                EmitExpr(sb, bin.Right);
                sb.Append(')');
                break;
            case UnaryExpr un:
                sb.Append(un.Op == UnaryOp.Negate ? "-" : "!");
                EmitExpr(sb, un.Operand);
                break;
            case CallExpr call:
                sb.Append($"{SanitizeIdentifier(call.FunctionName)}(");
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    EmitExpr(sb, call.Arguments[i]);
                }
                sb.Append(')');
                break;
            case ArrayAccessExpr acc:
                EmitExpr(sb, acc.Array);
                sb.Append('[');
                EmitExpr(sb, acc.Index);
                sb.Append(']');
                break;
            case MultiDimArrayAccessExpr macc:
                EmitExpr(sb, macc.Array);
                sb.Append('[');
                for (int i = 0; i < macc.Indices.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    EmitExpr(sb, macc.Indices[i]);
                }
                sb.Append(']');
                break;
            case ArrayLiteralExpr arr:
                sb.Append($"new {TypeToCSharp(arr.ElementType)}[] {{");
                for (int i = 0; i < arr.Elements.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    // Char arrays: emit single-char string literals as char literals
                    if (arr.ElementType == WordyType.Char && arr.Elements[i] is StringLiteral sl && sl.Value.Length == 1)
                        sb.Append($"'{sl.Value}'");
                    else
                        EmitExpr(sb, arr.Elements[i]);
                }
                sb.Append('}');
                break;
            case ArrayLiteral2DExpr arr2d:
                sb.Append($"new {TypeToCSharp(arr2d.ElementType)}[,] {{");
                for (int r = 0; r < arr2d.Rows.Count; r++)
                {
                    if (r > 0) sb.Append(", ");
                    sb.Append('{');
                    for (int c = 0; c < arr2d.Rows[r].Count; c++)
                    {
                        if (c > 0) sb.Append(", ");
                        EmitExpr(sb, arr2d.Rows[r][c]);
                    }
                    sb.Append('}');
                }
                sb.Append('}');
                break;
            case ExponentExpr exp:
                sb.Append("(int)Math.Pow(");
                EmitExpr(sb, exp.Base);
                sb.Append(", ");
                EmitExpr(sb, exp.Exponent);
                sb.Append(')');
                break;
            case CastExpr cast:
                EmitCast(sb, cast);
                break;
            case ScanExpr:
                sb.Append("(char)Console.Read()");
                break;
        }
    }

    private static void EmitCast(StringBuilder sb, CastExpr cast)
    {
        switch (cast.TargetType)
        {
            case WordyType.String:
                // Cast to string = .ToString()
                EmitExpr(sb, cast.Value);
                sb.Append(".ToString()");
                break;
            case WordyType.Int:
                sb.Append("((int)(");
                EmitExpr(sb, cast.Value);
                sb.Append("))");
                break;
            case WordyType.Float:
                sb.Append("((double)(");
                EmitExpr(sb, cast.Value);
                sb.Append("))");
                break;
            case WordyType.Bool:
                sb.Append("Convert.ToBoolean(");
                EmitExpr(sb, cast.Value);
                sb.Append(')');
                break;
            case WordyType.Char:
                sb.Append("Convert.ToChar(");
                EmitExpr(sb, cast.Value);
                sb.Append(')');
                break;
            default:
                EmitExpr(sb, cast.Value);
                break;
        }
    }

    private static string BinaryOpToCSharp(BinaryOp op) => op switch
    {
        BinaryOp.Add => "+",
        BinaryOp.Subtract => "-",
        BinaryOp.Multiply => "*",
        BinaryOp.Divide => "/",
        BinaryOp.Modulo => "%",
        BinaryOp.Equal => "==",
        BinaryOp.NotEqual => "!=",
        BinaryOp.LessThan => "<",
        BinaryOp.GreaterThan => ">",
        BinaryOp.LessThanOrEqual => "<=",
        BinaryOp.GreaterThanOrEqual => ">=",
        BinaryOp.LogicalAnd => "&&",
        BinaryOp.LogicalOr => "||",
        _ => "/* unknown */"
    };

    private static string TypeToCSharp(WordyType type) => type switch
    {
        WordyType.Int => "int",
        WordyType.Float => "double",
        WordyType.String => "string",
        WordyType.Bool => "bool",
        WordyType.Char => "char",
        WordyType.Void => "void",
        _ => "void"
    };

    private static string SanitizeIdentifier(string name)
    {
        var sanitized = new StringBuilder();
        bool capitalizeNext = true;
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sanitized.Append(capitalizeNext ? char.ToUpper(c) : c);
                capitalizeNext = false;
            }
            else
            {
                sanitized.Append('_');
                capitalizeNext = true;
            }
        }

        var result = sanitized.ToString();
        if (result.Length == 0 || char.IsDigit(result[0]))
            result = "_" + result;

        return result;
    }

    private static void Indent(StringBuilder sb, int level)
    {
        sb.Append(new string(' ', level * 4));
    }
}
