using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>Reflection test runner for the NUnit shim. Exit code = number of failures.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : null;
        int passed = 0, failed = 0;
        var fixtures = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetMethods().Any(IsTest))
            .OrderBy(t => t.FullName);

        foreach (var type in fixtures)
        {
            var setUp = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<SetUpAttribute>() != null);
            var tearDown = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<TearDownAttribute>() != null);
            foreach (var method in type.GetMethods().Where(IsTest).OrderBy(m => m.MetadataToken))
            {
                var cases = method.GetCustomAttributes<TestCaseAttribute>().Select(c => c.Arguments).ToList();
                if (cases.Count == 0) cases.Add(Array.Empty<object>());
                foreach (var caseArgs in cases)
                {
                    string name = type.Name + "." + method.Name + (caseArgs.Length > 0 ? "(" + string.Join(",", caseArgs) + ")" : "");
                    if (filter != null && !name.Contains(filter)) continue;
                    var instance = Activator.CreateInstance(type);
                    try
                    {
                        setUp?.Invoke(instance, null);
                        method.Invoke(instance, caseArgs);
                        tearDown?.Invoke(instance, null);
                        passed++;
                        Console.WriteLine("  PASS " + name);
                    }
                    catch (TargetInvocationException ex)
                    {
                        failed++;
                        Console.WriteLine("  FAIL " + name + "\n       " + ex.InnerException?.Message);
                    }
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Result: {passed} passed, {failed} failed, {passed + failed} total");
        return failed;
    }

    private static bool IsTest(MethodInfo m) =>
        m.GetCustomAttribute<TestAttribute>() != null || m.GetCustomAttributes<TestCaseAttribute>().Any();
}
