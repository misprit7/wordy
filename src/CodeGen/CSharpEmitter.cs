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
        var returnType = func.IsEntryPoint ? "void" : TypeToCSharp(func.ReturnType);
        var methodName = func.IsEntryPoint ? "Main" : SanitizeIdentifier(func.Name);
        var accessMod = func.IsEntryPoint ? "public static" : "public static";

        Indent(sb, indent);
        sb.Append($"{accessMod} {returnType} {methodName}(");

        if (func.IsEntryPoint)
        {
            sb.Append("string[] args");
        }
        else
        {
            sb.Append(string.Join(", ", func.Parameters.Select(p =>
                $"{TypeToCSharp(p.Type)} {SanitizeIdentifier(p.Name)}")));
        }

        sb.AppendLine(")");
        Indent(sb, indent);
        sb.AppendLine("{");

        foreach (var stmt in func.Body)
        {
            EmitStatement(sb, stmt, indent + 1);
        }

        Indent(sb, indent);
        sb.AppendLine("}");
    }

    private static void EmitStatement(StringBuilder sb, Stmt stmt, int indent)
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
                sb.Append($"var {SanitizeIdentifier(assign.Variable)} = ");
                EmitExpr(sb, assign.Value);
                sb.AppendLine(";");
                break;

            case DeleteStmt del:
                Indent(sb, indent);
                sb.AppendLine($"// delete {del.Variable} (no-op in C#)");
                break;

            case IfStmt ifStmt:
                EmitIf(sb, ifStmt, indent);
                break;

            case MatchStmt match:
                EmitMatch(sb, match, indent);
                break;

            case ForStmt forStmt:
                EmitFor(sb, forStmt, indent);
                break;

            case ExprStmt expr:
                Indent(sb, indent);
                EmitExpr(sb, expr.Expression);
                sb.AppendLine(";");
                break;
        }
    }

    private static void EmitIf(StringBuilder sb, IfStmt ifStmt, int indent)
    {
        Indent(sb, indent);
        sb.Append("if (");
        EmitExpr(sb, ifStmt.Condition);
        sb.AppendLine(")");
        Indent(sb, indent);
        sb.AppendLine("{");
        foreach (var s in ifStmt.TrueBranch)
            EmitStatement(sb, s, indent + 1);
        Indent(sb, indent);
        sb.AppendLine("}");
        if (ifStmt.FalseBranch.Count > 0)
        {
            Indent(sb, indent);
            sb.AppendLine("else");
            Indent(sb, indent);
            sb.AppendLine("{");
            foreach (var s in ifStmt.FalseBranch)
                EmitStatement(sb, s, indent + 1);
            Indent(sb, indent);
            sb.AppendLine("}");
        }
    }

    private static void EmitMatch(StringBuilder sb, MatchStmt match, int indent)
    {
        Indent(sb, indent);
        sb.Append("switch (");
        EmitExpr(sb, match.Subject);
        sb.AppendLine(")");
        Indent(sb, indent);
        sb.AppendLine("{");

        foreach (var c in match.Cases)
        {
            Indent(sb, indent + 1);
            if (c.Pattern is not null)
            {
                sb.Append("case ");
                EmitExpr(sb, c.Pattern);
                sb.AppendLine(":");
            }
            else
            {
                sb.AppendLine("default:");
            }

            foreach (var s in c.Body)
                EmitStatement(sb, s, indent + 2);

            Indent(sb, indent + 2);
            sb.AppendLine("break;");
        }

        Indent(sb, indent);
        sb.AppendLine("}");
    }

    private static void EmitFor(StringBuilder sb, ForStmt forStmt, int indent)
    {
        Indent(sb, indent);
        sb.Append("for (");
        EmitStatementInline(sb, forStmt.Init);
        sb.Append("; ");
        EmitExpr(sb, forStmt.Condition);
        sb.Append("; ");
        EmitStatementInline(sb, forStmt.Step);
        sb.AppendLine(")");
        Indent(sb, indent);
        sb.AppendLine("{");

        foreach (var s in forStmt.Body)
            EmitStatement(sb, s, indent + 1);

        Indent(sb, indent);
        sb.AppendLine("}");
    }

    private static void EmitStatementInline(StringBuilder sb, Stmt stmt)
    {
        switch (stmt)
        {
            case AssignStmt assign:
                sb.Append($"var {SanitizeIdentifier(assign.Variable)} = ");
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
                // Emit as int if it's a whole number
                if (num.Value == Math.Floor(num.Value) && !double.IsInfinity(num.Value))
                    sb.Append((long)num.Value);
                else
                    sb.Append(num.Value);
                break;
            case StringLiteral str:
                sb.Append($"\"{str.Value.Replace("\"", "\\\"")}\"");
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
            case ExponentExpr exp:
                sb.Append("Math.Pow(");
                EmitExpr(sb, exp.Base);
                sb.Append(", ");
                EmitExpr(sb, exp.Exponent);
                sb.Append(')');
                break;
        }
    }

    private static string BinaryOpToCSharp(BinaryOp op) => op switch
    {
        BinaryOp.Add => "+",
        BinaryOp.Subtract => "-",
        BinaryOp.Multiply => "*",
        BinaryOp.Divide => "/",
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
        WordyType.Error => "Exception",
        WordyType.Auto => "dynamic",
        _ => "dynamic"
    };

    private static string SanitizeIdentifier(string name)
    {
        var sanitized = new StringBuilder();
        bool capitalizeNext = true; // PascalCase
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
