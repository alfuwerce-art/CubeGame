// CubeGame.AlphaModels — local Alpha content cache (D86 M0-01).
//
// Alpha-derived data (decoded models, rigs, clips, contracts) is NEVER committed: the repository
// carries only code and numbers. The runtime asks this class where the local cache is and reports
// a precise status when it is absent, so the game can run (and say why visuals are missing)
// instead of failing silently.
//
// Search order:
//   1. explicit root passed by the bootstrap/tests (CUBEGAME_ALPHA_CONTENT may also be used),
//   2. CUBEGAME_ALPHA_CONTENT environment variable,
//   3. <project>/.scratch/content       — the developer cache next to the Unity project,
//   4. <persistentDataPath>/CubeGame/AlphaContent — a player build with a shipped local cache.
//
// Layout: <root>/actors/<actorId>/ with rig.json, models/*.cubdec, clips/*.json and optional
// contract.json, locomotion.json, actions.json (written by tools/alpha_models/universal/pipeline).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CubeGame.AlphaTransfer;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    public enum AlphaContentState
    {
        Ready = 0,
        /// <summary>No cache was found at any of the known locations.</summary>
        NotConfigured = 1,
        /// <summary>A cache exists but an actor inside it is incomplete or invalid.</summary>
        Incomplete = 2,
    }

    /// <summary>Only the field the visual layer needs; the manifest's blocked-action records are evidence
    /// for the report, not runtime data (JsonUtility skips unknown fields).</summary>
    [Serializable]
    public sealed class AlphaActionManifest
    {
        public string[] requiredClips = Array.Empty<string>();
    }

    [Serializable]
    sealed class AlphaRigIdOnly
    {
        public string rig;
    }

    /// <summary>One actor (creature, NPC or player race) prepared by the transfer pipeline.</summary>
    public sealed class AlphaActorDefinition
    {
        public string Id { get; internal set; }
        public string Directory { get; internal set; }
        public AlphaRigSpec Rig { get; internal set; }
        public AlphaEntityContract Contract { get; internal set; }
        public string LocomotionJson { get; internal set; }
        public string[] ClipJson { get; internal set; } = Array.Empty<string>();
        public string[] RequiredActions { get; internal set; } = Array.Empty<string>();
        public List<AlphaClip> Clips { get; } = new List<AlphaClip>();
        /// <summary>Action code → Alpha timing (actionEnd/recovery/windup) for combat scheduling.</summary>
        public Dictionary<int, AlphaActionTiming> ActionTimings { get; } = new Dictionary<int, AlphaActionTiming>();
        /// <summary>Raw clip documents as written by the pipeline (what AlphaClipPlayer consumes).</summary>
        public List<string> ClipJsonTexts { get; } = new List<string>();
        public List<string> Problems { get; } = new List<string>();

        public bool Ready => Problems.Count == 0 && Rig != null && Rig.parts != null && Rig.parts.Length > 0;
        public bool HasContract => Contract != null;
        public bool HasActionLibrary => RequiredActions.Length > 0;
        public string ControllerMode => Contract != null ? Contract.controllerMode : (HasActionLibrary ? "ACTIONS" : "LOCOMOTION_ONLY");
        public string RigId => Rig != null ? Rig.rig : "";

        public string[] ClipNames
        {
            get
            {
                var names = new string[Clips.Count];
                for (int i = 0; i < Clips.Count; i++) names[i] = Clips[i].Data.name;
                return names;
            }
        }

        public AlphaClip Clip(string name)
        {
            for (int i = 0; i < Clips.Count; i++) if (Clips[i].Data.name == name) return Clips[i];
            return null;
        }

        public string Summary() =>
            $"actor={Id} rig={RigId} parts={(Rig?.parts?.Length ?? 0)} clips={Clips.Count} actions={RequiredActions.Length} " +
            $"mode={ControllerMode} status={(Ready ? "READY" : "PROBLEMS:" + string.Join(",", Problems))}";
    }

    /// <summary>Resolves and loads the local Alpha content cache.</summary>
    public sealed class AlphaContentSource
    {
        public const string EnvVar = "CUBEGAME_ALPHA_CONTENT";
        public const string ActorsFolder = "actors";

        public string Root { get; }
        public AlphaContentState State { get; private set; }
        public string Diagnosis { get; private set; } = "";
        public string ActorsRoot => string.IsNullOrEmpty(Root) ? null : Path.Combine(Root, ActorsFolder);

        AlphaContentSource(string root)
        {
            Root = root;
            Refresh();
        }

        public static AlphaContentSource Resolve(string explicitRoot = null)
        {
            var root = FindRoot(explicitRoot);
            return new AlphaContentSource(root);
        }

        public static string FindRoot(string explicitRoot)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(explicitRoot)) candidates.Add(explicitRoot);
            var env = Environment.GetEnvironmentVariable(EnvVar);
            if (!string.IsNullOrEmpty(env)) candidates.Add(env);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            candidates.Add(Path.Combine(projectRoot, ".scratch", "content"));
            candidates.Add(Path.Combine(Application.persistentDataPath, "CubeGame", "AlphaContent"));
            foreach (var c in candidates)
            {
                try
                {
                    if (Directory.Exists(Path.Combine(c, ActorsFolder))) return Path.GetFullPath(c);
                }
                catch (Exception)
                {
                    // an unreadable candidate is skipped, not fatal: the next one may be valid
                }
            }
            return null;
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(Root) || !Directory.Exists(Root))
            {
                State = AlphaContentState.NotConfigured;
                Diagnosis = $"ALPHA_CONTENT_NOT_CONFIGURED (set {EnvVar} or place the cache at <project>/.scratch/content)";
                return;
            }
            if (!Directory.Exists(ActorsRoot))
            {
                State = AlphaContentState.NotConfigured;
                Diagnosis = $"ALPHA_ACTORS_MISSING {ActorsRoot}";
                return;
            }
            var ids = ActorIds;
            if (ids.Count == 0)
            {
                State = AlphaContentState.Incomplete;
                Diagnosis = $"ALPHA_CONTENT_EMPTY {ActorsRoot}";
                return;
            }
            State = AlphaContentState.Ready;
            Diagnosis = $"ALPHA_CONTENT_READY {ActorsRoot} actors={ids.Count}";
        }

        public IReadOnlyList<string> ActorIds
        {
            get
            {
                if (string.IsNullOrEmpty(ActorsRoot) || !Directory.Exists(ActorsRoot)) return Array.Empty<string>();
                var ids = Directory.GetDirectories(ActorsRoot)
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrEmpty(n) && !n.StartsWith("."))
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();
                return ids;
            }
        }

        public bool TryGetActorDirectory(string id, out string dir)
        {
            dir = string.IsNullOrEmpty(ActorsRoot) || string.IsNullOrEmpty(id) ? null : Path.Combine(ActorsRoot, id);
            return dir != null && Directory.Exists(dir);
        }

        /// <summary>Load one actor, collecting every problem instead of failing on the first one.</summary>
        public AlphaActorDefinition Load(string id)
        {
            var def = new AlphaActorDefinition { Id = id, Rig = null };
            if (!TryGetActorDirectory(id, out var dir))
            {
                def.Problems.Add("ACTOR_DIRECTORY_MISSING " + id);
                return def;
            }
            def.Directory = dir;
            var rigPath = Path.Combine(dir, "rig.json");
            if (!File.Exists(rigPath))
            {
                def.Problems.Add("ACTOR_RIG_MISSING " + rigPath);
                return def;
            }
            try { def.Rig = JsonUtility.FromJson<AlphaRigSpec>(File.ReadAllText(rigPath)); }
            catch (Exception e) { def.Problems.Add("ACTOR_RIG_INVALID " + e.Message); return def; }
            if (def.Rig == null || def.Rig.parts == null || def.Rig.parts.Length == 0)
            {
                def.Problems.Add("ACTOR_RIG_EMPTY");
                return def;
            }
            foreach (var part in def.Rig.parts)
            {
                if (part == null || string.IsNullOrEmpty(part.name)) { def.Problems.Add("ACTOR_PART_UNNAMED"); continue; }
                if (string.IsNullOrEmpty(part.file)) { def.Problems.Add("ACTOR_PART_NO_FILE " + part.name); continue; }
                var file = Path.GetFullPath(Path.Combine(dir, part.file));
                if (!file.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
                    def.Problems.Add("ACTOR_MODEL_OUTSIDE_ACTOR " + part.name);
                else if (!File.Exists(file)) def.Problems.Add("ACTOR_MODEL_MISSING " + part.name);
            }

            var contractPath = Path.Combine(dir, "contract.json");
            if (File.Exists(contractPath))
            {
                var problems = new List<string>();
                def.Contract = AlphaEntityContract.Parse(File.ReadAllText(contractPath), problems);
                def.Problems.AddRange(problems);
            }
            var locomotionPath = Path.Combine(dir, "locomotion.json");
            if (File.Exists(locomotionPath))
            {
                def.LocomotionJson = File.ReadAllText(locomotionPath);
                try
                {
                    var rigId = JsonUtility.FromJson<AlphaRigIdOnly>(def.LocomotionJson);
                    if (rigId != null && !string.IsNullOrEmpty(rigId.rig) && rigId.rig != def.Rig.rig)
                        def.Problems.Add($"ACTOR_LOCOMOTION_RIG_MISMATCH {rigId.rig} != {def.Rig.rig}");
                }
                catch (Exception e) { def.Problems.Add("ACTOR_LOCOMOTION_INVALID " + e.Message); }
            }

            var manifestPath = Path.Combine(dir, "actions.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifest = JsonUtility.FromJson<AlphaActionManifest>(File.ReadAllText(manifestPath));
                    def.RequiredActions = (manifest != null ? manifest.requiredClips : null) ?? Array.Empty<string>();
                }
                catch (Exception e) { def.Problems.Add("ACTOR_ACTION_MANIFEST_INVALID " + e.Message); }
            }

            var clipsDir = Path.Combine(dir, "clips");
            if (Directory.Exists(clipsDir))
            {
                foreach (var file in Directory.GetFiles(clipsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                {
                    var problems = new List<string>();
                    var text = File.ReadAllText(file);
                    var clip = AlphaClip.Parse(text, def.Rig.rig, problems);
                    def.Problems.AddRange(problems);
                    if (clip != null) { def.Clips.Add(clip); def.ClipJsonTexts.Add(text); }
                    if (clip != null && clip.Data.actionCode >= 0 && clip.Data.category == "MOTION")
                        def.ActionTimings[clip.Data.actionCode] = clip.Timing;
                }
            }
            if (def.Clips.Count == 0) def.Problems.Add("ACTOR_NO_CLIPS");
            if (def.HasContract)
            {
                // The full contract check needs the built rig instance (part transforms, anchors), so it is
                // executed by AlphaRigBuilder; here only the clip/action-level statements are verified.
                var contractProblems = def.Contract.Validate(null, def.Clips, def.HasActionLibrary);
                foreach (var p in contractProblems)
                    if (!p.StartsWith("CONTRACT_RIG_MISMATCH", StringComparison.Ordinal) && p != "CONTRACT_NO_RIG")
                        def.Problems.Add(p);
            }
            if (def.HasActionLibrary)
                foreach (var name in def.RequiredActions)
                    if (def.Clip(name) == null) def.Problems.Add("ACTOR_ACTION_CLIP_MISSING " + name);
            return def;
        }

        /// <summary>Load and check every actor of the cache; returns a per-actor status line each.</summary>
        public List<string> Scan()
        {
            var lines = new List<string>();
            foreach (var id in ActorIds) lines.Add(Load(id).Summary());
            return lines;
        }
    }
}
