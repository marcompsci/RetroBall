using System; using System.Collections.Generic; using System.IO; using System.Linq;
using Microsoft.CodeAnalysis; using Microsoft.CodeAnalysis.CSharp;
// Compiles the five RetroHoops assemblies against Unity's real reference assemblies (copied from the Mac),
// using the defines/langversion from Unity's own .rsp files. Prints errors and our warnings.
// Optional third argument "ios": also compile the iOS player code paths (UNITY_IOS without UNITY_EDITOR for
// the runtime assembly, UNITY_IOS plus the Xcode extension for the Editor assembly).
public static class Program {
  static string Refs, Assets;
  public static int Main(string[] a) {
    Refs = a[0]; Assets = a[1];
    bool ios = a.Length > 2 && a[2] == "ios";
    var built = new Dictionary<string, MetadataReference>();
    int errors = 0;
    foreach (var (name, folder, exclude) in new[] {
      ("RetroHoops.Logic", "Scripts/Logic", new string[0]),
      ("RetroHoops.Runtime", "Scripts", new[]{"Scripts/Logic/","Scripts/Editor/"}),
      ("RetroHoops.Editor", "Scripts/Editor", new string[0]),
      ("RetroHoops.Tests.EditMode", "Tests/EditMode", new string[0]),
      ("RetroHoops.Tests.PlayMode", "Tests/PlayMode", new string[0]) }) {
      // .rsp files dumped before the rename are named CallerRetroBall.*.rsp.
      var rspPath = Path.Combine(Refs, "rsp", name + ".rsp");
      if (!File.Exists(rspPath)) rspPath = Path.Combine(Refs, "rsp", name.Replace("RetroHoops.", "CallerRetroBall.") + ".rsp");
      var rsp = File.ReadAllLines(rspPath);
      var defines = rsp.Where(l => l.StartsWith("-define:")).Select(l => l.Substring(8)).ToList();
      if (ios) {
        defines.RemoveAll(d => d.StartsWith("UNITY_STANDALONE"));
        defines.Add("UNITY_IOS");
        if (name == "RetroHoops.Runtime" || name == "RetroHoops.Logic") defines.RemoveAll(d => d.StartsWith("UNITY_EDITOR"));
      }
      var refs = new List<MetadataReference>();
      foreach (var l in rsp.Where(l => l.StartsWith("-r:"))) {
        var p = l.Substring(3).Trim('"'); var file = Path.GetFileName(p);
        if (file.StartsWith("CallerRetroBall.") || file.StartsWith("RetroHoops.")) { var key = file.Replace(".ref.dll", "").Replace(".dll", "").Replace("CallerRetroBall.", "RetroHoops."); if (built.TryGetValue(key, out var r)) refs.Add(r); continue; }
        var local = Path.Combine(Refs, file); if (File.Exists(local)) refs.Add(MetadataReference.CreateFromFile(local));
      }
      if (ios && name == "RetroHoops.Editor") {
        var xcode = Path.Combine(Refs, "UnityEditor.iOS.Extensions.Xcode.dll");
        if (File.Exists(xcode)) refs.Add(MetadataReference.CreateFromFile(xcode));
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
