using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

public enum Token {
    eof = -1, 
    external = -3, 
    identifier = -4, 
    number = -5
}

public class Lexer {

    static char EOF = (char)0x1A;

    WordprocessingDocument doc;
    Body body;

    private DocumentFormat.OpenXml.OpenXmlElement curElement;
    private CharEnumerator curString = "".GetEnumerator();
    /* private bool curStringFinished = false; */
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
        /* foreach(var b in body.Elements()){ */
        /*     Console.WriteLine(b.InnerText); */
        /* } */
        char c = getChar();
        while(c != EOF){
            c = getChar();
            Console.Write(c);
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
        bool hasNext = curString.MoveNext();
        // Try current string
        if(hasNext){ 
            return curString.Current;
        }

        bool skipChildren = false;
        do {
            DocumentFormat.OpenXml.OpenXmlElement? next;
            
            // First try children, unless we're already at a run in which case we're at the bottom
            if(!(curElement is Run) && !skipChildren){
                next = curElement.FirstChild;
                /* Console.WriteLine("Trying child"); */
            } else{
                next = null;
            }

            // Next try sibling
            if(next == null){
                next = curElement.NextSibling();
                /* Console.WriteLine("Trying sibling"); */
            }

            // Try parent
            if(next == null){
                next = curElement.Parent;
                /* Console.WriteLine("Trying parent"); */
                // Ensure we don't loop between parent and children
                skipChildren = true;
            }else {
                skipChildren = false;
            }

            // Finally if nothing works we're at the end of the file
            if(next == null){
                return EOF;
            }
            curElement = next;
        } while(!(curElement is Run) || curElement.InnerText.Length == 0);

        curString = curElement.InnerText.GetEnumerator();
        curString.MoveNext();
        /* Console.WriteLine("Current Text:" + curElement.InnerText+"Length:" + curElement.InnerText.Length); */

        return curString.Current;
    }

}

