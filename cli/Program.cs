using Wordy.Reader;
using Wordy.Ast;
using Wordy.CodeGen;

// Generate test files
if (args.Length >= 2 && args[0] == "--gen")
{
    var outPath = args.Length >= 3 ? args[2] : args[1] + ".docx";
    switch (args[1].ToLowerInvariant())
    {
        case "fibonacci":
            Wordy.Debug.DocxGenerator.GenerateFibonacci(outPath);
            return 0;
        case "comprehensive":
            Wordy.Debug.DocxGenerator.GenerateComprehensive(outPath);
            return 0;
        case "arrays":
            Wordy.Debug.DocxGenerator.GenerateArrays(outPath);
            return 0;
        case "scan":
            Wordy.Debug.DocxGenerator.GenerateScanTest(outPath);
            return 0;
        default:
            Console.Error.WriteLine($"Unknown generator: {args[1]}");
            return 1;
    }
}

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: wordy <file.docx>");
    return 1;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 1;
}

var dumpIR = args.Contains("--dump-ir");
var dumpRaw = args.Contains("--dump-raw");

if (dumpRaw)
{
    Wordy.Debug.RawDump.Dump(path);
    return 0;
}

// Phase 1: Read .docx into document IR
var document = DocumentReader.Read(path);

if (dumpIR)
{
    Wordy.Debug.DumpIR.Dump(document);
    return 0;
}

// Phase 2: Parse IR into AST
var program = Parser.Parse(document);

// Phase 3: Resolve imports — find and compile imported .docx files
var baseDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var allFunctions = new List<Function>(program.Functions);
ResolveImports(program.Imports, baseDir, resolved, allFunctions);
program = new Wordy.Ast.Program(allFunctions, program.Imports);

// Phase 4: Transpile AST to C#
var csharp = CSharpEmitter.Emit(program);

var emitOnly = args.Contains("--emit");

if (emitOnly)
{
    Console.WriteLine(csharp);
    return 0;
}

// Phase 5: Compile and run
return Wordy.CodeGen.Compiler.CompileAndRun(csharp);

static void ResolveImports(List<Import> imports, string baseDir,
    HashSet<string> resolved, List<Function> allFunctions)
{
    foreach (var import in imports)
    {
        var fileName = import.FileName + ".docx";
        if (resolved.Contains(fileName)) continue;
        resolved.Add(fileName);

        var importPath = Path.Combine(baseDir, fileName);
        if (!File.Exists(importPath))
        {
            Console.Error.WriteLine($"Warning: imported file not found: {importPath}");
            continue;
        }

        var importDoc = DocumentReader.Read(importPath);
        var importProgram = Parser.Parse(importDoc);

        // Add non-entry-point functions from the imported file
        foreach (var func in importProgram.Functions)
        {
            if (!func.IsEntryPoint)
                allFunctions.Add(func);
        }

        // Recursively resolve the imported file's own imports
        ResolveImports(importProgram.Imports, baseDir, resolved, allFunctions);
    }
}
