using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

/******************************************************************************
 * Tokens
 ******************************************************************************/


public abstract class Token {
    
}

public class EOFToken : Token {

}

public class ExternalToken : Token {

}

public class IdentifierToken : Token {
    public string identifier;

    public IdentifierToken(string Identifier){
        identifier = Identifier;
    }
}

public class NumberToken : Token {
    public double number;

    public NumberToken(double Number){
        number = Number;
    }
}

public class AsciiToken : Token {
    public char c;

    public AsciiToken(char C){
        c = C;
    }
}

/******************************************************************************
 * Parser
 ******************************************************************************/

public class Parser {
    static char EOF = (char)0x1A;
    static int[] binopPrec = new int[256];

    CharEnumerator curString = "".GetEnumerator();
    Token curTok = null;

    public Parser(){
        binopPrec['<'] = 10;
        binopPrec['+'] = 20;
        binopPrec['-'] = 30;
        binopPrec['*'] = 40; // highest priority
    }

    private int getOpPrec(){
        if(!(curTok is AsciiToken)) return -1;
        int tokPrec = binopPrec[(curTok as AsciiToken).c];
        return tokPrec <= 0 ? -1 : tokPrec;
    }

    /**
     * Generic error thrower
     */
    private ExprAST parseError(){
        Console.Error.WriteLine("Error parsing document!");
        return null;
    }

    private char getChar(){
        bool hasNext = curString.MoveNext();
        // Try current string
        if(hasNext){ 
            return curString.Current;
        } else return EOF;
    }

    private Token getToken(){
        char lastChar = curString.Current;
        // Get rid of whitespace
        while(Char.IsWhiteSpace(lastChar))
            lastChar = getChar();

        //Handle identifiers
        if(Char.IsLetter(lastChar)){
            // Finish rest of identifier
            string identifierString = "";
            do {
                identifierString += lastChar;
                lastChar = getChar();
            } while(Char.IsLetterOrDigit(lastChar));

            
            return new IdentifierToken(identifierString);
        }
        // Handle numbers
        if(char.IsDigit(lastChar) || lastChar == '.'){
            string numStr = "";
            do {
                numStr += lastChar;
                lastChar = getChar();
            } while(char.IsDigit(lastChar) || lastChar == '.');

            return new NumberToken(Double.Parse(numStr));
        }
        // Other characters, e.g. '+'
        char otherChar = lastChar;
        lastChar = getChar();
        return curTok = new AsciiToken(lastChar);
    }
    
    private ExprAST parseNumberExpr(){
        var ret = new NumberExprAST(((NumberToken)curTok).number);
        getToken();
        return ret;
    }

    private ExprAST parseParenExpr(){
        getToken();
        var V = parseExpression();
        if(V is null) return null;
        if(!(curTok is AsciiToken) || ((AsciiToken)curTok).c != ')')
            return parseError();
        getToken();
        return V;
    }

    private ExprAST parseIdentifierExpr(){
        string idName = (curTok as IdentifierToken)?.identifier;
        getToken();

        if(!(curTok is AsciiToken) || (curTok as AsciiToken)?.c != '(')
            return new VariableExprAST(idName);

        getToken();
        List<ExprAST> args = new List<ExprAST>();
        if((curTok as AsciiToken)?.c != ')'){
            while(true){
                var arg = parseExpression();
                if(!(arg is null)){
                    args.Add(arg);
                } else {
                    return null;
                }
                if((curTok as AsciiToken)?.c == ')') break;
                if((curTok as AsciiToken)?.c != ',') return parseError();
            }
        }
        getToken();
        return new CallExprAST(idName, args);
    }

    private ExprAST parsePrimary(){
        switch(curTok)
        {
        case IdentifierToken tok:
            return parseIdentifierExpr();
        case NumberToken tok:
            return parseNumberExpr();
        case AsciiToken tok:
            if(tok.c == '(')
                return parseParenExpr();
            else return parseError();
        default:
            return parseError();
        }
    }

    private ExprAST parseExpression(){
        var LHS = parsePrimary();
        if(LHS is null) return null;
        return null;// parseBinOpRHS(0, LHS);
    }



    public ExprAST parseFunctionBody(Table table){
        TableRow row = table.Elements<TableRow>().First();
        TableCell col = table.Elements<TableCell>().First();
        Paragraph p = col.Elements<Paragraph>().First();
        curString = p.InnerText.GetEnumerator();
        getChar();
        return parseExpression();
    }
}

/******************************************************************************
 * Expression ASTs
 ******************************************************************************/

public abstract class ExprAST {
    public static bool DEBUG = true;
}

public class NumberExprAST : ExprAST {
    public double val;

    public NumberExprAST(double Val) {
        val=Val;
        if(DEBUG) Console.WriteLine("New number expression!");
    }
}

public class VariableExprAST : ExprAST {
    public string name { get; }

    public VariableExprAST(string Name) {
        name = Name;
        if(DEBUG) Console.WriteLine("New Variable expression!");
    }
}

public class BinaryExprAST : ExprAST {
    public char op;
    public ExprAST lhs, rhs;

    public BinaryExprAST(char Op, ExprAST LHS, ExprAST RHS){
        op = Op;
        lhs = LHS;
        rhs = RHS;
        if(DEBUG) Console.WriteLine("New Binary expression!");
    }
}

public class CallExprAST : ExprAST {
    public string callee;
    public List<ExprAST> args;

    public CallExprAST(string Callee, List<ExprAST> Args){
        callee = Callee;
        args = Args;
        if(DEBUG) Console.WriteLine("New Call expression!");
    }
}

/******************************************************************************
 * Function ASTs
 ******************************************************************************/

public class PrototypeAST {
    public string name;
    public List<string> args;

    public PrototypeAST(string Name, List<string> Args){
        name = Name;
        args = Args;
    }

    public PrototypeAST(Paragraph Name, Paragraph Args){
        name = Name.Descendants<Run>().First().InnerText;
        args = new List<string>();
        foreach(Run r in Args.Descendants<Run>()){
            foreach(string s in r.InnerText.Split(',')){
                args.Add(s.Trim());
            }
        }
        Console.WriteLine(name);
        foreach(var a in args){
            Console.WriteLine(a);
        }
    }
}

public class FunctionAST {
    public PrototypeAST proto;
    public ExprAST body;

    public FunctionAST(PrototypeAST Proto, ExprAST Body){
        proto = Proto;
        body = Body;
    }

    public FunctionAST(Paragraph name, Paragraph args, Table body){
        proto = new PrototypeAST(name, args);

        body = null;
    }
}




