using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

public enum Token {
    eof = -1, 
    external = -3, 
    identifier = -4, 
    number = -5
}

public class Lexer {
    WordprocessingDocument doc;
    Body body;

    private DocumentFormat.OpenXml.OpenXmlElement curElement;
    private CharEnumerator curString = "".GetEnumerator();
    private bool curStringFinished = false;
    private char lastChar = ' ';

    private string identifierString = "";
    private double numVal = 0;

    public Lexer(String docname){
        doc = WordprocessingDocument.Open(docname, false);
        body = doc?.MainDocumentPart?.Document?.Body!;
        if(body is null){
            Console.Error.Write("Document contains no body!");
            return;
        }
        curElement = body;
    }

    public void PrintDoc(){
        foreach(var b in body.Elements()){
            Console.WriteLine(b.InnerText);
        }
    }

    public void BodyElements(){
        Console.WriteLine(body.Elements());
    }

    public Token GetToken(){
        // Get rid of whitespace
        while(Char.IsWhiteSpace(lastChar))
            lastChar = getChar();

        //Handle identifiers
        if(Char.IsLetter(lastChar)){
            // Finish rest of identifier
            do {
                identifierString += lastChar;
                lastChar = getChar();
            } while(Char.IsLetterOrDigit(lastChar));

            return Token.identifier;
        }
        // Handle numbers
        if(char.IsDigit(lastChar) || lastChar == '.'){
            string numStr = "";
            do {
                numStr += lastChar;
                lastChar = getChar();
            } while(char.IsDigit(lastChar) || lastChar == '.');

            numVal = Double.Parse(numStr);
            return Token.number;
        }
        // Other characters, e.g. '+'
        char otherChar = lastChar;
        lastChar = getChar();
        // Explicit cast since positive values of token correspond to ascii
        return (Token) otherChar;
    }

    private char getChar(){
        if(!curStringFinished){
            curString.MoveNext();
            return curString.Current;
        }


        return curString.Current;
    }

}

