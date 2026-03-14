using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

public enum TokenEnum {
    eof = -1, 
    external = -3, 
    identifier = -4, 
    number = -5
}

public class Compiler {

    WordprocessingDocument doc;
    Body body;

    private DocumentFormat.OpenXml.OpenXmlElement curElement;

    public Compiler(String docname){
        doc = WordprocessingDocument.Open(docname, false);
        body = doc?.MainDocumentPart?.Document?.Body!;
        if(body is null){
            Console.Error.Write("Document contains no body!");
            return;
        }
        curElement = body.FirstChild;
    }


    /**
     * Assumes that curElement is top level header
     * returns null if no function can be parsed
     */
    public FunctionAST parseFunction(){
        Paragraph name = curElement.NextSibling<Paragraph>();
        if(name is null) return null;
        OpenXmlElement next = name.NextSibling();
        if(next is null) return null;
        Paragraph args = null;
        if(next is Paragraph){
            args = name.NextSibling<Paragraph>();
            next = args.NextSibling();
        } else next = next.NextSibling();
        if(!(next is Table)) return null;
        curElement = next;
        return new FunctionAST(name, args, (Table)next);
    }

    public void PrintDoc(){
        /* foreach(var b in body.Elements()){ */
        /*     Console.WriteLine(b.InnerText); */
        /* } */
        /* char c = getChar(); */
        /* while(c != EOF){ */
        /*     c = getChar(); */
        /*     Console.Write(c); */
        /* } */
    }

    public void BodyElements(){
        Console.WriteLine(body.Elements());
    }

}

