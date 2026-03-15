# Wordy

An esoteric programming language where Microsoft Word `.docx` documents are the source code. All syntax is represented through document formatting — there are no keywords. The goal is to be fun and clever, not practical.

## Project structure

```
wordy.sln
src/                    Class library (Wordy.Core) — the compiler pipeline
├── Reader/
│   ├── DocumentIR.cs       # IR: paragraphs, runs, tables, lists, imports
│   └── DocumentReader.cs   # .docx → IR (OpenXML SDK, bibliography, citations)
├── Ast/
│   ├── Ast.cs              # AST nodes (expressions, statements, functions, imports)
│   └── Parser.cs           # IR → AST: formatting brackets, tables, fonts, subscripts
├── CodeGen/
│   ├── CSharpEmitter.cs    # AST → C# source
│   └── Compiler.cs         # Roslyn in-memory compilation + execution
cli/                    Console app (references src/)
├── Program.cs              # CLI entry point, multi-file import resolution
└── Debug/
    ├── DumpIR.cs            # IR debug printer
    ├── RawDump.cs           # Raw OpenXML debug printer
    └── DocxGenerator.cs     # Programmatic .docx test file generator
web/                    Blazor WASM app (references src/)
├── Pages/Home.razor        # Compiler UI: upload, examples, multi-file, run
├── Services/WordyCompiler.cs # Browser-side compile+run via Roslyn-in-WASM
├── wwwroot/index.html      # Blazor host page
├── wwwroot/css/app.css     # Word-themed UI styles
├── wwwroot/docs/index.html # Docs site (also served standalone from docs/)
└── wwwroot/examples/       # Auto-populated at build from docx/ (gitignored)
docs/index.html         Standalone docs site (Word UI themed)
docx/                   Single source of truth for .docx files
├── manual/                 # Hand-authored (Factorial, FizzBuzz)
└── generated/              # Programmatically generated (Comprehensive, Fibonacci, Arrays)
old/                    Earlier abandoned attempt (~3 years ago). Ignore.
```

The web project's MSBuild target `CopyDocxExamples` copies from `docx/manual/` and `docx/generated/` into `web/wwwroot/examples/` at build time. Never put example docx files directly in `web/wwwroot/examples/`.

## Language semantics

### Functions
- **Headings** define functions. Heading text = function name, heading font = return type.
- **Subtitle** or typed-font paragraph after heading = parameters (each run = one param, font = type).
- **Drop cap** (Format → Drop Cap → In Margin) = entry point. The initial letter is stitched with the continuation paragraph.

### Types and casting
Font family determines type. Putting a value/variable in a type font casts it.

| Font | Type |
|------|------|
| Courier New | int |
| Times New Roman | string |
| Comic Sans MS | bool |
| Script/cursive | float |
| Impact | char |

Non-reserved fonts (Calibri, Cambria Math) carry no type info — use as neutral code text. Cambria Math is recommended since it includes all required Unicode symbols.

Expression-level font casting: if ALL value tokens share a single type font, the cast applies to the whole expression (not individual tokens). E.g., `n % 2` all in Comic Sans → `Convert.ToBoolean(n % 2)`.

### Literals and text
- **Italic** text = string literal. NOT a bracket type.
- Numbers are written as text content.

### Brackets / grouping
Formatting toggles represent parentheses. Applying a format opens `(`, ending it closes `)`.
- **Bold** and **highlight color** are bracket types.
- Nested formatting = nested brackets.

### Function calls
- Identifier + formatting bracket = function call. Bracket content = arguments.
- Inside a bracket, **juxtaposition** (identifier followed by values, no operators) = function call with multiple args. `max a b` = `max(a, b)`.
- Negated arguments must be wrapped in an inner bracket to disambiguate from subtraction.

### Operators
| Operator | Meaning |
|----------|---------|
| `+` | addition |
| `−` (U+2212) | subtraction / unary negation |
| `×` (U+00D7) | multiplication |
| `÷` (U+00F7) | division |
| `%` | modulo |
| `=` | equality |
| `!=` `<` `>` `<=` `>=` | comparison |
| `∧` (U+2227) | logical AND |
| `∨` (U+2228) | logical OR |
| `←` (U+2190) | assignment |
| superscript | exponentiation (N² → `Math.Pow(N, 2)`) |

The parser accepts ASCII `-` as well as `−` (U+2212).

### Control flow — tables
- **If**: 1 merged top cell (condition) + 2 cells below (true/false branches).
- **Match**: 3-row table — row 0 = merged subject, row 1 = patterns, row 2 = bodies. Comma-separated patterns (`3,6,9,12`), `_` = wildcard.
- **For loop**: 3 cells in top row (init | condition | step), remaining rows = body. Init variables hoisted before the `for` so they're accessible after.
- **Nested**: for body rows can form if/match patterns; tables can nest inside table cells.

