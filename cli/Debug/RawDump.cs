using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Wordy.Debug;

public static class RawDump
{
    public static void Dump(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart!.Document!.Body!;

        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph p)
            {
                var framePr = p.ParagraphProperties?.GetFirstChild<FrameProperties>();
                var dropCapRaw = framePr?.DropCap?.InnerText;
                var justRaw = p.ParagraphProperties?.Justification?.Val?.InnerText;
                Console.WriteLine($"Paragraph: dropCap={dropCapRaw ?? "none"}, justification={justRaw ?? "none"}");
                foreach (var run in p.Elements<Run>())
                {
                    var rp = run.RunProperties;
                    var glow = rp?.GetFirstChild<DocumentFormat.OpenXml.Office2010.Word.Glow>();
                    Console.WriteLine($"  Run: \"{run.InnerText}\" glow={glow != null}");
                }
            }
            else if (element is Table t)
            {
                Console.WriteLine("Table:");
                foreach (var row in t.Elements<TableRow>())
                {
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        foreach (var cp in cell.Elements<Paragraph>())
                        {
                            var jRaw = cp.ParagraphProperties?.Justification?.Val?.InnerText;
                            Console.WriteLine($"  Cell para: \"{cp.InnerText}\" justification={jRaw ?? "none"}");
                        }
                    }
                }
            }
        }
    }
}
