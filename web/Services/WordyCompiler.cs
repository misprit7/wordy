using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Wordy.Reader;
using Wordy.Ast;
using Wordy.CodeGen;

namespace web.Services;

public record CompilationResult(
    string? CSharpSource,
    string? Output,
    string? Error,
    bool Success
);

public class WordyCompiler
{
    private readonly HttpClient _http;
    private List<MetadataReference>? _references;

    private static readonly string[] LibraryNames =
    {
        "System.Private.CoreLib.bin",
        "mscorlib.bin",
        "System.bin",
        "System.Console.bin",
        "System.Core.bin",
        "System.Runtime.bin",
        "System.Collections.bin",
        "netstandard.bin",
    };

    public WordyCompiler(HttpClient http)
    {
        _http = http;
    }

    public async Task InitializeAsync()
    {
        if (_references != null) return;

        var refs = new List<MetadataReference>();
        var errors = new List<string>();

        foreach (var name in LibraryNames)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync($"lib/{name}");
                refs.Add(MetadataReference.CreateFromImage(bytes));
            }
            catch (Exception ex)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }

        if (refs.Count == 0)
            throw new InvalidOperationException(
                $"Failed to load any reference assemblies.\n{string.Join("\n", errors)}");

        _references = refs;
    }

    public async Task<CompilationResult> CompileAndRunAsync(Stream docxStream)
    {
        try
        {
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            return new CompilationResult(null, null, ex.Message, false);
        }

        try
        {
            // Phase 1: Read .docx into document IR
            var document = DocumentReader.Read(docxStream);

            // Phase 2: Parse IR into AST
            var program = Parser.Parse(document);

            // Phase 3: Transpile AST to C#
            var csharpSource = CSharpEmitter.Emit(program);

            // Phase 4: Compile with Roslyn
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpSource);

            var compilation = CSharpCompilation.Create(
                "WordyProgram",
                syntaxTrees: new[] { syntaxTree },
                references: _references,
                options: new CSharpCompilationOptions(
                    OutputKind.ConsoleApplication,
                    concurrentBuild: false));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errs = string.Join("\n", result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                return new CompilationResult(csharpSource, null, $"Compilation errors:\n{errs}", false);
            }

            // Phase 5: Execute
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var entryPoint = assembly.EntryPoint;

            if (entryPoint is null)
                return new CompilationResult(csharpSource, null, "No entry point found.", false);

            var writer = new StringWriter();
            var oldOut = Console.Out;
            Console.SetOut(writer);
            try
            {
                var parameters = entryPoint.GetParameters();
                if (parameters.Length == 0)
                    entryPoint.Invoke(null, null);
                else
                    entryPoint.Invoke(null, new object[] { Array.Empty<string>() });
            }
            catch (TargetInvocationException ex)
            {
                return new CompilationResult(csharpSource, writer.ToString(),
                    $"Runtime error: {ex.InnerException?.Message ?? ex.Message}", false);
            }
            finally
            {
                Console.SetOut(oldOut);
            }

            return new CompilationResult(csharpSource, writer.ToString(), null, true);
        }
        catch (Exception ex)
        {
            return new CompilationResult(null, null, ex.Message, false);
        }
    }
}
