# Wordy

An esoteric programming language where Microsoft Word `.docx` documents are the source code. All syntax is represented through document formatting — there are no keywords.

## How it works

The compiler pipeline is: `.docx` → DocumentIR → AST → C# source → Roslyn compilation → execution.

1. **DocumentReader** parses the `.docx` using OpenXML, extracting paragraphs and tables with all formatting metadata (bold, italic, font, alignment, highlight, etc.) into a `DocumentIR`.
2. **Parser** converts the IR into an AST. This is where the core language semantics live — formatting becomes brackets, tables become control flow, fonts become types.
3. **CSharpEmitter** transpiles the AST to C# source code.
4. **Compiler** uses Roslyn to compile and execute the generated C# in-memory.

## Key language concepts

- **Functions** = headings. Parameters in subtitle. Return type from heading font.
- **Brackets** = formatting nesting (bold, highlight). Italic is NOT a bracket — it's for string literals.
- **Control flow** = tables. 1 merged top cell = match/if. 3 cells in top row = for loop.
- **Types** = fonts. Courier New = int, Times New Roman = string, Comic Sans = bool, Script = float, Symbol = char. Non-reserved fonts (e.g. Cambria Math) carry no type info.
- **Casting** = putting a variable in a different type font.
- **String literals** = italic text.
- **Assignment** = `←` (U+2190).
- **Operators** = Unicode symbols: `×` `÷` `−` `∨` `∧` plus `+` `%` `=` `<` `>`.
- **Return** = right-aligned text.
- **Entry point** = drop cap paragraph.
- **Function calls** = identifier followed by a formatting bracket, or juxtaposition inside a bracket.
- **Print** = `print` built-in function.
- **Match wildcard** = `_` pattern.

## Building and running

```bash
cd src
dotnet run -- path/to/program.docx        # run
dotnet run -- path/to/program.docx --emit  # show generated C#
```

## Test programs

Located at `/mnt/d/dev/wordy/`:
- `Factorial.docx` — recursive factorial, outputs 3628800
- `FizzBuzz.docx` — FizzBuzz with match statement, sum function, for loops

## What's implemented vs planned

**Working:** functions, parameters, return, if/match/for, assignment (←), arithmetic (× ÷ − + %), logical (∨ ∧), comparison (= < > <= >= !=), formatting brackets (bold, highlight), font-based types and casting, italic string literals, drop cap entry point, print, match wildcards (_), comma-separated match patterns, nested control flow in tables, Roslyn compilation.

**Not yet implemented:** superscript/subscript, footnotes (error handling), endnotes (defer), track changes (mutation), bibliography (imports), mail merge (generics), cross-references (pointers), bookmarks, page breaks (early return), center alignment (assertions), shadow (clone), line spacing (delay), table of contents (exports), small caps (constants), glow effect, equation editor, data structures (lists).
