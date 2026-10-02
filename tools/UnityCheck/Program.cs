using System; using System.Collections.Generic; using System.IO; using System.Linq;
using Microsoft.CodeAnalysis; using Microsoft.CodeAnalysis.CSharp;
// Compiles the five CallerRetroBall assemblies against Unity's real reference assemblies (copied from the Mac),
// using the defines/langversion from Unity's own .rsp files. Prints errors and our warnings.
public static class Program {
  static string Refs, Assets;
  public static int Main(string[] a) {
    Refs = a[0]; Assets = a[1];
    var built = new Dictionary<string, MetadataReference>();
    int errors = 0;
    foreach (var (name, folder, exclude) in new[] {
      ("CallerRetroBall.Logic", "Scripts/Logic", new string[0]),
      ("CallerRetroBall.Runtime", "Scripts", new[]{"Scripts/Logic/","Scripts/Editor/"}),
      ("CallerRetroBall.Editor", "Scripts/Editor", new string[0]),
      ("CallerRetroBall.Tests.EditMode", "Tests/EditMode", new string[0]),
      ("CallerRetroBall.Tests.PlayMode", "Tests/PlayMode", new string[0]) }) {
      var rsp = File.ReadAllLines(Path.Combine(Refs, "rsp", name + ".rsp"));
      var defines = rsp.Where(l => l.StartsWith("-define:")).Select(l => l.Substring(8)).ToList();
      var refs = new List<MetadataReference>();
      foreach (var l in rsp.Where(l => l.StartsWith("-r:"))) {
        var p = l.Substring(3).Trim('"'); var file = Path.GetFileName(p);
        if (file.StartsWith("CallerRetroBall.")) { var key = file.Replace(".ref.dll", "").Replace(".dll", ""); if (built.TryGetValue(key, out var r)) refs.Add(r); continue; }
        var local = Path.Combine(Refs, file); if (File.Exists(local)) refs.Add(MetadataReference.CreateFromFile(local));
      }
      var opts = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: defines);
      var root = Path.Combine(Assets, folder);
      var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
        .Where(f => !exclude.Any(x => f.Replace('\\','/').Contains("/" + x))).ToList();
      var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), opts, f)).ToList();
      var comp = CSharpCompilation.Create(name, trees, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>{{"CS0169",ReportDiagnostic.Suppress},{"CS0649",ReportDiagnostic.Suppress},{"CS1701",ReportDiagnostic.Suppress},{"CS1702",ReportDiagnostic.Suppress}}));
      var diags = comp.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning && d.Location.IsInSource).ToList();
      int e = diags.Count(d => d.Severity == DiagnosticSeverity.Error);
      errors += e;
      Console.WriteLine($"{name}: {files.Count} files, {e} errors, {diags.Count - e} warnings");
      foreach (var d in diags.Take(40)) Console.WriteLine("  " + d.Severity.ToString()[0] + " " + d.Id + " " + Path.GetRelativePath(Assets, d.Location.SourceTree.FilePath) + ":" + (d.Location.GetLineSpan().StartLinePosition.Line + 1) + " " + d.GetMessage());
      built[name] = comp.ToMetadataReference();
    }
    return errors == 0 ? 0 : 1;
  }
}
