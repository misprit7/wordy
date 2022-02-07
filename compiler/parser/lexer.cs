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
        while(Char.IsWhiteSpace(curString.Current))
            if(curStringFinished = !curString.MoveNext()) break;

        // If more string to parse
        if(!curStringFinished){
            //Handle identifiers
            if(Char.IsLetter(curString.Current)){
                // First character
                identifierString = char.ToString(curString.Current);
                // Finish rest of identifier
                do {
                    identifierString += curString.Current;
                    if(curStringFinished = !curString.MoveNext()) break;
                } while(Char.IsLetterOrDigit(curString.Current));

                return Token.identifier;
            }
            // Handle numbers
            if(char.IsDigit(curString.Current) || curString.Current == '.'){
                string numStr = "";
                do {
                    numStr += curString.Current;
                    if(curStringFinished = !curString.MoveNext()) break;
                } while(char.IsDigit(curString.Current) || curString.Current == '.');

                numVal = Double.Parse(numStr);
                return Token.number;
            }
            // Other characters, e.g. '+'
            char otherChar = curString.Current;
            curStringFinished = curString.MoveNext();
            // Explicit cast since positive values of token correspond to ascii
            return (Token) otherChar;
        } else if(curElement.ChildElements.Count > 0){
            
        } else {
            
        }
        return (Token) 1;
    }

}

