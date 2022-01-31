/*****************************************************************************/
// Expression ASTs
/*****************************************************************************/

public class ExprAST {
    
}

public class NumberExprAST : ExprAST {
    public double val;

    public NumberExprAST(double Val) {val=Val;}
}

public class VariableExprAST : ExprAST {
    public string name { get; }

    public VariableExprAST(string Name) { name = Name; }
}

public class BinaryExprAST : ExprAST {
    public char op;
    public ExprAST lhs, rhs;

    public BinaryExprAST(char Op, ExprAST LHS, ExprAST RHS){
        op = Op;
        lhs = LHS;
        rhs = RHS;
    }
}

public class CallExprAST : ExprAST {
    public string callee;
    public List<ExprAST> args;

    public CallExprAST(string Callee, List<ExprAST> Args){
        callee = Callee;
        args = Args;
    }
}

/*****************************************************************************/
// Function ASTs
/*****************************************************************************/

public class PrototypeAST {
    public string name;
    public List<string> args;

    public PrototypeAST(string Name, List<string> Args){
        name = Name;
        args = Args;
    }
}

public class FunctionAST {
    public PrototypeAST proto;
    public ExprAST body;

    public FunctionAST(PrototypeAST Proto, ExprAST Body){
        proto = Proto;
        body = Body;
    }
}




