# Wordy Language Specification (Draft)

Wordy is an esoteric programming language that uses Microsoft Word `.docx` documents as source code. Every piece of syntax is represented through document formatting — there are no keywords.

## General Rules

- **Whitespace insensitive**: newlines, spaces, and indentation have no semantic meaning.
- **Case insensitive**: `Foo`, `foo`, and `FOO` are the same identifier.
- **No keywords**: all syntax is communicated through formatting, not text content.
- **No inline comments**: comments are done exclusively through Word's comment feature (Insert > Comment).

## Functions

- **Declaration**: a **heading** (Heading 1, Heading 2, etc.) defines a function. The heading text is the function name.
- **Parameters**: defined in a **subtitle** paragraph immediately following the heading.
- **Entry point**: the function whose heading uses a **drop cap** is the program's entry point (equivalent to `main`).
- **Visibility**: functions listed in the **Table of Contents** are public/exported. Functions not in the ToC are private to the module. (Heading level could also play a role here — Heading 1 = public, Heading 2+ = private — TBD.)

## Variables

- **Declaration**: the first occurrence of a word is its declaration. No explicit declaration syntax.
- **Assignment**: uses **tab stops**. The value is on the left of the tab stop, the variable name is on the right. (TBD: or vice versa.)
- **Immutability by default**: variables are immutable. To reassign, the reassignment must be written with **Track Changes** enabled, so the old value appears as deleted and the new value as inserted. Mutation history is visible in the document.
- **Constants**: **small caps** formatting marks a variable as a compile-time constant.

## Types

Types are determined by **font family**:

| Font | Type | Rationale |
|------|------|-----------|
| Times New Roman | string | The literary font — it's for text. |
| Courier New | int | Monospaced, precise, rigid — counting energy. |
| Comic Sans | bool | You either love it or hate it. True or false. |
| Script/cursive (e.g. Brush Script) | float | Cursive is fluid and imprecise, like floating point. |
| Wingdings | void | The text is unreadable — it represents nothing. |
| Symbol | char | Named "Symbol" — it's for individual symbols. |
| Calibri | auto (inferred) | Word's default font. No formatting = compiler infers the type. |
| Impact | error/exception | It's called Impact. Errors hit hard. |

Changing the font of a value or variable changes its type.

## Numeric Literals

The **font size** of the text *is* the numeric value. Text at size 42 represents the number 42. The actual text content of numeric literals is ignored (or could be required to match — TBD).

## Arithmetic

- Addition: `+`
- Subtraction: `-`
- Multiplication: `×` (the actual times symbol, not `*`)
- Division: `÷` (the actual division symbol, not `/`)
- Exponentiation: **superscript** formatting (e.g., x with a superscripted 2 = x²)
- Array access: **subscript** formatting (e.g., arr with a subscripted i = arr[i])

## Brackets / Grouping

Brackets are represented by **formatting nesting**. Applying a formatting style opens a bracket; ending that formatting closes it. For example:

- **Bold** text opens a bracket. Ending bold closes it.
- Within that bold region, a **highlight color** opens a nested bracket.
- Within that, **italic** starts another nesting level.

The ordering of formatting types for nesting is user-defined (not fixed). However, **undoing an outer formatting inside an inner one is invalid syntax**. For example, if bold is the outer bracket and highlight is the inner bracket, removing bold while still inside the highlighted region is a syntax error.

## Control Flow

All control flow uses **tables**.

### Match Statements

A table with **one merged top cell** is a match statement:
- The **merged top cell** contains the expression to match on.
- The row below is split into N cells, one per case.
- The **top of each case cell** contains the value to match against.
- The **rest of each case cell** contains the body to execute.

### If Statements (Syntax Sugar)

An if statement is a match statement where the matched expression has type **bool**. As sugar:
- The two case cells do not need to explicitly contain `true` and `false`.
- The **left cell** is the true branch, the **right cell** is the false branch.

### For Loops

A table with **three merged top cells** is a for loop (C-style):
- **Left top cell**: loop variable declaration / initialization.
- **Middle top cell**: loop condition.
- **Right top cell**: increment / step expression.
- The **row below** (spanning the full width) is the loop body.

## Functions Calls

**Hyperlinks**: the URL/target of the hyperlink is the function name, the display text contains the arguments.

## Return Values

**Right-aligned** text is a return value. The function returns that expression to its caller.

**Page breaks** exit the current function (early return). A right-aligned expression followed by a page break returns that value and exits.

## Output / Print

**Glow text effect**: text with a glow effect is printed to stdout.

## Error Handling

**Footnotes**: a footnote reference in the main text marks a "try" site. The corresponding footnote at the bottom of the page is the error handler for that site. If the expression at the footnote reference throws, execution jumps to the footnote body.

## Deferred Execution / Cleanup

**Endnotes**: endnote bodies execute when the current function exits, regardless of how it exits (similar to `defer` in Go or `finally`). They run in reverse order of their appearance.

## Memory Management

**Strikethrough**: applying strikethrough to a variable name deallocates / deletes it. Visually, you are crossing it out.

## Copy / Clone

**Shadow text effect**: text with a shadow creates a copy/clone of the value.

## Assertions

**Center-aligned** text is an assertion. The expression must evaluate to true or the program panics.

## Imports

Defined in the document's **bibliography / citations**. Each bibliography entry maps to a module import. Citing a source in the text brings that module's exports into scope at that point.

## Pointers and References

**Cross-references**: a cross-reference to a bookmark is a pointer/reference to that variable. Following the cross-reference = dereferencing.

**Bookmarks**: define named locations that can be referenced (and jumped to with goto — TBD whether goto should exist).

## Generics / Templates

**Mail merge fields**: a mail merge placeholder (`«T»`) is a generic type parameter. It gets filled in with a concrete type at the call site, just like mail merge fills in names.

## Sleep / Delay

**Line spacing**: the line spacing value of a paragraph determines execution delay. Single-spaced = normal. Double-spaced = 2x delay. This is intentionally useless.

## Data Structures

**Lists** (bulleted or numbered) define data structures / records. Details TBD for later implementation.

## Compilation

Wordy transpiles to **C#**. The compiler is written in C# using the **OpenXML SDK** to parse `.docx` files. Long-term goal: self-hosting (the compiler is written in Wordy itself).

---

## Open Questions

- Full font-to-type mapping
- Whether numeric literal text content must match the font size or is ignored
- Exact tab stop assignment semantics (value→name or name→value)
- Heading level vs. ToC for visibility — pick one or use both
- Whether bookmarks should support goto or only exist for cross-references
- Equation editor for complex math (deferred — implementation complexity)
- Concurrency via columns (explicitly not implementing)
- Details of list-based data structure definitions
