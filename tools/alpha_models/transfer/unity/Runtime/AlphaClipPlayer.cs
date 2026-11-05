// AlphaClipPlayer.cs — plays AlphaClips on an AlphaRig in PlayMode (and on demand in EditMode).
// time clips advance in game milliseconds; walk clips advance the walk phase at the captured
// rate (rad/s), exactly how Alpha advances creature+0x118c while walking.
// Optional look pitch (degrees) re-applies Alpha's aim layer: head/hair rotate about the neck
// (3 voxels below the joint), the weapon in the hand by half the pitch about the grip.
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [RequireComponent(typeof(AlphaRig))]
    public class AlphaClipPlayer : MonoBehaviour
    {
        public TextAsset[] clipAssets = new TextAsset[0];
        public string playing;
        public float speed = 1f;
        public float lookPitchDeg;
        public bool autoPlay = true;

        readonly Dictionary<string, AlphaClip> _clips = new Dictionary<string, AlphaClip>();
        readonly Dictionary<string, int[]> _maps = new Dictionary<string, int[]>();
        AlphaRig _rig;
        AlphaClip _current;
        PartPose[] _buffer = new PartPose[0];
        public float Cursor { get; private set; }
        public int FramesApplied { get; private set; }
        public List<string> Problems { get; } = new List<string>();

        public AlphaClip Current => _current;

        public void LoadClips()
        {
            _rig = GetComponent<AlphaRig>();
            _clips.Clear(); _maps.Clear(); Problems.Clear(); _current = null; Cursor = 0;
            foreach (var asset in clipAssets)
            {
                if (asset == null) { Problems.Add("CLIP_ASSET_NULL"); continue; }
                var clip = AlphaClip.Parse(asset.text, string.IsNullOrEmpty(_rig.rigId) ? AlphaRig.RigId : _rig.rigId, Problems);
                if (clip == null) continue;
                var map = new int[clip.PartCount];
                for (int i = 0; i < map.Length; i++)
                {
                    map[i] = _rig.IndexOf(clip.Data.parts[i]);
                    if (map[i] < 0) Problems.Add($"CLIP_PART_NOT_IN_RIG {clip.Data.name} {clip.Data.parts[i]}");
                }
                if (_clips.ContainsKey(clip.Data.name)) { Problems.Add($"CLIP_NAME_DUPLICATE {clip.Data.name}"); continue; }
                _clips[clip.Data.name] = clip;
                _maps[clip.Data.name] = map;
            }
        }

        public IEnumerable<string> ClipNames => _clips.Keys;
        public AlphaClip Get(string name) => name != null && _clips.TryGetValue(name, out var c) ? c : null;

        public bool Play(string name, float cursor = 0f)
        {
            if (_clips.Count == 0) LoadClips();
            if (!_clips.TryGetValue(name, out var clip)) { Problems.Add($"CLIP_NOT_FOUND {name}"); return false; }
            _current = clip;
            playing = name;
            Cursor = cursor;
            if (_buffer.Length != clip.PartCount) _buffer = new PartPose[clip.PartCount];
            _rig.SetWeaponOnBack(clip.Data.weaponOnBack);
            ApplyAt(Cursor);
            return true;
        }

        /// <summary>Pose the rig at a clip parameter (ms or phase radians).</summary>
        public void ApplyAt(float x)
        {
            if (_current == null) return;
            _current.Sample(x, _buffer);
            _rig.Apply(_buffer, _maps[_current.Data.name]);
            if (Mathf.Abs(lookPitchDeg) > 1e-6f) ApplyLookPitch(lookPitchDeg);
            FramesApplied++;
        }

        void ApplyLookPitch(float pitch)
        {
            // Alpha X axis == Unity X axis (only Y/Z swap), so the pitch is a rotation about the
            // part's own local X; the pivot is the neck (Alpha (0,0,-3) -> Unity (0,-3,0)).
            Pitch("head", pitch, 3f);
            Pitch("hair", pitch, 3f);
            if (!_rig.WeaponOnBack) Pitch("weapon", pitch * 0.5f, 0f);
        }

        void Pitch(string part, float deg, float pivotVox)
        {
            int i = _rig.IndexOf(part);
            if (i < 0) return;
            var t = _rig.parts[i];
            // J' = T(0,0,p) Rx(k*pitch) T(0,0,-p) J  (Alpha row-vector) == rotate in the part frame
            // J = S*T(0,0,3)*Rx*... -> the neck pivot is at 3/s in the part's local frame
            // (= 3 rig voxels after the part scale s is applied).
            float s = Mathf.Abs(t.localScale.y);
            var pivotLocal = new Vector3(0f, -pivotVox / (s > 1e-6f ? s : 1f), 0f);
            var pivotWorld = t.TransformPoint(pivotLocal);
            // Alpha Rx(+a) turns +Y(forward) toward +Z(up); after the Y/Z swap that is Unity's
            // AngleAxis(-a, X). Checked against the Alpha-space formula by the self-tests.
            t.RotateAround(pivotWorld, t.TransformDirection(Vector3.right), -deg);
        }

        void Start()
        {
            LoadClips();
            if (autoPlay && !string.IsNullOrEmpty(playing)) Play(playing);
        }

        void Update()
        {
            Advance(Time.deltaTime);
        }

        public void Advance(float dt)
        {
            if (_current == null) return;
            var d = _current.Data;
            float step = d.param == "walk_phase" ? d.phaseRate * dt : dt * 1000f;
            Cursor += step * speed;
            if (!d.loop && Cursor > d.duration) Cursor = d.duration;
            ApplyAt(Cursor);
        }
    }
}
