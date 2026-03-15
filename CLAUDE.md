# Wordy

An esoteric programming language where Microsoft Word `.docx` documents are the source code. All syntax is represented through document formatting — there are no keywords. The goal is to be fun and clever, not practical. The project may become a YouTube video.

## Architecture

The compiler pipeline is: `.docx` → DocumentIR → AST → C# source → Roslyn compilation → execution.

```
src/
├── Program.cs              # CLI entry point with --emit, --dump-ir, --dump-raw, --gen flags
├── Reader/
│   ├── DocumentIR.cs       # Formatting-aware IR (paragraphs with runs, tables with cells)
│   └── DocumentReader.cs   # .docx → IR using OpenXML SDK
├── Ast/
│   ├── Ast.cs              # AST node definitions (expressions, statements, functions)
│   └── Parser.cs           # IR → AST: formatting brackets, table control flow, font types
├── CodeGen/
│   ├── CSharpEmitter.cs    # AST → C# source code
│   └── Compiler.cs         # Roslyn in-memory compilation and execution
└── Debug/
    ├── DumpIR.cs            # IR debug printer
    ├── RawDump.cs           # Raw OpenXML debug printer
    └── DocxGenerator.cs     # Programmatic .docx test file generator
```

## Language semantics

### Functions
- **Headings** define functions. The heading text = function name, the heading font = return type.
- **Subtitle** paragraph after the heading defines parameters (each run = one param, font = type).
- Plain paragraphs with typed-font runs after a heading also work as parameter declarations.
- **Drop cap** paragraph (Format → Drop Cap → In Margin) marks the entry point / main function.
- The drop cap initial letter is stitched with the continuation paragraph to form the first statement.

### Types and casting
Font family determines type. Putting a value/variable in a type font casts it.

| Font | Type |
|------|------|
| Courier New | int |
| Times New Roman | string |
| Comic Sans MS | bool |
| Script/cursive | float |
| Symbol | char |
| Calibri | auto (inferred) |

Any non-reserved font (e.g. Cambria Math) carries no type info — use it as neutral code text.

Expression-level font casting: if ALL value tokens in an expression share the same type font, the entire expression result is cast (not individual tokens). E.g., `n % 2` all in Comic Sans → `Convert.ToBoolean(n % 2)`. The parser unwraps per-token casts and applies one expression-level cast.

### String literals
**Italic** text = string literal. The raw text content is the string value. Italic is NOT a bracket type.

### Brackets / grouping
Formatting nesting represents parentheses. Applying a format opens a bracket, ending it closes.
- **Bold** and **highlight color** are bracket types.
- Nested formatting = nested brackets. Undoing outer formatting inside inner is invalid.

### Function calls
- Identifier followed by a formatting bracket = function call. The bracket content = arguments.
- Inside a bracket, **juxtaposition** (identifier followed by values without operators) = function call.
- **Multi-argument**: OCaml-style, space-separated values inside a bracket. `max a b` = `max(a, b)`.
- To pass a negated argument, wrap it in an inner bracket (e.g., highlight inside bold) to disambiguate from subtraction.

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
| superscript | exponentiation (N² → `(int)Math.Pow(N, 2)`) |

### Control flow — all via tables
- **If statement**: table with 1 merged top cell (condition) + 2 cells below (true/false branches). This is syntactic sugar for a match on bool — if the condition is boolean, the left cell = true, right = false.
- **Match statement**: 3-row table. Row 0 = merged subject cell, Row 1 = pattern cells, Row 2 = body cells. Patterns can be comma-separated (`3,6,9,12`). `_` = wildcard/default.
- **For loop**: table with 3 cells in the top row (init | condition | step). Remaining rows = body. For loop init variables are hoisted before the for statement so they remain accessible after the loop.
- **Nested control flow**: for loop body rows can form an if/match pattern (merged row + branch row). Tables can also be nested inside table cells (e.g., for loop inside an if branch).

### Other syntax
- **Right-aligned** text = return value.
- **`print`** = built-in function (call like any function, emits `Console.WriteLine`).
- **Case insensitive** — all identifiers lowercased internally, emitted as PascalCase.
- **Whitespace insensitive** — newlines and spaces don't matter.
- **Comments** = Word's comment feature (Insert → Comment). No inline comments.
- **First occurrence** of a variable = declaration (emits `var`). Subsequent = reassignment.

## Building and running

```bash
cd src
dotnet run -- program.docx             # compile and run
dotnet run -- program.docx --emit      # show generated C#
dotnet run -- program.docx --dump-ir   # show formatting-aware IR
dotnet run -- program.docx --dump-raw  # show raw OpenXML structure
dotnet run -- --gen fibonacci out.docx       # generate test .docx
dotnet run -- --gen comprehensive out.docx   # generate comprehensive test
```

## Test programs

- `Factorial.docx` — recursive factorial (hand-authored), outputs 3628800
- `FizzBuzz.docx` — FizzBuzz with match, sum, for loops (hand-authored)
- `Fibonacci.docx` — recursive fibonacci (generated), outputs 55
- `Comprehensive.docx` — exercises all implemented features (generated):
  - Abs: if statement, unary negation
  - Max: multi-argument function call
  - Square: superscript exponentiation
  - Classify: match with comma patterns, wildcard, italic strings, font cast (N.ToString())
  - SumTo: for loop, assignment/reassignment, <=
  - Collatz: Comic Sans bool cast on `n % 2` for if condition
  - CollatzSteps: for loop with function call in body via formatting brackets
  - SumOdds: for loop with nested if in body rows (same table)
  - CalcRange: for loop nested inside an if (table inside table cell)
  - FormatResult: logical AND (∧), string return
  - Main: drop cap entry point, multiple print calls, for loop with nested function calls

