namespace Wordy.Reader;

public record DocumentIR(List<DocumentElement> Elements, List<ImportInfo> Imports);

public record ImportInfo(string Tag, string FileName);

public abstract record DocumentElement;

public record ParagraphElement(
    string? Style,
    string? Alignment,  // "left", "right", "center", "both", or null
    bool IsDropCap,
    List<RunElement> Runs
) : DocumentElement;

public record RunElement(
    string Text,
    bool Bold,
    bool Italic,
    bool Strikethrough,
    bool Superscript,
    bool Subscript,
    string? FontName,
    double? FontSize,
    string? HighlightColor,
    string? Underline,
    bool SmallCaps
);

public record TableElement(
    List<TableRowElement> Rows
) : DocumentElement;

public record TableRowElement(
    List<TableCellElement> Cells
);

public record TableCellElement(
    int GridSpan,
    List<DocumentElement> Content
);

public record ListElement(
    List<ListItemElement> Items
) : DocumentElement;

public record ListItemElement(
    int IndentLevel,
    List<RunElement> Runs
);
