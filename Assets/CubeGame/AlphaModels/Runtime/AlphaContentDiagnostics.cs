// CubeGame.AlphaModels — content-cache diagnosis for tests, the editor and the game bootstrap.
using System.Collections.Generic;
using System.Text;

namespace CubeGame.AlphaModels
{
    /// <summary>Result of inspecting the local Alpha content cache (never throws, always explains).</summary>
    public sealed class AlphaContentReport
    {
        public AlphaContentState State;
        public string Root;
        public string Diagnosis = "";
        public readonly List<string> Actors = new List<string>();
        public readonly List<string> Problems = new List<string>();

        public bool Ready => State == AlphaContentState.Ready && Problems.Count == 0;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("ALPHA_CONTENT_STATUS=").Append(State)
              .Append(" root=").Append(string.IsNullOrEmpty(Root) ? "<none>" : Root)
              .Append(" actors=").Append(Actors.Count)
              .Append(" problems=").Append(Problems.Count);
            if (!string.IsNullOrEmpty(Diagnosis)) sb.Append(" diagnosis=").Append(Diagnosis);
            return sb.ToString();
        }

        public string Detail()
        {
            var sb = new StringBuilder(ToString());
            foreach (var a in Actors) sb.Append('\n').Append("  ").Append(a);
            foreach (var p in Problems) sb.Append('\n').Append("  PROBLEM ").Append(p);
            return sb.ToString();
        }
    }

    public static class AlphaContentDiagnostics
    {
        public static AlphaContentReport Inspect(string explicitRoot = null)
        {
            var report = new AlphaContentReport();
            var source = AlphaContentSource.Resolve(explicitRoot);
            report.Root = source.Root;
            report.Diagnosis = source.Diagnosis;
            report.State = source.State;
            if (source.State != AlphaContentState.Ready) return report;
            foreach (var id in source.ActorIds)
            {
                var def = source.Load(id);
                report.Actors.Add(def.Summary());
                if (!def.Ready) report.Problems.Add(id + ": " + string.Join(",", def.Problems));
            }
            if (report.Problems.Count > 0) report.State = AlphaContentState.Incomplete;
            return report;
        }
    }
}
