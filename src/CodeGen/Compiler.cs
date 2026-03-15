using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wordy.CodeGen;

public static class Compiler
{
    public static int CompileAndRun(string csharpSource)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(csharpSource);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
        };

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")));

        var compilation = CSharpCompilation.Create(
            "WordyProgram",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            Console.Error.WriteLine("Compilation errors:");
            foreach (var diag in result.Diagnostics.Where(d =>
                d.Severity == DiagnosticSeverity.Error))
            {
                Console.Error.WriteLine($"  {diag}");
            }
            return 1;
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        var entryPoint = assembly.EntryPoint;

        if (entryPoint is null)
        {
            Console.Error.WriteLine("No entry point found in compiled program.");
            return 1;
        }

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
            Console.Error.WriteLine($"Runtime error: {ex.InnerException?.Message ?? ex.Message}");
            return 1;
        }

        return 0;
    }
}
