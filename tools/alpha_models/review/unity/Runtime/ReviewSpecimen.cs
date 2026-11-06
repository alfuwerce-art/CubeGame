// ReviewSpecimen.cs — builds one prepared specimen (StreamingAssets/Review/<id>) at runtime with the
// SAME transfer runtime the batches verified: AlphaRig (flat rig, Alpha anchors), VoxelMesh (Alpha
// face rule), AlphaClipPlayer (imported clips) and AlphaLocomotionDriver (parametric walk formula).
// Nothing is re-modelled or re-animated; the players are driven manually by ReviewViewer.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Review
{
    public class ReviewSpecimen : MonoBehaviour
    {
        public ReviewSpecimenInfo info;
        public ReviewContract contract;
        public ReviewActions actions;
        public AlphaRig rig;
        public AlphaClipPlayer player;
        public AlphaLocomotionDriver driver;
        public AlphaRigPartSpec[] partSpecs = new AlphaRigPartSpec[0];
        public int[][] markers = new int[0][];            // raw pure R/G/B voxel counts per part
        public bool[] markerTransparent = new bool[0];    // Alpha load flag 0 -> markers not drawn
        public bool alphaMarkerRule = true;               // false = "before D61" view (markers drawn)
        public int hairPart = -1;
        public int[] vertexCounts = new int[0];
        public readonly List<string> problems = new List<string>();
        readonly List<Object> _owned = new List<Object>();

        public static ReviewSpecimen Build(ReviewSpecimenInfo info, Material mat, Transform parent, bool alphaMarkerRule = true)
        {
            var dir = Path.Combine(ReviewData.Root, info.id);
            var go = new GameObject($"{info.name} {info.id}");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * AlphaTransferConsts.RootScale;
            var s = go.AddComponent<ReviewSpecimen>();
            s.info = info;
            s.alphaMarkerRule = alphaMarkerRule;
            var rigPath = Path.Combine(dir, "rig.json");
            if (!File.Exists(rigPath)) { s.problems.Add($"RIG_SPEC_MISSING {rigPath}"); return s; }
            var spec = JsonUtility.FromJson<AlphaRigSpec>(File.ReadAllText(rigPath));
            s.partSpecs = spec.parts;
            var rig = s.rig = go.AddComponent<AlphaRig>();
            rig.rigId = spec.rig; rig.variant = spec.variant; rig.weaponSubtype = spec.weaponSubtype;
            rig.weaponHandleMinZ = spec.weaponHandleMinZ; rig.weaponHandleMaxZ = spec.weaponHandleMaxZ;
            int n = spec.parts.Length;
            rig.partNames = new string[n]; rig.parts = new Transform[n]; rig.meshes = new Transform[n];
            rig.sizes = new Vector3Int[n]; rig.anchors = new string[n];
            s.markers = new int[n][]; s.vertexCounts = new int[n]; s.markerTransparent = new bool[n];
            var hairMat = AlphaRig.MakeHairMaterial(mat);           // Alpha draw order head -> hair
            s._owned.Add(hairMat);
            for (int i = 0; i < n; i++)
            {
                var ps = spec.parts[i];
                var bytes = File.ReadAllBytes(Path.Combine(dir, ps.file));
                s.markers[i] = ReviewData.MarkerCounts(bytes);
                s.markerTransparent[i] = ps.markerTransparent;
                if (ps.name == "hair") s.hairPart = i;
                var built = VoxelMesh.Build(bytes, ps.name + "_" + Path.GetFileNameWithoutExtension(ps.model),
                                            ps.markerTransparent && alphaMarkerRule);
                if (built.problem != null) s.problems.Add($"MESH {ps.name}: {built.problem}");
                if (built.size != new Vector3Int(ps.size[0], ps.size[1], ps.size[2])) s.problems.Add($"MESH_SIZE {ps.name}");
                if (built.mesh != null)
                {
                    s._owned.Add(built.mesh);
                    s.vertexCounts[i] = built.mesh.vertexCount;
                    if (alphaMarkerRule && ps.expectedVertices > 0 && built.mesh.vertexCount != ps.expectedVertices)
                        s.problems.Add($"GEOMETRY {ps.name} vertices {built.mesh.vertexCount} != {ps.expectedVertices}");
                }
                var part = new GameObject("part_" + ps.name).transform;
                part.SetParent(go.transform, false);
                var meshT = new GameObject("mesh").transform;
                meshT.SetParent(part, false);
                meshT.gameObject.AddComponent<MeshFilter>().sharedMesh = built.mesh;
                meshT.gameObject.AddComponent<MeshRenderer>().sharedMaterial = AlphaRig.MaterialFor(ps.name, mat, hairMat);
                rig.partNames[i] = ps.name; rig.parts[i] = part; rig.meshes[i] = meshT;
                rig.sizes[i] = built.size; rig.anchors[i] = ps.anchor;
            }
            rig.SetWeaponOnBack(true);
            for (int i = 0; i < n; i++) rig.SetTint(i, spec.parts[i].HasTint ? spec.parts[i].TintColor : Color.white);

            // clips: the players stay disabled; ReviewViewer owns the clock
            var player = s.player = go.AddComponent<AlphaClipPlayer>();
            player.enabled = false; player.autoPlay = false;
            var clipDir = Path.Combine(dir, "clips");
            var files = Directory.Exists(clipDir) ? Directory.GetFiles(clipDir, "*.json").OrderBy(f => f).ToArray() : new string[0];
            player.clipAssets = files.Select(f =>
            {
                var t = new TextAsset(File.ReadAllText(f)) { name = Path.GetFileNameWithoutExtension(f) };
                s._owned.Add(t);
                return t;
            }).ToArray();
            player.LoadClips();
            s.problems.AddRange(player.Problems);
            if (files.Length == 0) s.problems.Add("NO_CLIPS");

            var loco = Path.Combine(dir, "locomotion.json");
            if (File.Exists(loco))
            {
                var d = s.driver = go.AddComponent<AlphaLocomotionDriver>();
                d.enabled = false;
                d.specAsset = new TextAsset(File.ReadAllText(loco)) { name = "locomotion" };
                s._owned.Add(d.specAsset);
                d.Load();
                s.problems.AddRange(d.Problems);
            }
            var con = Path.Combine(dir, "contract.json");
            s.contract = File.Exists(con) ? JsonUtility.FromJson<ReviewContract>(File.ReadAllText(con)) : null;
            if (s.contract == null) s.problems.Add("CONTRACT_MISSING");
            var act = Path.Combine(dir, "actions.json");
            s.actions = File.Exists(act) ? JsonUtility.FromJson<ReviewActions>(File.ReadAllText(act)) : null;
            s.problems.AddRange(rig.Validate());
            return s;
        }

        void OnDestroy()
        {
            foreach (var o in _owned)
                if (o != null) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
        }

        /// <summary>Pose the rig from a source at a cursor (clip: ms or walk phase; formula: phase).</summary>
        public void Apply(ReviewSource s, float cursor)
        {
            if (s == null) return;
            if (s.formula)
            {
                rig.SetWeaponOnBack(true);
                driver.amplitude = s.amp;
                driver.phase = cursor;
                driver.Apply();
            }
            else
            {
                if (player.playing != s.clip || player.Current == null) player.Play(s.clip, cursor);
                player.ApplyAt(cursor);
            }
        }

        static bool IsStatic(AlphaClipData d) => d.param == "time_ms" && d.duration == 0;
        static bool IsLoop(AlphaClipData d) => d.loop && d.param == "walk_phase";

        /// <summary>Playable sources of a mode. Every clip carries its own provenance status.</summary>
        public List<ReviewSource> Sources(ReviewMode mode)
        {
            var list = new List<ReviewSource>();
            if (player == null) return list;
            var clips = player.ClipNames.Select(player.Get).Where(c => c != null).ToList();
            if (mode == ReviewMode.Idle)
            {
                foreach (var c in clips.Where(c => IsStatic(c.Data)))
                    list.Add(new ReviewSource { label = $"{c.Data.name} (клип)", status = c.Data.status, clip = c.Data.name, category = "STATIC_POSE" });
                if (driver != null && driver.Spec != null)
                    list.Add(new ReviewSource { label = "формула ходьбы, амплитуда 0 (стойка)", status = ReviewData.FormulaStatus, formula = true, amp = 0f });
            }
            else if (mode == ReviewMode.Walk)
            {
                foreach (var c in clips.Where(c => IsLoop(c.Data)))
                    list.Add(new ReviewSource { label = $"{c.Data.name} (клип)", status = c.Data.status, clip = c.Data.name, category = "LOOP_LOCOMOTION" });
                if (driver != null && driver.Spec != null)
                    list.Add(new ReviewSource { label = "формула ходьбы, живой драйвер (amp 0.19)", status = ReviewData.FormulaStatus, formula = true, amp = 0.19f });
            }
            else
            {
                foreach (var c in clips.Where(c => !IsStatic(c.Data) && !IsLoop(c.Data))
                             .OrderBy(c => c.Data.actionCode < 0 ? int.MaxValue : c.Data.actionCode).ThenBy(c => c.Data.name))
                {
                    var d = c.Data;
                    string cat = d.category == "MOTION" ? "" : $" [{d.category}]";
                    list.Add(new ReviewSource
                    {
                        label = d.actionCode >= 0 ? $"ID {d.actionCode:000} · {d.name}{cat}" : $"{d.name}{cat}",
                        status = d.status, clip = d.name, category = d.category, actionCode = d.actionCode
                    });
                }
            }
            return list;
        }

        /// <summary>Bounds of the visible parts (world space).</summary>
        public Bounds VisibleBounds()
        {
            bool any = false; var b = new Bounds(transform.position, Vector3.zero);
            if (rig == null) return b;
            foreach (var m in rig.meshes)
            {
                var r = m != null ? m.GetComponent<Renderer>() : null;
                if (r == null || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Hair colour factor (Alpha uniform30 of the hair block = hair_rgb/255).</summary>
        public void SetHairColour(Color c) { if (hairPart >= 0) rig.SetTint(hairPart, c); }
        public Color HairColour => hairPart >= 0 ? rig.TintOf(hairPart) : Color.white;
        /// <summary>HAIR_RANDOM_PER_NPC | HAIR_FIXED_BY_KIND (pipeline voxel_markers.hair_rule).</summary>
        public string HairRule => hairPart >= 0 ? partSpecs[hairPart].tintMode : "";
        public bool HairFixedByKind => HairRule == "HAIR_FIXED_BY_KIND";

        public string ModelOf(string part) => partSpecs.FirstOrDefault(p => p.name == part)?.model ?? "—";
    }
}