### Arrays
- **Numbered lists** = array literals. Font of items determines element type.
  - 1D: flat list → `new int[] {1, 2, 3}`
  - 2D: nested list (blank top-level items as row separators, indent-1 sub-items as values) → `new int[,] {{1, 2}, {3, 4}}`
- **Subscript** = array access. `arr` with subscript `i` → `arr[i]`. Comma-separated subscript `i,j` → `arr[i, j]`.
- Array assignment: `variable ←` paragraph followed by a numbered list.
- Inside formatting brackets, subscript after an identifier = array access (not function call argument).

### Imports (Bibliography/Citations)
- Word's **bibliography** = imports. Each bibliography entry = one imported module.
- **Title** field = filename of the `.docx` to import (without extension).
- **Citations** (References → Insert Citation) reference imported functions inline. The `\p` (page) field = function name.
- Citations become synthetic runs preserving formatting, so they work inside brackets for function calls with arguments.
- The compiler resolves imports by looking for `.docx` files in the same directory. Resolution is recursive.
- The "Works Cited" SDT block is ignored by the parser.

### Other syntax
- **Right-aligned** text = return value.
- **`print`** = built-in function (emits `Console.WriteLine`).
- **Case insensitive** — identifiers lowercased internally, emitted as PascalCase.
- **Whitespace insensitive**.
- **Comments** = Word's comment feature (Insert → Comment).
- **First occurrence** of a variable = declaration (`var`). Subsequent = reassignment.

## Building and running

```bash
# CLI
cd cli
dotnet run -- program.docx             # compile and run
dotnet run -- program.docx --emit      # show generated C#
dotnet run -- program.docx --dump-ir   # show formatting-aware IR
dotnet run -- program.docx --dump-raw  # show raw OpenXML structure
dotnet run -- --gen fibonacci out.docx
dotnet run -- --gen comprehensive out.docx
dotnet run -- --gen arrays out.docx

# Web (Blazor WASM)
cd web
dotnet run                             # serves at https://localhost:5001
```

## Test programs

All live in `docx/` (the single source of truth):

- `manual/Factorial.docx` — recursive factorial, outputs 3628800
- `manual/FizzBuzz.docx` — FizzBuzz with match, sum, for loops; imports Factorial via bibliography citation
- `generated/Fibonacci.docx` — recursive fibonacci, outputs 55
- `generated/Comprehensive.docx` — exercises all features: if, match, for, nested control flow, superscript, font casting, formatting brackets, multi-arg calls, arrays (1D/2D), subscript access, logical operators, string returns
- `generated/Arrays.docx` — array-specific tests: 1D/2D literals, subscript access, for loop over array, multidimensional access

## Not yet implemented (discussed and specced)

- **Footnotes** → error handling (try/catch — footnote ref = try site, footnote body = catch)
- **Endnotes** → deferred execution (`defer` / `finally`)
- **Track changes** → mutation (immutable by default, track changes required to reassign)
- **Mail merge fields** → generics/templates (`«T»`)
- **Cross-references** → pointers/references
- **Bookmarks** → named references / goto targets
- **Page breaks** → early return
- **Center alignment** → assertions
- **Shadow text effect** → copy/clone
- **Line spacing** → sleep/delay (intentionally useless)
- **Small caps** → constants (compile-time)
- **Table of Contents** → export list (ToC = public, others = private)
- **Bulleted lists** → other data structures
- **Equation editor** → complex math expressions (hard to implement)
- **Font size as numeric literals** → the size IS the number
- **Hyperlinks** → alternative function call syntax
- **Glow text effect** → alternative print

## Design decisions and gotchas

- Cambria Math is the recommended neutral font — includes ×, ÷, −, ∨, ∧, ←.
- `−` (U+2212) is the minus sign, not ASCII `-`. Same for `×` (not `*`) and `÷` (not `/`).
- OpenXML enum types (JustificationValues, etc.) have broken `ToString()` — use `.InnerText` on the Val property.
- `double.TryParse` treats commas as thousands separators in some locales. The tokenizer rejects comma-containing text as numbers to avoid `1,0` → `10` in subscripts.
- Inline citations in OpenXML are `SdtRun` elements containing `FieldCode` runs. The reader detects `<w:citation />` in SDT properties and extracts the function name from the `CITATION \p FunctionName` field instruction.
- The web project ships .NET reference DLLs as `.bin` files (renamed from `.dll`) in `wwwroot/lib/` to avoid GitHub Pages blocking. These are copied from the browser-wasm runtime pack at build time.
