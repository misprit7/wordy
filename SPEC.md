# Wordy Language Specification (Draft)

Wordy is an esoteric programming language that uses Microsoft Word `.docx` documents as source code. Every piece of syntax is represented through document formatting — there are no keywords.

## General Rules

- **Whitespace insensitive**: newlines, spaces, and indentation have no semantic meaning.
- **Case insensitive**: `Foo`, `foo`, and `FOO` are the same identifier.
- **No keywords**: all syntax is communicated through formatting, not text content.
- **No inline comments**: comments are done exclusively through Word's comment feature (Insert > Comment).
- Any non-reserved font (e.g., Cambria Math) can be used as a neutral "code" font with no type implications.

## Functions

- **Declaration**: a **heading** (Heading 1, Heading 2, etc.) defines a function. The heading text is the function name.
- **Return type**: determined by the **font** of the heading text.
- **Parameters**: defined in a **subtitle** paragraph immediately following the heading, or a plain paragraph where each parameter's font indicates its type.
- **Entry point**: a paragraph with a **drop cap** marks the program's entry point (equivalent to `main`).
- **Visibility**: functions listed in the **Table of Contents** are public/exported. Functions not in the ToC are private to the module. (TBD.)

## Variables

- **Declaration**: the first occurrence of a word is its declaration. No explicit declaration syntax.
- **Assignment**: `variable ← value` using the left arrow `←` (U+2190).
- **Immutability by default**: variables are immutable. To reassign, the reassignment must be written with **Track Changes** enabled, so the old value appears as deleted and the new value as inserted. Mutation history is visible in the document.
- **Constants**: **small caps** formatting marks a variable as a compile-time constant.

## Types and Casting

Types are determined by **font family**. Placing a value or variable in a type font **casts** it to that type.

| Font | Type | Rationale |
|------|------|-----------|
| Times New Roman | string | The literary font — it's for text. |
| Courier New | int | Monospaced, precise, rigid — counting energy. |
| Comic Sans | bool | You either love it or hate it. True or false. |
| Script/cursive (e.g. Brush Script) | float | Cursive is fluid and imprecise, like floating point. |
| Symbol | char | Named "Symbol" — it's for individual symbols. |
| Calibri | auto (inferred) | Word's default font. No formatting = compiler infers the type. |

Any font not in the above list (e.g., Cambria Math) carries no type information and is treated as neutral code text.

## String Literals

**Italic** text is a string literal. The text content is the string value. This applies regardless of font — italics always mean "this is a string."

## Numeric Literals

Numbers are written as text content (e.g., `42`). Font size as numeric value is a possible future feature.

## Arithmetic

- Addition: `+`
- Subtraction: `−` (U+2212, the actual minus sign)
- Multiplication: `×` (U+00D7, the actual times symbol, not `*`)
- Division: `÷` (U+00F7, the actual division symbol, not `/`)
- Modulo: `%`
- Exponentiation: **superscript** formatting (e.g., x with a superscripted 2 = x²)
- Array access: **subscript** formatting (e.g., arr with a subscripted i = arr[i])

## Logical Operators

- Logical AND: `∧` (U+2227)
- Logical OR: `∨` (U+2228)

## Brackets / Grouping

Brackets are represented by **formatting nesting**. Applying a formatting style opens a bracket; ending that formatting closes it. For example:

- **Bold** text opens a bracket. Ending bold closes it.
- Within that bold region, a **highlight color** opens a nested bracket.

The ordering of formatting types for nesting is user-defined (not fixed). However, **undoing an outer formatting inside an inner one is invalid syntax**.

Note: **italic** is reserved for string literals and cannot be used as a bracket level.

## Control Flow

All control flow uses **tables**.

### Match Statements

A table with **one merged top cell** is a match statement:
- The **merged top cell** contains the expression to match on.
- **3-row format**: Row 1 = pattern cells, Row 2 = body cells (paired by column index).
- **2-row format**: Row 1 cells contain pattern (first paragraph) + body (remaining paragraphs).
- Patterns can be **comma-separated** for multiple values per case (e.g., `3,6,9,12`).
- A pattern of `_` is the **default/wildcard** case.

### If Statements (Syntax Sugar)

An if statement is a match statement where the matched expression is boolean:
- The two case cells do not need explicit `true`/`false` patterns.
- The **left cell** is the true branch, the **right cell** is the false branch.

### For Loops

A table with **three cells in the top row** is a for loop (C-style):
- **Left cell**: loop variable initialization.
- **Middle cell**: loop condition.
- **Right cell**: increment / step expression.
- **Remaining rows** are the loop body. If the body rows form a match/if pattern (merged cell + branch cells), they are parsed as nested control flow.

## Function Calls

A function call is an **identifier followed by a formatting bracket** containing the arguments. For example, bold text `factorial` followed by highlighted+bold text `number − 1` = `factorial(number − 1)`.

Within a bracket, **juxtaposition** (identifier followed by a value without an operator) is also a function call: `fizzbuzz i` inside a bold bracket = `fizzbuzz(i)`.

## Return Values

**Right-aligned** text is a return value. The function returns that expression to its caller.

**Page breaks** exit the current function (early return).

## Output / Print

`print` is a built-in function. Call it like any other function: `print` followed by a bracketed argument.

## Error Handling

**Footnotes**: a footnote reference in the main text marks a "try" site. The corresponding footnote at the bottom of the page is the error handler for that site.

## Deferred Execution / Cleanup

**Endnotes**: endnote bodies execute when the current function exits, regardless of how it exits (similar to `defer` in Go or `finally`). They run in reverse order of their appearance.

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

- Heading level vs. ToC for visibility — pick one or use both
- Whether bookmarks should support goto or only exist for cross-references
- Equation editor for complex math (deferred — implementation complexity)
- Details of list-based data structure definitions
- Font size as numeric literal (currently using text content instead)
