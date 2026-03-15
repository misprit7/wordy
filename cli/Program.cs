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

// Phase 3: Transpile AST to C#
var csharp = CSharpEmitter.Emit(program);

var emitOnly = args.Contains("--emit");

if (emitOnly)
{
    Console.WriteLine(csharp);
    return 0;
}

// Phase 4: Compile and run
return Wordy.CodeGen.Compiler.CompileAndRun(csharp);
