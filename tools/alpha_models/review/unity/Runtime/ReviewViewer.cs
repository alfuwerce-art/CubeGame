// ReviewViewer.cs — interactive viewer of the S05B visual review (all 15 S05B-1 kinds).
// Plays ONLY what the pipeline imported: clips with their own provenance status, or the parametric
// walk formula (RECONSTRUCTED_FROM_EVIDENCE). No pose is edited, no clip is invented, no transition
// is blended (switching sources is a hard cut). Problems (tint, missing actions) are shown, not masked.
//
// Controls (Game view must have focus):
//   mouse: LMB/RMB drag = orbit, MMB drag (or Shift+drag) = pan, wheel = zoom, F = frame
//   Up/Down = kind, Left/Right = variant, 1/2/3 = Idle/Walk/Action, Q/E = prev/next clip or action
//   Space = pause, R = restart, , and . = previous/next frame, - and = = speed, L = lineup of 15,
//   P = pivots, B = bounds, G = grid, M = Alpha marker rule on/off (before/after D61),
//   C = next hair colour (live samples), Tab = collapse panel, H = hide UI, F1 = help
// Layout: ReviewLayout (one tabbed side panel + status + timeline, never overlapping, any Game view size).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Review
{
    public class ReviewViewer : MonoBehaviour
    {
        public Material voxelMaterial;
        public ReviewOrbitCamera orbit;
        public float uiScale;                 // 0 = automatic (screen height)

        // ---- PlayMode smoke tour (ReviewSceneBuilder.RunPlayMode) --------------------------------
        public static bool SmokeTour;
        public static bool SmokeDone;
        public static readonly List<string> SmokeLog = new List<string>();
        public static int SmokeSpecimens, SmokeSources, SmokeFrames;

        // ---- state --------------------------------------------------------------------------------
        ReviewIndex _index;
        readonly List<string> _loadProblems = new List<string>();
        List<string> _kinds = new List<string>();
        readonly Dictionary<string, List<ReviewSpecimenInfo>> _byKind = new Dictionary<string, List<ReviewSpecimenInfo>>();
        int _kindIdx, _varIdx, _srcIdx;
        ReviewMode _mode = ReviewMode.Idle;
        ReviewSpecimen _cur;
        List<ReviewSource> _sources = new List<ReviewSource>();
        bool _paused, _repeat = true;
        float _speed = 1f, _cursor, _hold;
        bool _showPivots, _showBounds = true, _showGrid = true, _hideUI, _help, _lineup;
        bool[] _userHidden = new bool[0];
        float _groundY;
        Vector2 _scrollKinds, _scrollSources, _scrollInfo;
        readonly List<Rect> _panels = new List<Rect>();
        bool _dragging; int _dragButton;
        readonly List<LineupMember> _lineupMembers = new List<LineupMember>();
        Transform _stage;
        Material _lineMat;

        class LineupMember { public ReviewSpecimen spec; public ReviewSource src; public float cursor; }

        // D61 colour review: Alpha marker rule on/off (before/after) and the per-instance hair colour
        bool _alphaMarkerRule = true;
        public class HairOption { public string label; public Color colour; public bool observedSameKind, identity; }
        List<HairOption> _hairOptions = new List<HairOption>();
        int _hairChoice; string _hairChoiceKind;
        public enum Tab { Kind, Animation, Parts, Checks, Help }
        Tab _tab = Tab.Kind;
        bool _panelOpen = true;
        ReviewLayout _layout;

        public ReviewSpecimen Current => _cur;
        public ReviewMode Mode => _mode;
        public ReviewSource CurrentSource => _srcIdx >= 0 && _srcIdx < _sources.Count ? _sources[_srcIdx] : null;
        public int KindCount => _kinds.Count;
        public int VariantCount(int k) => _byKind[_kinds[k]].Count;
        public int SourceCount => _sources.Count;
        public IReadOnlyList<string> LoadProblems => _loadProblems;

        // ---- lifecycle ----------------------------------------------------------------------------
        void Awake()
        {
            _stage = new GameObject("ReviewStage").transform;
            _index = ReviewData.LoadIndex(_loadProblems);
            if (_index != null)
            {
                _kinds = _index.kinds.ToList();
                foreach (var k in _kinds) _byKind[k] = _index.specimens.Where(s => s.kind == k && s.prepare == "PASS").ToList();
                _kinds = _kinds.Where(k => _byKind[k].Count > 0).ToList();
            }
            if (orbit == null) orbit = FindFirstObjectByType<ReviewOrbitCamera>();
        }

        void Start()
        {
            if (_kinds.Count > 0) Select(0, 0);
            if (SmokeTour) StartCoroutine(Tour());
        }

        void Update()
        {
            if (_lineup) { AdvanceLineup(Time.deltaTime); return; }
            Advance(Time.deltaTime);
        }

        // ---- selection ----------------------------------------------------------------------------
        public void Select(int kindIdx, int varIdx)
        {
            if (_kinds.Count == 0) return;
            if (_lineup) SetLineup(false);
            _kindIdx = (kindIdx % _kinds.Count + _kinds.Count) % _kinds.Count;
            var list = _byKind[_kinds[_kindIdx]];
            _varIdx = (varIdx % list.Count + list.Count) % list.Count;
            if (_cur != null) Destroy(_cur.gameObject);
            _cur = ReviewSpecimen.Build(list[_varIdx], voxelMaterial, _stage, _alphaMarkerRule);
            _userHidden = new bool[_cur.rig != null ? _cur.rig.partNames.Length : 0];
            _hairOptions = HairOptions(_cur);
            if (_hairChoiceKind != _cur.info.kind) { _hairChoice = 0; _hairChoiceKind = _cur.info.kind; }
            ApplyHairChoice(_cur);
            var keepMode = _mode;
            SetMode(ReviewMode.Idle);
            _groundY = _cur.VisibleBounds().min.y;
            if (orbit != null) orbit.Frame(_cur.VisibleBounds());
            if (keepMode != ReviewMode.Idle) SetMode(keepMode);
        }

        public void SetMode(ReviewMode mode)
        {
            _mode = mode;
            _sources = _cur != null ? _cur.Sources(mode) : new List<ReviewSource>();
            SelectSource(0);
        }

        public void SelectSource(int i)
        {
            _srcIdx = _sources.Count == 0 ? -1 : Mathf.Clamp(i, 0, _sources.Count - 1);
            _cursor = 0f; _hold = 0f;
            var s = CurrentSource;
            if (s == null || _cur == null) return;
            if (s.formula) { _cur.driver.phase = 0f; _cur.driver.headLeanDeg = 0f; _cur.driver.amplitude = s.amp; }
            else _cur.player.Play(s.clip);
            if (_mode == ReviewMode.Action) _paused = false;
            ApplyCurrent();
        }

        // ---- playback -----------------------------------------------------------------------------
        AlphaClip CurClip => CurrentSource != null && !CurrentSource.formula ? _cur.player.Get(CurrentSource.clip) : null;

        static void ApplySource(ReviewSpecimen spec, ReviewSource s, float cursor)
        {
            if (spec != null) spec.Apply(s, cursor);
        }

        void ApplyCurrent()
        {
            if (_cur == null) return;
            ApplySource(_cur, CurrentSource, _cursor);
            for (int i = 0; i < _userHidden.Length; i++)
                if (_userHidden[i]) { var r = _cur.rig.meshes[i].GetComponent<Renderer>(); if (r != null) r.enabled = false; }
        }

        float Advanced(ReviewSpecimen spec, ReviewSource s, float cursor, float dt, ref float hold)
        {
            if (s.formula)
            {
                spec.driver.phase = cursor;
                spec.driver.Step(dt);
                return Mathf.Repeat(spec.driver.phase, Mathf.PI * 2f);
            }
            var c = spec.player.Get(s.clip);
            if (c == null) return cursor;
            var d = c.Data;
            if (d.duration <= 0f) return 0f;
            cursor += d.param == "walk_phase" ? d.phaseRate * dt : dt * 1000f;
            if (d.loop) return Mathf.Repeat(cursor, d.duration);
            if (cursor >= d.duration)
            {
                cursor = d.duration;
                if (_repeat) { hold += dt; if (hold > 0.6f) { hold = 0f; cursor = 0f; } }
            }
            return cursor;
        }

        void Advance(float dt)
        {
            var s = CurrentSource;
            if (_cur == null || s == null) return;
            if (!_paused) _cursor = Advanced(_cur, s, _cursor, dt * _speed, ref _hold);
            ApplyCurrent();
        }

        public float Duration
        {
            get
            {
                var s = CurrentSource;
                if (s == null) return 0f;
                if (s.formula) return Mathf.PI * 2f;
                return CurClip?.Data.duration ?? 0f;
            }
        }

        public void Restart() { _cursor = 0f; _hold = 0f; _paused = false; ApplyCurrent(); }

        public void StepFrame(int dir)
        {
            _paused = true;
            var s = CurrentSource;
            if (s == null) return;
            if (s.formula)
            {
                float rate = AlphaParamWalk.PhaseRate(_cur.driver.Spec.@params, _cur.driver.speedXY);
                _cursor = Mathf.Repeat(_cursor + dir * rate / 60f, Mathf.PI * 2f);
            }
            else
            {
                var keys = CurClip.Data.keys;
                int k = KeyIndex();
                bool onKey = Mathf.Abs(keys[k].x - _cursor) < 1e-4f;
                int target = dir > 0 ? k + 1 : (onKey ? k - 1 : k);
                if (CurClip.Data.loop) target = (target % keys.Length + keys.Length) % keys.Length;
                target = Mathf.Clamp(target, 0, keys.Length - 1);
                _cursor = keys[target].x;
            }
            ApplyCurrent();
        }

        public int KeyIndex()
        {
            var c = CurClip;
            if (c == null) return -1;
            var keys = c.Data.keys;
            int k = 0;
            while (k + 1 < keys.Length && keys[k + 1].x <= _cursor + 1e-4f) k++;
            return k;
        }

        // ---- lineup -------------------------------------------------------------------------------
        public void SetLineup(bool on)
        {
            foreach (var m in _lineupMembers) if (m.spec != null) Destroy(m.spec.gameObject);
            _lineupMembers.Clear();
            _lineup = on;
            if (_cur != null) _cur.gameObject.SetActive(!on);
            if (!on) { if (_cur != null && orbit != null) orbit.Frame(_cur.VisibleBounds()); return; }
            if (_mode == ReviewMode.Action) _mode = ReviewMode.Idle;
            float x = 0f; var all = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
            foreach (var k in _kinds)
            {
                var info = _byKind[k].FirstOrDefault(s => s.IsBaseVariant) ?? _byKind[k][0];
                var spec = ReviewSpecimen.Build(info, voxelMaterial, _stage, _alphaMarkerRule);
                var opts = HairOptions(spec);
                if (opts.Count > 0) spec.SetHairColour(opts[0].colour);
                var src = spec.Sources(_mode).LastOrDefault(s => s.formula) ?? spec.Sources(_mode).FirstOrDefault();
                var m = new LineupMember { spec = spec, src = src };
                ApplySource(spec, src, 0f);
                var b = spec.VisibleBounds();
                spec.transform.position += new Vector3(x - b.min.x, 0, 0);
                x += b.size.x + 0.5f;
                b = spec.VisibleBounds();
                if (first) { all = b; first = false; } else all.Encapsulate(b);
                _lineupMembers.Add(m);
            }
            if (orbit != null) { orbit.SetView(180f, 8f); orbit.Frame(all); }
        }

        void AdvanceLineup(float dt)
        {
            foreach (var m in _lineupMembers)
            {
                if (m.src == null) continue;
                float hold = 0f;
                if (!_paused) m.cursor = Advanced(m.spec, m.src, m.cursor, dt * _speed, ref hold);
                ApplySource(m.spec, m.src, m.cursor);
            }
        }

        public IReadOnlyList<ReviewSpecimen> LineupSpecimens => _lineupMembers.Select(m => m.spec).ToList();

        // ---- overlays (GL) ------------------------------------------------------------------------
        void EnsureLineMaterial()
        {
            if (_lineMat != null) return;
            _lineMat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            _lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            _lineMat.SetInt("_Cull", 0);
        }

        void OnRenderObject()
        {
            if (Camera.current == null || (Camera.current.cameraType != CameraType.Game && Camera.current.cameraType != CameraType.SceneView)) return;
            EnsureLineMaterial();
            _lineMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            if (_showGrid)
            {
                // 1 Unity unit = 10 voxels; fine lines every voxel near the specimen
                var c = _cur != null && !_lineup ? _cur.VisibleBounds().center : Vector3.zero;
                float y = _groundY;
                int half = _lineup ? 40 : 20;
                for (int i = -half; i <= half; i++)
                {
                    float step = _lineup ? 1f : 0.1f;
                    GL.Color(i % 10 == 0 ? new Color(0.25f, 0.3f, 0.25f, 1f) : new Color(0.45f, 0.55f, 0.45f, 1f));
                    float cx = Mathf.Round(c.x / step) * step, cz = Mathf.Round(c.z / step) * step;
                    GL.Vertex3(cx + i * step, y, cz - half * step); GL.Vertex3(cx + i * step, y, cz + half * step);
                    GL.Vertex3(cx - half * step, y, cz + i * step); GL.Vertex3(cx + half * step, y, cz + i * step);
                }
            }
            if (_cur != null && !_lineup && _cur.rig != null)
            {
                if (_showBounds)
                {
                    var b = _cur.VisibleBounds();
                    GL.Color(new Color(1f, 1f, 0.2f, 1f));
                    Box(b.min, b.max);
                }
                if (_showPivots)
                    for (int i = 0; i < _cur.rig.parts.Length; i++)
                    {
                        var t = _cur.rig.parts[i];
                        float L = 0.25f;
                        GL.Color(Color.red); GL.Vertex(t.position); GL.Vertex(t.position + t.right * L);
                        GL.Color(Color.green); GL.Vertex(t.position); GL.Vertex(t.position + t.up * L);
                        GL.Color(Color.blue); GL.Vertex(t.position); GL.Vertex(t.position + t.forward * L);
                    }
            }
            GL.End();
            GL.PopMatrix();
        }

        static void Box(Vector3 a, Vector3 b)
        {
            var p = new[]
            {
                new Vector3(a.x, a.y, a.z), new Vector3(b.x, a.y, a.z), new Vector3(b.x, a.y, b.z), new Vector3(a.x, a.y, b.z),
                new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, b.z)
            };
            int[] e = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < e.Length; i++) GL.Vertex(p[e[i]]);
        }

        // ---- checks shown in the UI ---------------------------------------------------------------
        /// <summary>Mirror pairs of the entity contract: |x_l + x_r|, |y_l - y_r|, |z_l - z_r| in voxels (root space).</summary>
        public List<string> MirrorReport()
        {
            var lines = new List<string>();
            if (_cur?.contract?.parts == null) return lines;
            var done = new HashSet<string>();
            foreach (var p in _cur.contract.parts)
            {
                if (string.IsNullOrEmpty(p.mirrorOf) || done.Contains(p.name)) continue;
                done.Add(p.name); done.Add(p.mirrorOf);
                int a = _cur.rig.IndexOf(p.name), b = _cur.rig.IndexOf(p.mirrorOf);
                if (a < 0 || b < 0) { lines.Add($"{p.name}/{p.mirrorOf}: часть отсутствует в риге"); continue; }
                var pa = _cur.rig.parts[a].localPosition; var pb = _cur.rig.parts[b].localPosition;
                var sa = _cur.rig.parts[a].localScale; var sb = _cur.rig.parts[b].localScale;
                string flip = (sa.x * sa.y * sa.z < 0) || (sb.x * sb.y * sb.z < 0) ? " отражённый масштаб" : "";
                lines.Add($"{p.name}/{p.mirrorOf}: Δx {Mathf.Abs(pa.x + pb.x):0.00}  Δy {Mathf.Abs(pa.y - pb.y):0.00}  Δz {Mathf.Abs(pa.z - pb.z):0.00} вокс{flip}");
            }
            return lines;
        }

        public List<string> TintReport()
        {
            var lines = new List<string>();
            if (_cur?.rig == null) return lines;
            for (int i = 0; i < _cur.markers.Length; i++)
            {
                var m = _cur.markers[i];
                if (m[0] + m[1] + m[2] == 0) continue;
                lines.Add($"{_cur.rig.partNames[i]}: чистый R {m[0]}, G {m[1]}, B {m[2]} вокс.");
            }
            return lines;
        }

        // ---- hair colour (D61) -------------------------------------------------------------------
        /// <summary>Hair colour choices: live samples of this kind first, then live samples of other
        /// kinds, then identity (x1, "not set"). Never an invented colour.</summary>
        /// <summary>Hair colour choices by the Alpha rule of the kind (pipeline voxel_markers.hair_rule):
        /// FIXED_BY_KIND -> only the fixed colour; RANDOM_PER_NPC -> live samples of this kind, NPCs drawn
        /// by the server rule (MSVC rand stream, Server.exe 0x40A840), live samples of other kinds and x1.
        /// Never an invented colour.</summary>
        public List<HairOption> HairOptions(ReviewSpecimen spec)
        {
            var list = new List<HairOption>();
            if (spec == null || spec.hairPart < 0) return list;
            if (spec.HairFixedByKind)
            {
                var c = spec.partSpecs[spec.hairPart].TintColor;
                list.Add(new HairOption { label = $"фиксирован видом ({c.r * 255:0},{c.g * 255:0},{c.b * 255:0}): клиент не берёт цвет спавна", colour = c, observedSameKind = true });
                return list;
            }
            var kind = spec.info.kind;
            var samples = _index?.hairSamples ?? new ReviewHairSample[0];
            var keys = new HashSet<string>();
            foreach (var h in samples.Where(h => h.kind == kind && h.hair_rgb.Length == 3))
                if (keys.Add(string.Join(",", h.hair_rgb)))
                    list.Add(new HairOption { label = "живой NPC этого вида: " + h.Label, colour = h.Colour, observedSameKind = true });
            // server rule: consecutive NPC spawns from the MSVC rand stream (state 1 = CRT default seed)
            uint state = 1;
            for (int n = 1; n <= 8; n++)
            {
                var r = new int[5];
                for (int k = 0; k < 5; k++) { state = unchecked(state * 214013u + 2531011u); r[k] = (int)((state >> 16) & 0x7fff); }
                int R = r[2] & 255, G = r[1] & 255, B = r[0] & 255;
                list.Add(new HairOption
                {
                    label = $"правило сервера, NPC #{n} (rand с srand(1)): ({R},{G},{B}), вариант s{r[3] % 100}-{r[4] % 100}",
                    colour = new Color(R / 255f, G / 255f, B / 255f, 1f)
                });
            }
            foreach (var h in samples.Where(h => h.kind != kind && h.hair_rgb.Length == 3))
                if (keys.Add(string.Join(",", h.hair_rgb)))
                    list.Add(new HairOption { label = "живой NPC другого вида: " + h.Label, colour = h.Colour });
            list.Add(new HairOption { label = "×1 — цвет не задан (исходные цвета модели)", colour = Color.white, identity = true });
            return list;
        }

        void ApplyHairChoice(ReviewSpecimen s)
        {
            if (s == null || s.hairPart < 0 || _hairOptions.Count == 0) return;
            _hairChoice = Mathf.Clamp(_hairChoice, 0, _hairOptions.Count - 1);
            s.SetHairColour(_hairOptions[_hairChoice].colour);
        }

        public void SetHairChoice(int i) { _hairChoice = i; ApplyHairChoice(_cur); }
        public int HairOptionCount => _hairOptions.Count;

        public bool AlphaMarkerRule
        {
            get => _alphaMarkerRule;
            set
            {
                if (_alphaMarkerRule == value) return;
                _alphaMarkerRule = value;
                if (_lineup) { SetLineup(true); return; }
                var keepSrc = _srcIdx; var keepMode = _mode;
                Select(_kindIdx, _varIdx);
                if (keepMode != _mode) SetMode(keepMode);
                SelectSource(keepSrc);
            }
        }

        // ---- smoke tour ---------------------------------------------------------------------------
        IEnumerator Tour()
        {
            SmokeLog.Clear(); SmokeSpecimens = SmokeSources = SmokeFrames = 0; SmokeDone = false;
            yield return null;
            for (int k = 0; k < KindCount; k++)
                for (int v = 0; v < VariantCount(k); v++)
                {
                    Select(k, v);
                    SmokeSpecimens++;
                    if (_cur.problems.Count > 0) SmokeLog.Add($"PROBLEMS {_cur.info.id}: {string.Join("; ", _cur.problems)}");
                    foreach (Tab t in Enum.GetValues(typeof(Tab))) { _tab = t; yield return null; SmokeFrames++; }
                    foreach (ReviewMode mode in Enum.GetValues(typeof(ReviewMode)))
                    {
                        SetMode(mode);
                        for (int s = 0; s < SourceCount; s++)
                        {
                            SelectSource(s);
                            SmokeSources++;
                            for (int f = 0; f < 2; f++) { yield return null; SmokeFrames++; }
                            StepFrame(1); StepFrame(-1);
                            foreach (var t in _cur.rig.parts)
                                if (!IsFinite(t.localPosition) || !IsFinite(t.localScale) || float.IsNaN(t.localRotation.x))
                                { SmokeLog.Add($"NON_FINITE_POSE {_cur.info.id} {mode} {CurrentSource.label} {t.name}"); break; }
                        }
                    }
                    if (_cur.hairPart >= 0)
                    {
                        for (int h = 0; h < HairOptionCount; h++)
                        {
                            SetHairChoice(h);
                            yield return null; SmokeFrames++;
                            if (_cur.HairColour != _hairOptions[h].colour) SmokeLog.Add($"HAIR_TINT_NOT_APPLIED {_cur.info.id} {h}");
                        }
                        SetHairChoice(0);
                    }
                }
            // before/after toggle on a specimen with hair markers
            Select(0, 0);
            int after = _cur.rig.meshes.Sum(m => m.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            AlphaMarkerRule = false; yield return null; SmokeFrames++;
            int before = _cur.rig.meshes.Sum(m => m.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            AlphaMarkerRule = true; yield return null; SmokeFrames++;
            if (_cur.hairPart >= 0 && before == after) SmokeLog.Add("MARKER_RULE_TOGGLE_HAS_NO_EFFECT");
            SmokeMarkerToggle = $"{before}->{after}";
            SetLineup(true);
            for (int f = 0; f < 5; f++) { yield return null; SmokeFrames++; }
            if (_lineupMembers.Count != KindCount) SmokeLog.Add($"LINEUP_COUNT {_lineupMembers.Count} != {KindCount}");
            SetLineup(false);
            SmokeDone = true;
        }

        public static string SmokeMarkerToggle = "";

        static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                                             float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        // ---- UI -----------------------------------------------------------------------------------
        GUIStyle _wrap, _bold, _small, _tabStyle;

        void Styles()
        {
            if (_wrap != null) return;
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
            _bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = true, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, richText = true };
            _tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, padding = new RectOffset(3, 3, 3, 3) };
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        static string Tag(string status) => $"<color=#{Hex(ReviewData.StatusColor(status))}>{status}</color>";

        void OnGUI()
        {
            Styles();
            float scale = uiScale > 0 ? uiScale : ReviewLayout.AutoScale(Screen.width, Screen.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _layout = ReviewLayout.Compute(Screen.width, Screen.height, scale, _panelOpen);
            HandleInput(Event.current);
            _panels.Clear();
            if (_hideUI) { GUI.Label(new Rect(8, _layout.height - 24, 400, 22), "H — показать интерфейс"); return; }
            if (_kinds.Count == 0)
            {
                var r = new Rect(10, 10, Mathf.Min(640, _layout.width - 20), Mathf.Min(300, _layout.height - 20)); _panels.Add(r);
                GUILayout.BeginArea(r, GUI.skin.box);
                GUILayout.Label("<b>Нет данных просмотра.</b> Запустите tools/alpha_models/review/setup_review.ps1.", _wrap);
                foreach (var p in _loadProblems) GUILayout.Label(p, _wrap);
                GUILayout.EndArea();
                return;
            }
            Panel(_layout.panel);
            if (_layout.status.width > 0) StatusBar(_layout.status);
            if (_lineup) LineupBar(_layout.timeline); else Timeline(_layout.timeline);
        }

        void HandleInput(Event e)
        {
            bool overUI = !_hideUI && _panels.Any(r => r.Contains(e.mousePosition));
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (overUI) return;
                    _dragging = true; _dragButton = e.shift ? 2 : e.button; e.Use(); break;
                case EventType.MouseUp:
                    if (_dragging) { _dragging = false; e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (!_dragging || orbit == null) return;
                    if (_dragButton == 2) orbit.Pan(e.delta.x, e.delta.y); else orbit.Rotate(e.delta.x, e.delta.y);
                    e.Use(); break;
                case EventType.ScrollWheel:
                    if (overUI || orbit == null) return;
                    orbit.Zoom(e.delta.y > 0 ? 1 : -1); e.Use(); break;
                case EventType.KeyDown:
                    if (Key(e.keyCode)) e.Use();
                    break;
            }
        }

        bool Key(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.UpArrow: Select(_kindIdx - 1, 0); return true;
                case KeyCode.DownArrow: Select(_kindIdx + 1, 0); return true;
                case KeyCode.LeftArrow: Select(_kindIdx, _varIdx - 1); return true;
                case KeyCode.RightArrow: Select(_kindIdx, _varIdx + 1); return true;
                case KeyCode.Alpha1: case KeyCode.Keypad1: SetModeUI(ReviewMode.Idle); return true;
                case KeyCode.Alpha2: case KeyCode.Keypad2: SetModeUI(ReviewMode.Walk); return true;
                case KeyCode.Alpha3: case KeyCode.Keypad3: SetModeUI(ReviewMode.Action); return true;
                case KeyCode.Q: if (!_lineup) SelectSource(_srcIdx - 1); return true;
                case KeyCode.E: if (!_lineup) SelectSource(_srcIdx + 1); return true;
                case KeyCode.Space: _paused = !_paused; return true;
                case KeyCode.R: Restart(); return true;
                case KeyCode.Comma: if (!_lineup) StepFrame(-1); return true;
                case KeyCode.Period: if (!_lineup) StepFrame(1); return true;
                case KeyCode.Minus: case KeyCode.KeypadMinus: _speed = Mathf.Max(0.05f, _speed * 0.5f); return true;
                case KeyCode.Equals: case KeyCode.KeypadPlus: _speed = Mathf.Min(4f, _speed * 2f); return true;
                case KeyCode.F: if (orbit != null) orbit.Frame(_lineup ? LineupBounds() : _cur.VisibleBounds()); return true;
                case KeyCode.L: SetLineup(!_lineup); return true;
                case KeyCode.P: _showPivots = !_showPivots; return true;
                case KeyCode.B: _showBounds = !_showBounds; return true;
                case KeyCode.G: _showGrid = !_showGrid; return true;
                case KeyCode.M: AlphaMarkerRule = !AlphaMarkerRule; return true;
                case KeyCode.C: if (_cur != null && _cur.hairPart >= 0) SetHairChoice((_hairChoice + 1) % Mathf.Max(1, HairOptionCount)); return true;
                case KeyCode.Tab: _panelOpen = !_panelOpen; return true;
                case KeyCode.H: _hideUI = !_hideUI; return true;
                case KeyCode.F1: _panelOpen = true; _tab = Tab.Help; return true;
            }
            return false;
        }

        Bounds LineupBounds()
        {
            var b = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
            foreach (var m in _lineupMembers) { var x = m.spec.VisibleBounds(); if (first) { b = x; first = false; } else b.Encapsulate(x); }
            return b;
        }

        void SetModeUI(ReviewMode m)
        {
            if (_lineup)
            {
                if (m == ReviewMode.Action) return;      // lineup = Idle/Walk of all kinds
                _mode = m; SetLineup(true); return;
            }
            SetMode(m);
        }

        static readonly string[] TabNames = { "Вид", "Анимация", "Части/цвет", "Проверки", "Справка" };

        void Panel(Rect r)
        {
            _panels.Add(r);
            GUILayout.BeginArea(r, GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_panelOpen ? "◀" : "▶ панель", _tabStyle, GUILayout.Width(_panelOpen ? 24 : 100))) _panelOpen = !_panelOpen;
            if (_panelOpen)
                for (int t = 0; t < TabNames.Length; t++)
                    if (GUILayout.Toggle(_tab == (Tab)t, TabNames[t], _tabStyle) && _tab != (Tab)t) _tab = (Tab)t;
            GUILayout.EndHorizontal();
            if (_panelOpen)
            {
                _scrollInfo = GUILayout.BeginScrollView(_scrollInfo);
                switch (_tab)
                {
                    case Tab.Kind: KindTab(); break;
                    case Tab.Animation: AnimationTab(); break;
                    case Tab.Parts: PartsTab(); break;
                    case Tab.Checks: ChecksTab(); break;
                    default: HelpTab(); break;
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        void ModeButtons()
        {
            GUILayout.BeginHorizontal();
            foreach (ReviewMode m in Enum.GetValues(typeof(ReviewMode)))
            {
                string label = m == ReviewMode.Idle ? "1 Idle" : m == ReviewMode.Walk ? "2 Walk" : "3 Action";
                GUI.enabled = !(_lineup && m == ReviewMode.Action);
                if (GUILayout.Toggle(_mode == m, label, GUI.skin.button) && _mode != m) SetModeUI(m);
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }

        void KindTab()
        {
            if (GUILayout.Toggle(_lineup, $"Все виды в ряд ({_kinds.Count}) (L)", GUI.skin.button) != _lineup) SetLineup(!_lineup);
            ModeButtons();
            if (!_lineup && _cur != null)
            {
                var vars = _byKind[_kinds[_kindIdx]];
                GUILayout.Label($"<b>Вариант (←/→): {_varIdx + 1}/{vars.Count}</b>", _wrap);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", GUILayout.Width(32))) Select(_kindIdx, _varIdx - 1);
                GUILayout.Label(vars[_varIdx].VariantLabel, _small);
                if (GUILayout.Button("▶", GUILayout.Width(32))) Select(_kindIdx, _varIdx + 1);
                GUILayout.EndHorizontal();
                GUILayout.Label($"голова: {_cur.ModelOf("head")} · волосы: {_cur.ModelOf("hair")}", _small);
            }
            GUILayout.Label($"<b>Виды (↑/↓), {_kinds.Count}:</b>", _wrap);
            for (int k = 0; k < _kinds.Count; k++)
            {
                var first = _byKind[_kinds[k]][0];
                bool sel = k == _kindIdx && !_lineup;
                string acts = _byKind[_kinds[k]].Any(s => s.actionsStatus == "AVAILABLE" && s.controllerMode == "ACTIONS") ? " · действия" : "";
                if (GUILayout.Toggle(sel, $"{first.kind} {first.name} ({_byKind[_kinds[k]].Count} вар.){acts}", GUI.skin.button) && !sel)
                    Select(k, 0);
            }
            if (_lineup) LineupInfo();
        }

        void AnimationTab()
        {
            ModeButtons();
            if (_lineup) { GUILayout.Label("В режиме ряда — только Idle/Walk всех видов. Выйдите из ряда (L) для действий.", _wrap); return; }
            GUILayout.Label(_mode == ReviewMode.Action ? $"<b>Действия по ID ({_sources.Count}), Q/E:</b>" : $"<b>Источник ({_sources.Count}), Q/E:</b>", _wrap);
            if (_sources.Count == 0)
            {
                if (_mode == ReviewMode.Action) ActionsMissingNote();
                else GUILayout.Label("<color=#ff6666>Нет импортированного источника для этого режима.</color>", _wrap);
            }
            for (int i = 0; i < _sources.Count; i++)
            {
                var s = _sources[i];
                var prev = GUI.contentColor;
                GUI.contentColor = ReviewData.StatusColor(s.status);
                if (GUILayout.Toggle(i == _srcIdx, s.label, GUI.skin.button) && i != _srcIdx) SelectSource(i);
                GUI.contentColor = prev;
            }
            if (_mode == ReviewMode.Action && _cur.actions?.blockedActions != null && _cur.actions.blockedActions.Length > 0)
            {
                GUILayout.Label("<b>Не импортированы (BLOCKED):</b>", _wrap);
                foreach (var b in _cur.actions.blockedActions)
                    GUILayout.Label($"<color=#ff7777>ID {b.action:000}</color> — {b.reason}", _small);
            }
            ClipInfo();
        }

        void ActionsMissingNote()
        {
            var i = _cur.info;
            bool kindHasActions = _byKind[_kinds[_kindIdx]].Any(s => s.controllerMode == "ACTIONS" && s.actionsStatus == "AVAILABLE");
            if (kindHasActions)
                GUILayout.Label("<color=#ffb347>Для этого варианта библиотека действий не импортирована.</color> Действия доказаны и запечены только " +
                                "на образце своего вида (seed 0-0, D60); у HumanMale — на варианте «s0-0 + библиотека игрока».", _wrap);
            else
                GUILayout.Label($"<color=#ff6666>Действий нет:</color> {i.actionsStatus} {i.actionsReason}", _wrap);
        }

        void ClipInfo()
        {
            var s = CurrentSource;
            GUILayout.Space(4);
            GUILayout.Label("<b>Клип / происхождение</b>", _wrap);
            if (s == null) { GUILayout.Label("<color=#ff6666>нет источника</color>", _wrap); return; }
            GUILayout.Label(Tag(s.status) + (string.IsNullOrEmpty(s.category) ? "" : $" · {s.category}"), _bold);
            if (s.formula)
            {
                GUILayout.Label($"Формула drawModel (ходьба) из параметров внешности; скорость {_cur.driver.speedXY} — PLACEHOLDER_NOT_ALPHA.", _small);
                return;
            }
            if (CurClip == null) return;
            var d = CurClip.Data;
            GUILayout.Label($"{d.param}, длительность {d.duration:0.###}, ключей {d.keys.Length}, loop {d.loop}" +
                            (d.actionCode >= 0 ? $"\nдействие ID {d.actionCode}, конец {d.actionEndMs} мс, восстановление с {d.recoveryStartMs} мс" : "") +
                            (d.visibilityMode != "STATIC_ALL" ? $"\nвидимость частей: {d.visibilityMode}" : ""), _small);
            if (d.status == "TRANSFERRED_FROM_ALPHA" && !string.IsNullOrEmpty(d.provenance))
            {
                ReviewCaptureInfo cap = null;
                try { cap = JsonUtility.FromJson<ReviewCaptureInfo>(d.provenance); } catch (Exception) { }
                if (cap != null && !string.IsNullOrEmpty(cap.kind))
                {
                    bool other = !string.Equals(cap.kind, _cur.info.kind, StringComparison.OrdinalIgnoreCase);
                    GUILayout.Label($"живой захват Alpha: вид {cap.kind}, {cap.capture}, кадров {cap.frames_used}" +
                                    (other ? $"\n<color=#ffb347>захвачен на другом виде того же семейства ({cap.kind} → {_cur.info.kind})</color>" : ""), _small);
                }
            }
            if (!string.IsNullOrEmpty(d.provenance))
                GUILayout.Label("provenance: " + (d.provenance.Length > 300 ? d.provenance.Substring(0, 300) + "…" : d.provenance), _small);
        }

        void PartsTab()
        {
            if (_lineup || _cur == null) { GUILayout.Label("Выберите один вид (выйдите из ряда).", _wrap); return; }
            GUILayout.Label("<b>Цвет (D61)</b>", _wrap);
            bool rule = GUILayout.Toggle(_alphaMarkerRule, " правило маркеров Alpha (M); выкл = как до исправления");
            if (rule != _alphaMarkerRule) AlphaMarkerRule = rule;
            if (_cur.hairPart >= 0)
            {
                GUILayout.Label(_cur.HairFixedByKind
                    ? "Цвет волос этого вида фиксирован (клиент не копирует цвет спавна) — доказано исполнением кода."
                    : "Цвет волос — случайный для каждого NPC: сервер берёт 3 значения rand() (Server.exe 0x40A840). Варианты (C):", _small);
                for (int h = 0; h < _hairOptions.Count; h++)
                {
                    var o = _hairOptions[h];
                    GUILayout.BeginHorizontal();
                    var prev = GUI.color; GUI.color = o.colour;
                    GUILayout.Box("", GUILayout.Width(16), GUILayout.Height(16));
                    GUI.color = prev;
                    if (GUILayout.Toggle(h == _hairChoice, o.label, GUI.skin.button) && h != _hairChoice) SetHairChoice(h);
                    GUILayout.EndHorizontal();
                }
            }
            else GUILayout.Label("У вида нет блока волос: статичного tint нет, цвета моделей настоящие.", _small);

            GUILayout.Space(4);
            GUILayout.Label("<b>Части</b> (галочка — показать)", _wrap);
            for (int p = 0; p < _cur.rig.partNames.Length; p++)
            {
                var spec = _cur.partSpecs[p];
                var m = _cur.markers[p];
                bool clipVisible = _cur.rig.IsVisible(p) || _userHidden[p];
                GUILayout.BeginHorizontal();
                bool show = GUILayout.Toggle(!_userHidden[p], "", GUILayout.Width(18));
                if (show == _userHidden[p]) { _userHidden[p] = !show; ApplyCurrent(); }
                string mk = m[0] + m[1] + m[2] == 0 ? "" : _cur.markerTransparent[p]
                    ? $" маркеры R{m[0]} G{m[1]} B{m[2]} " + (_alphaMarkerRule ? "<color=#80ff80>скрыты (флаг 0)</color>" : "<color=#ff8080>показаны (до исправления)</color>")
                    : $" чистые R{m[0]} G{m[1]} B{m[2]}: <color=#80ff80>настоящие цвета (флаг 1)</color>";
                string hid = clipVisible ? "" : " <color=#aaaaaa>(скрыта клипом)</color>";
                var hc = _cur.HairColour;
                string tint = spec.name == "hair" ? $" ×({hc.r * 255:0},{hc.g * 255:0},{hc.b * 255:0})" : "";
                GUILayout.Label($"<b>{spec.name}</b> {spec.model} {spec.size[0]}×{spec.size[1]}×{spec.size[2]} · {spec.anchor}{(spec.mirrored ? " · mirrored" : "")}{tint}{mk}{hid}", _small);
                GUILayout.EndHorizontal();
            }
        }

        void ChecksTab()
        {
            if (_lineup || _cur == null) { LineupInfo(); return; }
            var i = _cur.info;
            GUILayout.Label($"<b>{i.name} {i.kind}</b> · {i.VariantLabel}", _wrap);
            GUILayout.Label($"семейство: {i.family}\nисточник: {i.source}", _small);
            if (_cur.contract != null)
                GUILayout.Label($"категория: {_cur.contract.category} · контроллер: {_cur.contract.controllerMode} · переходы: {_cur.contract.transitions}", _small);
            if (!string.IsNullOrEmpty(i.actionProfile))
                GUILayout.Label($"профиль: {i.actionProfile}\nзапуск: {i.trigger} · live_check: <color=#ffb347>{(string.IsNullOrEmpty(i.liveCheck) ? "—" : i.liveCheck)}</color> · экипировка NPC наблюдалась: {(i.equipmentObserved ? "да" : "нет")}", _small);
            GUILayout.BeginHorizontal();
            _showPivots = GUILayout.Toggle(_showPivots, "оси (P)");
            _showBounds = GUILayout.Toggle(_showBounds, "габарит (B)");
            _showGrid = GUILayout.Toggle(_showGrid, "сетка (G)");
            GUILayout.EndHorizontal();
            var bb = _cur.VisibleBounds();
            GUILayout.Label($"габарит (вокс.): {bb.size.x / AlphaTransferConsts.RootScale:0.0} × {bb.size.y / AlphaTransferConsts.RootScale:0.0} × {bb.size.z / AlphaTransferConsts.RootScale:0.0}; сетка: тонкая = 1 вокс., толстая = 10", _small);
            var mirror = MirrorReport();
            if (mirror.Count > 0)
            {
                GUILayout.Label("Зеркальные пары (текущий кадр; в стойке Δ≈0):", _small);
                foreach (var l in mirror) GUILayout.Label("  " + l, _small);
            }
            if (_cur.problems.Count > 0 || i.problems.Length > 0)
            {
                GUILayout.Label("<color=#ff6666><b>Проблемы загрузки:</b></color>", _wrap);
                foreach (var p in _cur.problems.Concat(i.problems)) GUILayout.Label("  " + p, _small);
            }
            if (i.skippedClips.Length > 0)
            {
                GUILayout.Label("Не импортированы конвейером:", _small);
                foreach (var p in i.skippedClips) GUILayout.Label("  " + p, _small);
            }
            GUILayout.Label("<b>Камера</b>", _wrap);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Спереди")) orbit.SetView(180f, 5f);
            if (GUILayout.Button("Сбоку")) orbit.SetView(90f, 5f);
            if (GUILayout.Button("Сзади")) orbit.SetView(0f, 5f);
            if (GUILayout.Button("Сверху")) orbit.SetView(180f, 89f);
            GUILayout.EndHorizontal();
        }

        void HelpTab()
        {
            GUILayout.Label(
                "<b>Управление</b> (щёлкните по Game view для фокуса)\n" +
                "↑/↓ — вид · ←/→ — вариант · 1/2/3 — Idle/Walk/Action · Q/E — клип/действие\n" +
                "Space — пауза · R — сначала · , и . — кадр · − и = — скорость\n" +
                "L — все виды в ряд · F — показать целиком · P/B/G — оси/габарит/сетка\n" +
                "M — правило маркеров Alpha вкл/выкл (до/после) · C — следующий цвет волос\n" +
                "Tab — свернуть панель · H — скрыть интерфейс\n" +
                "Мышь: ЛКМ/ПКМ — вращение, СКМ или Shift+ЛКМ — сдвиг, колесо — зум.\n\n" +
                "<b>Статусы:</b> " + Tag("TRANSFERRED_FROM_ALPHA") + " — живой захват; " + Tag("EMULATED_ALPHA_CODE") +
                " — исполнение кода Alpha, без живой проверки; " + Tag("RECONSTRUCTED_FROM_EVIDENCE") + " — формула ходьбы.\n" +
                "Смена источника — жёсткая смена позы (переходы не показываются).", _wrap);
        }

        void LineupInfo()
        {
            GUILayout.Label($"<b>Ряд: {_lineupMembers.Count} видов (seed 0-0)</b>, режим {_mode}", _wrap);
            foreach (var m in _lineupMembers)
            {
                var b = m.spec.VisibleBounds();
                GUILayout.Label($"{m.spec.info.kind} {m.spec.info.name}: высота {b.size.y / AlphaTransferConsts.RootScale:0.0} вокс · " +
                                (m.src != null ? Tag(m.src.status) : "<color=#ff6666>нет источника</color>"), _small);
            }
        }

        void StatusBar(Rect r)
        {
            _panels.Add(r);
            GUILayout.BeginArea(r, GUI.skin.box);
            string rule = _alphaMarkerRule ? "" : " · <color=#ff8080>маркеры показаны (до исправления)</color>";
            if (_lineup) GUILayout.Label($"<b>Ряд из {_lineupMembers.Count} видов</b> · {_mode}{rule}", _small);
            else if (_cur != null)
            {
                var s = CurrentSource;
                GUILayout.Label($"<b>{_cur.info.name} {_cur.info.kind}</b> {_cur.info.VariantLabel} · {_mode} · {(s != null ? s.label + " " + Tag(s.status) : "—")}{rule}", _small);
            }
            GUILayout.EndArea();
        }

        void Timeline(Rect r)
        {
            _panels.Add(r);
            GUILayout.BeginArea(r, GUI.skin.box);
            var s = CurrentSource;
            float dur = Duration;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_paused ? "▶" : "❚❚", GUILayout.Width(32))) _paused = !_paused;
            if (GUILayout.Button("⟲", GUILayout.Width(32))) Restart();
            if (GUILayout.Button("◀|", GUILayout.Width(32))) StepFrame(-1);
            if (GUILayout.Button("|▶", GUILayout.Width(32))) StepFrame(1);
            _repeat = GUILayout.Toggle(_repeat, "повтор", GUILayout.Width(66));
            GUILayout.Label($"×{_speed:0.00}", GUILayout.Width(44));
            float sp = GUILayout.HorizontalSlider(_speed, 0.05f, 2f, GUILayout.MinWidth(40));
            if (Mathf.Abs(sp - _speed) > 1e-4f) _speed = sp;
            GUILayout.EndHorizontal();
            if (s != null && dur > 0f)
            {
                float c = GUILayout.HorizontalSlider(_cursor, 0f, dur);
                if (Mathf.Abs(c - _cursor) > 1e-5f) { _cursor = c; _paused = true; ApplyCurrent(); }
            }
            string pos;
            if (s == null) pos = "—";
            else if (s.formula) pos = $"фаза {_cursor:0.000} / 2π рад";
            else if (CurClip != null && CurClip.Data.param == "walk_phase") pos = $"фаза {_cursor:0.000} / {dur:0.000} · ключ {KeyIndex() + 1}/{CurClip.Data.keys.Length}";
            else if (CurClip != null) pos = $"{_cursor:0} / {dur:0} мс · ключ {KeyIndex() + 1}/{CurClip.Data.keys.Length}";
            else pos = "—";
            GUILayout.Label(pos, _small);
            GUILayout.EndArea();
        }

        void LineupBar(Rect r)
        {
            _panels.Add(r);
            GUILayout.BeginArea(r, GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_paused ? "▶" : "❚❚", GUILayout.Width(32))) _paused = !_paused;
            GUILayout.Label($"×{_speed:0.00}", GUILayout.Width(44));
            _speed = GUILayout.HorizontalSlider(_speed, 0.05f, 2f);
            if (GUILayout.Button("Выйти из ряда (L)", GUILayout.Width(130))) SetLineup(false);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