## What's implemented

Functions, multi-arg parameters, return (right-align), if/match/for, assignment (←), reassignment, unary negation, arithmetic (× ÷ − + %), exponentiation (superscript), logical (∨ ∧), comparison (= < > <= >= !=), formatting brackets (bold, highlight), multi-arg function calls (OCaml-style juxtaposition), font-based types and casting (per-token and expression-level), italic string literals, drop cap entry point, print built-in, match wildcards (_), comma-separated match patterns, nested control flow in tables (if inside for, for inside if), programmatic .docx generation for tests, Roslyn compilation.

## Documentation site

`docs/index.html` is a single-page documentation site styled to look like the Microsoft Word UI (title bar, ribbon toolbar, navigation pane, ruler, paper document area, status bar). It documents all implemented language features based on the actual code, with a formatting reference table showing implementation status badges (Implemented/Recognized/Planned). Keep it in sync with code changes.

## Not yet implemented (discussed and specced)

- **Subscript** → array/index access
- **Footnotes** → error handling (try/catch — footnote ref = try site, footnote body = catch)
- **Endnotes** → deferred execution (like Go's `defer` or `finally`)
- **Track changes** → mutation (variables immutable by default, track changes required to reassign)
- **Bibliography/citations** → imports (each bibliography entry = a module import)
- **Mail merge fields** → generics/templates (`«T»` = generic type parameter)
- **Cross-references** → pointers/references (cross-ref = pointer, following it = deref)
- **Bookmarks** → named references / goto targets
- **Page breaks** → early return (leave the current page = exit function)
- **Center alignment** → assertions (centered text must evaluate to true)
- **Shadow text effect** → copy/clone
- **Line spacing** → sleep/delay (double-spaced = 2x delay, intentionally useless)
- **Small caps** → constants (compile-time)
- **Table of Contents** → export list (functions in ToC = public, others = private)
- **Lists** (bulleted/numbered) → data structure definitions
- **Equation editor** → complex math expressions (deferred — hard to implement)
- **Font size as numeric literals** → the font size IS the number (not yet used, text content used instead)
- **Hyperlinks** → alternative function call syntax (URL = function name, display text = args)
- **Glow text effect** → alternative print (currently using `print` as built-in name instead)

## Planned: Blazor WASM online compiler (next major task)

The main website will be a Blazor WebAssembly app that lets users upload a `.docx`, transpile to C#, compile with Roslyn, and execute — all client-side in the browser. The existing docs site (`docs/index.html`) will be integrated as a secondary tab/panel.

**Reference project:** https://github.com/itsBuggingMe/CSharpWasm — proves Roslyn-in-WASM is feasible with acceptable load times. Ships .NET DLLs in `wwwroot/lib/`, uses `Assembly.Load` on compiled bytes, redirects `Console.Out` to `StringWriter`.

**Planned restructure:**
```
wordy.sln           Solution file
src/                Becomes a class library (Wordy.Core) — keeps Reader/, Ast/, CodeGen/ only
bin/cli/            Thin console app referencing src/ (Program.cs + Debug/ move here)
web/                Blazor WASM app referencing src/
docs/               Existing docs (content integrated into web app)
```

**Implementation steps:**
1. Convert `src/` to a class library, move CLI bits to `bin/cli/`
2. Add `Stream` overload to `DocumentReader.Read()` (OpenXML supports `WordprocessingDocument.Open(stream, false)`)
3. Create Blazor WASM project (`web/`) targeting .NET 8, requires `wasm-tools` workload
4. Ship .NET reference DLLs in `wwwroot/lib/` for Roslyn: System.Runtime, System.Private.CoreLib, System.Console, mscorlib, System.Collections, System.Linq.Expressions, Microsoft.CSharp, System.Core, netstandard, etc.
5. Create `WordyCompiler` service: uploaded .docx stream → DocumentReader → Parser → CSharpEmitter → Roslyn compile with shipped references → Assembly.Load → redirect Console.Out → invoke entry point → return C# source + output + errors
6. UI: Word-inspired theme (reuse ribbon styling from docs), file upload zone, generated C# panel, console output panel, docs tab
7. `dynamic` type (Auto/Calibri) requires Microsoft.CSharp.dll + System.Linq.Expressions.dll as Roslyn references

## Design decisions and gotchas

- Cambria Math is the recommended neutral font since it includes all required Unicode symbols (×, ÷, −, ∨, ∧, ←).
- The `−` character (U+2212) is the minus sign, NOT the ASCII hyphen `-`. The parser accepts both but documents should use `−` for consistency. Same for `×` (not `*`) and `÷` (not `/`).
- To pass a negated value as a function argument, it must be wrapped in an inner formatting bracket to disambiguate from binary subtraction. E.g., `abs(−5)` needs "abs " [bold] + "−5" [bold+highlight].
- OpenXML enum types (JustificationValues, DropCapLocationValues) have broken ToString() — use `.InnerText` on the Val property to get the string representation.
- Word's `[Dd]ebug/` gitignore pattern catches `src/Debug/` — the gitignore was fixed to use `**/bin/[Dd]ebug/` instead.
- The `old/` directory contains an earlier abandoned attempt from ~3 years ago. Ignore it.
- `.docx` test files live at `/mnt/d/dev/wordy/` (a Windows path accessible from WSL).
