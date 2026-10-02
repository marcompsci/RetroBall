using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public static class Program
{
    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : "../../CallerRetroBall/Assets";
        var symbolSets = new[]
        {
            new[] { "UNITY_EDITOR", "ENABLE_INPUT_SYSTEM", "UNITY_INCLUDE_TESTS", "DEVELOPMENT_BUILD" },
            new[] { "UNITY_IOS" }, // player build without editor / input system defines
        };
        int errors = 0, files = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(f => f))
        {
            files++;
            string text = File.ReadAllText(file);
            foreach (var symbols in symbolSets)
            {
                var options = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: symbols);
                var tree = CSharpSyntaxTree.ParseText(text, options, file);
                foreach (var d in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    errors++;
                    Console.WriteLine(d.ToString());
                }
            }
        }
        Console.WriteLine($"Syntax check: {files} files, {errors} errors");
        return errors;
    }
}
