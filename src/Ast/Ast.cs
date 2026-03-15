namespace Wordy.Ast;

// --- Types ---

public enum WordyType
{
    Int,       // Courier New
    Float,     // Script/cursive
    String,    // Times New Roman
    Bool,      // Comic Sans
    Char,      // Symbol
    Void,      // Wingdings
    Error,     // Impact
    Auto       // Calibri (inferred)
}

// --- Expressions ---

public abstract record Expr;

public record NumberLiteral(double Value) : Expr;

public record StringLiteral(string Value) : Expr;

public record BoolLiteral(bool Value) : Expr;

public record VariableRef(string Name) : Expr;

public record BinaryExpr(Expr Left, BinaryOp Op, Expr Right) : Expr;

public record UnaryExpr(UnaryOp Op, Expr Operand) : Expr;

public record CallExpr(string FunctionName, List<Expr> Arguments) : Expr;

public record ArrayAccessExpr(Expr Array, Expr Index) : Expr;

public record ExponentExpr(Expr Base, Expr Exponent) : Expr;

public record CastExpr(Expr Value, WordyType TargetType) : Expr;

public enum BinaryOp
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
    LogicalAnd,
    LogicalOr
}

public enum UnaryOp
{
    Negate
}

// --- Statements ---

public abstract record Stmt;

public record AssignStmt(string Variable, Expr Value) : Stmt;

public record ReturnStmt(Expr Value) : Stmt;

public record PrintStmt(Expr Value) : Stmt;

public record IfStmt(
    Expr Condition,
    List<Stmt> TrueBranch,
    List<Stmt> FalseBranch
) : Stmt;

public record MatchStmt(
    Expr Subject,
    List<MatchCase> Cases
) : Stmt;

public record MatchCase(List<Expr> Patterns, List<Stmt> Body);

public record ForStmt(
    Stmt Init,
    Expr Condition,
    Stmt Step,
    List<Stmt> Body
) : Stmt;

public record ExprStmt(Expr Expression) : Stmt;

// --- Top-level ---

public record Parameter(string Name, WordyType Type);

public record Function(
    string Name,
    List<Parameter> Parameters,
    WordyType ReturnType,
    List<Stmt> Body,
    bool IsEntryPoint
);

public record Program(List<Function> Functions);
