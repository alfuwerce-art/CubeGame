using System;
using System.Collections.Generic;

namespace CubeGame.M0Pilots
{
    /// <summary>
    /// Minimal assertion harness for the isolated M0-00 pilot lab.
    /// No Unity, no NUnit: the lab is compiled by the Roslyn compiler shipped with Unity
    /// against Mono 4.5 assemblies and produces a deterministic console report.
    /// </summary>
    public sealed class PilotResult
    {
        public PilotResult(string name, int passed, int failed, IList<string> failures)
        {
            Name = name;
            Passed = passed;
            Failed = failed;
            Failures = failures;
        }

        public string Name { get; }

        public int Passed { get; }

        public int Failed { get; }

        public IList<string> Failures { get; }
    }

    public static class Check
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> Failures = new List<string>();
        private static string _suite = "?";

        public static void Start(string suite)
        {
            _suite = suite;
            _passed = 0;
            _failed = 0;
            Failures.Clear();
            Console.WriteLine();
            Console.WriteLine("== " + suite + " ==");
        }

        public static PilotResult Finish()
        {
            string marker = _failed == 0 ? "PASS" : "FAIL";
            Console.WriteLine(_suite + "_RESULT=" + marker + " checks=" + (_passed + _failed) + " failed=" + _failed);
            return new PilotResult(_suite, _passed, _failed, new List<string>(Failures));
        }

        public static void True(string name, bool condition, string detail = null)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("[PASS] " + name);
                return;
            }

            _failed++;
            string message = string.IsNullOrEmpty(detail) ? name : name + " | " + detail;
            Failures.Add(message);
            Console.WriteLine("[FAIL] " + message);
        }

        public static void Equal(string name, int expected, int actual)
        {
            True(name, expected == actual, "expected=" + expected + " actual=" + actual);
        }

        public static void Equal<TEnum>(string name, TEnum expected, TEnum actual)
            where TEnum : struct, IConvertible
        {
            True(name, expected.Equals(actual), "expected=" + expected + " actual=" + actual);
        }

        public static void Equal(string name, long expected, long actual)
        {
            True(name, expected == actual, "expected=" + expected + " actual=" + actual);
        }

        public static void Equal(string name, string expected, string actual)
        {
            True(name, string.Equals(expected, actual, StringComparison.Ordinal),
                "expected='" + expected + "' actual='" + actual + "'");
        }

        public static void Near(string name, double expected, double actual, double tolerance = 1e-9)
        {
            double delta = Math.Abs(expected - actual);
            True(name, delta <= tolerance,
                "expected=" + expected.ToString("R") + " actual=" + actual.ToString("R") + " delta=" + delta.ToString("R"));
        }

        public static void Throws<TException>(string name, Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                True(name, true);
                return;
            }
            catch (Exception exception)
            {
                True(name, false, "wrong exception: " + exception.GetType().Name + " " + exception.Message);
                return;
            }

            True(name, false, "no exception");
        }
    }

    public static class TestData
    {
        /// <summary>Classic Microsoft CRT rand() sequence for srand(1), used as an independent known vector.</summary>
        public static readonly int[] MsRandSeed1 = { 41, 18467, 6334, 26500, 19169 };
    }
}
