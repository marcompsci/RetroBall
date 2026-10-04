// Minimal NUnit-compatible surface used by the RetroBall logic tests, so they can
// run with plain `dotnet run` where NuGet is unavailable. Only the members the tests use
// are implemented. Inside Unity, the real nunit.framework.dll is used instead.
using System;
using System.Collections.Generic;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TestCaseAttribute : Attribute
    {
        public readonly object[] Arguments;
        public TestCaseAttribute(params object[] args) { Arguments = args; }
    }

    public sealed class InconclusiveException : Exception
    {
        public InconclusiveException(string message) : base(message) { }
    }

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        private static string M(string message, string detail) =>
            string.IsNullOrEmpty(message) ? detail : detail + " — " + message;

        public static void Inconclusive(string message = null) => throw new InconclusiveException(message ?? "Inconclusive");

        public static void Fail(string message = null) => throw new AssertionException(message ?? "Assert.Fail");

        public static void IsNotEmpty(System.Collections.IEnumerable value, string message = null)
        {
            if (value == null || !value.GetEnumerator().MoveNext()) throw new AssertionException(message ?? "Expected a non-empty value");
        }

        public static void Contains(object expected, System.Collections.ICollection actual, string message = null)
        {
            if (actual != null)
                foreach (var item in actual)
                    if (Equals(item, expected)) return;
            throw new AssertionException(message ?? "Expected collection to contain <" + expected + ">");
        }

        public static void IsTrue(bool condition, string message = null)
        {
            if (!condition) throw new AssertionException(M(message, "Expected true"));
        }

        public static void IsFalse(bool condition, string message = null)
        {
            if (condition) throw new AssertionException(M(message, "Expected false"));
        }

        public static void IsNull(object value, string message = null)
        {
            if (value != null) throw new AssertionException(M(message, "Expected null but was " + value));
        }

        public static void IsNotNull(object value, string message = null)
        {
            if (value == null) throw new AssertionException(M(message, "Expected not null"));
        }

        public static void AreEqual(double expected, double actual, double delta, string message = null)
        {
            if (Math.Abs(expected - actual) > delta)
                throw new AssertionException(M(message, "Expected " + expected + " ± " + delta + " but was " + actual));
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!ObjectsEqual(expected, actual))
                throw new AssertionException(M(message, "Expected <" + expected + "> but was <" + actual + ">"));
        }

        public static void AreNotEqual(object expected, object actual, string message = null)
        {
            if (ObjectsEqual(expected, actual))
                throw new AssertionException(M(message, "Expected values to differ but both were <" + actual + ">"));
        }

        public static void Greater(double a, double b, string message = null) { if (!(a > b)) throw new AssertionException(M(message, a + " is not > " + b)); }
        public static void GreaterOrEqual(double a, double b, string message = null) { if (!(a >= b)) throw new AssertionException(M(message, a + " is not >= " + b)); }
        public static void Less(double a, double b, string message = null) { if (!(a < b)) throw new AssertionException(M(message, a + " is not < " + b)); }
        public static void LessOrEqual(double a, double b, string message = null) { if (!(a <= b)) throw new AssertionException(M(message, a + " is not <= " + b)); }

        public static T Throws<T>(Action code, string message = null) where T : Exception
        {
            try { code(); }
            catch (T ex) { return ex; }
            catch (Exception ex) { throw new AssertionException(M(message, "Expected " + typeof(T).Name + " but got " + ex.GetType().Name)); }
            throw new AssertionException(M(message, "Expected " + typeof(T).Name + " but nothing was thrown"));
        }

        // NUnit treats numerics of different types as equal when their values match.
        private static bool ObjectsEqual(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is System.Collections.IEnumerable ea && b is System.Collections.IEnumerable eb && !(a is string) && !(b is string))
            {
                var la = new List<object>();
                var lb = new List<object>();
                foreach (var x in ea) la.Add(x);
                foreach (var x in eb) lb.Add(x);
                if (la.Count != lb.Count) return false;
                for (int i = 0; i < la.Count; i++) if (!ObjectsEqual(la[i], lb[i])) return false;
                return true;
            }
            if (IsNumeric(a) && IsNumeric(b) && !(a is Enum) && !(b is Enum))
            {
                if (a is float || a is double || b is float || b is double)
                    return Convert.ToDouble(a).Equals(Convert.ToDouble(b));
                return Convert.ToDecimal(a) == Convert.ToDecimal(b);
            }
            return a.Equals(b);
        }

        private static bool IsNumeric(object o) =>
            o is byte || o is sbyte || o is short || o is ushort || o is int || o is uint ||
            o is long || o is ulong || o is float || o is double || o is decimal;
    }

    public static class CollectionAssert
    {
        public static void AllItemsAreUnique<T>(IEnumerable<T> items, string message = null)
        {
            var seen = new HashSet<T>();
            foreach (var item in items)
                if (!seen.Add(item)) throw new AssertionException((message ?? "") + " duplicate item " + item);
        }
    }

    public static class StringAssert
    {
        public static void Contains(string expected, string actual, string message = null)
        {
            if (actual == null || !actual.Contains(expected))
                throw new AssertionException((message ?? "") + " expected \"" + actual + "\" to contain \"" + expected + "\"");
        }
    }
}
