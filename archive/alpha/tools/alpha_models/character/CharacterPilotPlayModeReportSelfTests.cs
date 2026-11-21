// S05A-2 (FIX-07) — EditMode self-tests for the PlayMode report separation.
//
// The defect these tests pin down: the lifecycle verdict used to rewrite PilotOutput/playmode.txt and
// playmode.json when it downgraded a run to FAIL, so the sampler's diagnostic list disappeared and all that
// was left was "lifecycle: verdict downgraded to FAIL". The tests use a sampler report containing a specific
// problem string and require that string to survive in the composed lifecycle/verdict reports, and that the
// writer leaves the sampler-owned file byte-identical.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static partial class CharacterPilotVerifier
{
    internal static class PlayModeReportSelfTests
    {
        internal static (int cases, int failed, string json) Run()
        {
            var entries = new List<(string name, bool expectProblems, bool pass, List<string> problems)>();
            int failed = 0;

            void Add(string name, bool expectProblems, List<string> problems, string needle = null)
            {
                bool pass = (problems.Count > 0) == expectProblems;
                if (pass && needle != null)
                    pass = problems.Any(p => p.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!pass) failed++;
                entries.Add((name, expectProblems, pass, problems));
            }

            List<string> Contains(string text, string needle)
            {
                var found = text != null && text.IndexOf(needle, StringComparison.Ordinal) >= 0;
                return found ? new List<string>() : new List<string> { $"'{needle}' is missing from the report" };
            }

            List<string> Expect(bool condition, string problem) =>
                condition ? new List<string>() : new List<string> { problem };

            const string session = "session-abc123";
            const string samplerProblem = "attack: left palm left the secondary grip by 0.125";
            const string otherProblem = "blade tip: does not move in PlayMode (travel 0.26 < 0.35)";
            var samplerText = "PLAYMODE_RESULT=FAIL\n" + "session=" + session + "\nframes=40/40\n" +
                              samplerProblem + "\n" + otherProblem + "\n";
            var reasons = new List<string>
            {
                "lifecycle: verdict downgraded to FAIL before exit (sampler=True, duplicateFinish=False, " +
                "duplicateHandler=False, forbiddenExit=False)",
            };

            // Extraction keeps the problem lines verbatim, including on CRLF input.
            {
                var extracted = ExtractSamplerProblems(samplerText);
                Add("report: sampler problems are extracted verbatim", false,
                    Expect(extracted.Count == 2 && extracted[0] == samplerProblem &&
                           extracted[1] == otherProblem,
                        $"extracted {extracted.Count} problems: {string.Join(" | ", extracted)}"));
                var crlf = ExtractSamplerProblems(samplerText.Replace("\n", "\r\n"));
                Add("report: CRLF sampler reports are parsed the same way", false,
                    Expect(crlf.Count == 2 && crlf[1] == otherProblem,
                        $"extracted {crlf.Count} problems from the CRLF report"));
                Add("report: the sampler result is read from its own header", false,
                    Expect(ExtractSamplerResult(samplerText) == "FAIL",
                        $"sampler result was '{ExtractSamplerResult(samplerText)}'"));
            }

            // The lifecycle report keeps the sampler's diagnostics and the lifecycle reasons side by side.
            {
                var lifecycle = ComposeReportText("PLAYMODE_LIFECYCLE_RESULT", "FAIL", session, 40, 40,
                    SamplerPresent, samplerText, reasons);
                var check = Contains(lifecycle, samplerProblem)
                    .Concat(Contains(lifecycle, otherProblem))
                    .Concat(Contains(lifecycle, reasons[0]))
                    .Concat(Contains(lifecycle, "sampler_result=FAIL"))
                    .ToList();
                Add("report: the lifecycle report keeps the sampler problems verbatim", false, check);

                var verdict = ComposeReportText("PLAYMODE_VERDICT", "FAIL", session, 40, 40,
                    SamplerPresent, samplerText, reasons);
                var verdictCheck = Contains(verdict, "PLAYMODE_VERDICT=FAIL")
                    .Concat(Contains(verdict, samplerProblem))
                    .Concat(Contains(verdict, reasons[0]))
                    .ToList();
                Add("report: the combined verdict carries both lists", false, verdictCheck);

                var json = ComposeReportJson("FAIL", session, 40, 40, SamplerPresent, samplerText, reasons);
                var jsonCheck = Contains(json, samplerProblem)
                    .Concat(Contains(json, reasons[0]))
                    .Concat(Contains(json, "\"samplerResult\": \"FAIL\""))
                    .ToList();
                Add("report: the JSON verdict carries both lists", false, jsonCheck);
            }

            // A missing sampler report must be named, never replaced by a lifecycle-only verdict.
            {
                var lifecycle = ComposeReportText("PLAYMODE_LIFECYCLE_RESULT", "FAIL", session, 0, 40,
                    SamplerNotWritten, "", reasons);
                var check = Contains(lifecycle, "sampler_report=not-written")
                    .Concat(Contains(lifecycle, "the sampler never wrote playmode.txt"))
                    .Concat(Contains(lifecycle, "sampler_result=none"))
                    .ToList();
                Add("report: a missing sampler report is stated explicitly", false, check);

                var notRun = ComposeReportText("PLAYMODE_LIFECYCLE_RESULT", "FAIL", session, 0, 40,
                    SamplerNotRun, "", reasons);
                Add("report: a sampler that never ran is stated explicitly", false,
                    Contains(notRun, "PlayMode was never entered"));
            }

            // File round trip: the writer must not touch the sampler-owned file, and must still carry its list.
            {
                var dir = Path.Combine(OutputDir, "selftest_playmode_report");
                Directory.CreateDirectory(dir);
                var samplerPath = Path.Combine(dir, SamplerReportFile);
                File.WriteAllText(samplerPath, samplerText);
                WritePlayModeReports(dir, "FAIL", session, 40, 40, SamplerPresent, reasons);

                var after = File.ReadAllText(samplerPath);
                Add("report: writing the lifecycle report leaves playmode.txt byte-identical", false,
                    Expect(after == samplerText, "playmode.txt was modified by the lifecycle writer"));

                var lifecyclePath = Path.Combine(dir, LifecycleReportFile);
                var verdictPath = Path.Combine(dir, VerdictReportFile);
                Add("report: the writer produces the lifecycle and verdict reports", false,
                    Expect(File.Exists(lifecyclePath) && File.Exists(verdictPath) &&
                           File.Exists(Path.Combine(dir, LifecycleReportJson)) &&
                           File.Exists(Path.Combine(dir, VerdictReportJson)),
                        "one of playmode_lifecycle.* / playmode_verdict.* was not written"));
                Add("report: the written report still contains the sampler problem", false,
                    File.Exists(lifecyclePath)
                        ? Contains(File.ReadAllText(lifecyclePath), samplerProblem)
                        : new List<string> { "the lifecycle report is missing" });
                Add("report: the writer never creates a lifecycle-owned playmode.json", false,
                    Expect(!File.Exists(Path.Combine(dir, "playmode.json")),
                        "the lifecycle writer created playmode.json"));
            }

            var casesJson = new StringBuilder("[");
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (i > 0) casesJson.Append(',');
                casesJson.Append($"{{\"name\":{Q(entry.name)}," +
                                 $"\"expected\":{(entry.expectProblems ? "\"problems\"" : "\"clean\"")}," +
                                 $"\"passed\":{(entry.pass ? "true" : "false")}," +
                                 $"\"problems\":[{string.Join(",", entry.problems.Select(Q))}]}}");
            }
            casesJson.Append(']');
            return (entries.Count, failed, casesJson.ToString());
        }
    }
}
