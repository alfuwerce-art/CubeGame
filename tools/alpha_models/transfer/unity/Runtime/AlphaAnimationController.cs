// Single clock/pose owner. Locomotion is the established Alpha formula; actions are raw oracle
// or captured clips. Bounded entry/recovery/exit crossfades are UNITY_TRANSITION, not Alpha poses.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    public enum AlphaControllerPhase { Locomotion, Stopping, EnteringAction, Action, Recovery, ExitingAction }

    [RequireComponent(typeof(AlphaRig))]
    public class AlphaAnimationController : MonoBehaviour
    {
        public AlphaLocomotionDriver locomotion;
        public AlphaClipPlayer actions;
        public string[] requiredClips = new string[0];
        public float entrySeconds = .15f, recoveryBlendSeconds = .18f, exitSeconds = .12f;
        public float stopAmplitudePerSecond = 1.2f; // Unity input policy, PLACEHOLDER_NOT_ALPHA
        public string CurrentAction { get; private set; }
        public bool InAction => CurrentAction != null;
        public bool Initialized { get; private set; }
        public AlphaControllerPhase Phase { get; private set; }
        public bool IsBlending => Phase == AlphaControllerPhase.EnteringAction || Phase == AlphaControllerPhase.ExitingAction
            || (Phase == AlphaControllerPhase.Recovery && BlendWeight < 1f);
        public PartPose[] BlendFrom { get; private set; }
        public float BlendWeight { get; private set; } = 1f;
        public List<string> Problems { get; } = new List<string>();
        public List<string> Events { get; } = new List<string>();
        AlphaRig _rig;
        float _elapsed;
        string _pending;

        public bool Init()
        {
            if (Initialized) return Problems.Count == 0;
            Problems.Clear(); Events.Clear();
            _rig = GetComponent<AlphaRig>();
            if (locomotion == null) locomotion = GetComponent<AlphaLocomotionDriver>();
            if (actions == null) actions = GetComponent<AlphaClipPlayer>();
            if (locomotion == null) Problems.Add("CONTROLLER_NO_LOCOMOTION");
            if (actions == null) Problems.Add("CONTROLLER_NO_ACTION_PLAYER");
            if (!(entrySeconds > 0) || !(recoveryBlendSeconds > 0) || !(exitSeconds > 0) || !(stopAmplitudePerSecond > 0))
                Problems.Add("CONTROLLER_INVALID_TRANSITION_DURATION");
            if (Problems.Count > 0) return false;
            Problems.AddRange(_rig.Validate());
            locomotion.enabled = false; actions.enabled = false; actions.autoPlay = false;
            locomotion.Load(); Problems.AddRange(locomotion.Problems);
            actions.LoadClips(); Problems.AddRange(actions.Problems);
            var names = requiredClips.Length > 0 ? requiredClips : actions.ClipNames.Where(n => n.StartsWith("action_")).ToArray();
            if (names.Length == 0) Problems.Add("CONTROLLER_NO_REQUIRED_ACTIONS");
            foreach (var n in names)
            {
                var clip = actions.Get(n);
                if (clip == null) { Problems.Add("ACTION_CLIP_MISSING " + n); continue; }
                var d = clip.Data;
                if (d.loop || d.param != "time_ms" || !(d.actionEndMs > 0) || !(d.recoveryStartMs > d.actionEndMs)
                    || !(d.recoveryStartMs < d.duration)) Problems.Add("ACTION_TIMELINE_INVALID " + n);
                if (d.parts.Length != _rig.partNames.Length || !_rig.partNames.All(p => clip.PartIndex(p) >= 0))
                    Problems.Add("ACTION_PART_SET_INCOMPLETE " + n);
                if (d.status != "EMULATED_ALPHA_CODE" && d.status != "TRANSFERRED_FROM_ALPHA")
                    Problems.Add("ACTION_PROVENANCE_UNSUPPORTED " + n);
            }
            if (Problems.Count > 0) return false;
            Initialized = true;
            Phase = AlphaControllerPhase.Locomotion;
            _rig.SetWeaponOnBack(true);
            locomotion.Apply(); Events.Add("idle");
            return true;
        }

        // Busy/unknown requests never replace a running action. A moving request queues a stop.
        public bool PlayAction(string name)
        {
            if (!Initialized && !Init()) return false;
            if (Phase != AlphaControllerPhase.Locomotion) return false;
            if (name == null || actions.Get(name) == null || !name.StartsWith("action_"))
            { Problems.Add("ACTION_CLIP_MISSING " + name); return false; }
            if (requiredClips.Length > 0 && Array.IndexOf(requiredClips, name) < 0) return false;
            _pending = name;
            if (locomotion.amplitude > 1e-5f || Mathf.Abs(locomotion.headLeanDeg) > 1e-4f)
            { Phase = AlphaControllerPhase.Stopping; Events.Add("stop"); }
            else Enter();
            return true;
        }

        PartPose[] Snapshot()
        {
            var p = new PartPose[_rig.parts.Length];
            for (int i = 0; i < p.Length; i++)
                p[i] = new PartPose { position = _rig.parts[i].localPosition, rotation = _rig.parts[i].localRotation, scale = _rig.parts[i].localScale };
            return p;
        }

        void Blend(PartPose[] from, float u)
        {
            BlendWeight = Mathf.Clamp01(u);
            for (int i = 0; i < from.Length; i++)
            {
                var t = _rig.parts[i];
                t.localPosition = Vector3.Lerp(from[i].position, t.localPosition, BlendWeight);
                t.localRotation = Quaternion.Slerp(from[i].rotation, t.localRotation, BlendWeight);
                t.localScale = Vector3.Lerp(from[i].scale, t.localScale, BlendWeight);
            }
        }

        void Enter()
        {
            BlendFrom = Snapshot();
            if (!actions.Play(_pending)) { Problems.AddRange(actions.Problems); return; }
            CurrentAction = _pending;
            _elapsed = 0; Phase = AlphaControllerPhase.EnteringAction;
            Blend(BlendFrom, 0); Events.Add("enter:" + CurrentAction);
        }

        public void Step(float dt)
        {
            if (!Initialized || Problems.Count > 0) return;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0 || !(actions.speed > 0) || float.IsInfinity(actions.speed))
            { Problems.Add("CONTROLLER_INVALID_CLOCK"); return; }
            switch (Phase)
            {
                case AlphaControllerPhase.Locomotion:
                    locomotion.Step(dt); locomotion.Apply();
                    break;
                case AlphaControllerPhase.Stopping:
                    locomotion.followAmplitude = false;
                    locomotion.boost = false; locomotion.accelerating = false;
                    locomotion.amplitude = Mathf.MoveTowards(locomotion.amplitude, 0, stopAmplitudePerSecond * dt);
                    locomotion.Step(dt); locomotion.Apply();
                    if (locomotion.amplitude == 0 && Mathf.Abs(locomotion.headLeanDeg) < 1e-4f) Enter();
                    break;
                case AlphaControllerPhase.EnteringAction:
                    _elapsed += dt;
                    actions.ApplyAt(0); Blend(BlendFrom, _elapsed / entrySeconds);
                    if (_elapsed >= entrySeconds) { Phase = AlphaControllerPhase.Action; Events.Add("action:" + CurrentAction); }
                    break;
                case AlphaControllerPhase.Action:
                    var previous = Snapshot();
                    actions.Advance(dt);
                    if (actions.Cursor >= actions.Current.Data.actionEndMs)
                    {
                        BlendFrom = previous; _elapsed = 0; Phase = AlphaControllerPhase.Recovery;
                        Blend(BlendFrom, 0); Events.Add("recovery:" + CurrentAction);
                    }
                    break;
                case AlphaControllerPhase.Recovery:
                    _elapsed += dt;
                    actions.Advance(dt); Blend(BlendFrom, _elapsed / recoveryBlendSeconds);
                    if (actions.Cursor >= actions.Current.Data.duration && BlendWeight >= 1f)
                    {
                        BlendFrom = Snapshot(); locomotion.Apply(); Blend(BlendFrom, 0);
                        _elapsed = 0; Phase = AlphaControllerPhase.ExitingAction; Events.Add("exit:" + CurrentAction);
                    }
                    break;
                case AlphaControllerPhase.ExitingAction:
                    _elapsed += dt; locomotion.Apply(); Blend(BlendFrom, _elapsed / exitSeconds);
                    if (_elapsed >= exitSeconds)
                    {
                        Phase = AlphaControllerPhase.Locomotion; CurrentAction = null; _pending = null;
                        Events.Add("idle");
                    }
                    break;
            }
        }

        void Start() { Init(); }
        void Update() { Step(Time.deltaTime); }
    }
}
