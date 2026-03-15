using Wordy.Reader;

namespace Wordy.Debug;

public static class DumpIR
{
    public static void Dump(DocumentIR doc, int indent = 0)
    {
        foreach (var element in doc.Elements)
        {
            DumpElement(element, indent);
        }
    }

    public static void DumpElement(DocumentElement element, int indent)
    {
        var prefix = new string(' ', indent * 2);
        switch (element)
        {
            case ParagraphElement para:
                Console.WriteLine($"{prefix}Paragraph [style={para.Style}, align={para.Alignment}, dropCap={para.IsDropCap}]");
                foreach (var run in para.Runs)
                {
                    Console.WriteLine($"{prefix}  Run: \"{run.Text}\" [font={run.FontName}, size={run.FontSize}, bold={run.Bold}, italic={run.Italic}, strike={run.Strikethrough}, super={run.Superscript}, sub={run.Subscript}, highlight={run.HighlightColor}, smallcaps={run.SmallCaps}]");
                }
                break;
            case TableElement table:
                Console.WriteLine($"{prefix}Table [{table.Rows.Count} rows]");
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    Console.WriteLine($"{prefix}  Row {r} [{row.Cells.Count} cells]");
                    for (int c = 0; c < row.Cells.Count; c++)
                    {
                        var cell = row.Cells[c];
                        Console.WriteLine($"{prefix}    Cell {c} [gridSpan={cell.GridSpan}]");
                        foreach (var child in cell.Content)
                        {
                            DumpElement(child, indent + 3);
                        }
                    }
                }
                break;
            case ListElement list:
                Console.WriteLine($"{prefix}List [{list.Items.Count} items]");
                for (int i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    Console.WriteLine($"{prefix}  Item {i} [indent={item.IndentLevel}]");
                    foreach (var run in item.Runs)
                    {
                        Console.WriteLine($"{prefix}    Run: \"{run.Text}\" [font={run.FontName}, bold={run.Bold}, italic={run.Italic}, sub={run.Subscript}]");
                    }
                }
                break;
        }
    }
}
