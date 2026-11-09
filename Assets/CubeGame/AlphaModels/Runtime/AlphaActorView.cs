// CubeGame.AlphaModels — the visual adapter the game talks to (D86 M0-01/M0-03C/M0-05C).
//
// One Alpha actor (creature, NPC or player race) = one AlphaActorView. It owns geometry, the
// locomotion formula and the action controller; it NEVER owns game state. The game (Claude's
// GameSession/Features) supplies the inputs that Alpha's creature update would supply and reads
// back phase/events:
//
//     view.ApplyLocomotion(new AlphaLocomotionState { SpeedXY = ..., Amplitude = ..., Boost = ... });
//     view.TryPlayAction(alphaActionCode, out reason);
//     view.Tick(gameClock.DeltaSeconds);
//
// Health, stamina, damage, loot, money and saves stay outside this component (ADR-0001).
using System;
using System.Collections.Generic;
using CubeGame.AlphaTransfer;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    /// <summary>Locomotion inputs of one frame; the values Alpha's creature update feeds drawModel.</summary>
    public struct AlphaLocomotionState
    {
        /// <summary>Horizontal speed in Alpha units/s (the unit scale is OQ-23 OPEN: PLACEHOLDER_NOT_ALPHA).</summary>
        public float SpeedXY;
        /// <summary>Walk amplitude creature+0x1188 (0 = standing, ~0.19 = steady walk in the captures).</summary>
        public float Amplitude;
        public bool Boost;
        public bool Accelerating;
        public bool OnGround;

        public static AlphaLocomotionState Standing => new AlphaLocomotionState { OnGround = true };
    }

    public sealed class AlphaActorView : MonoBehaviour
    {
        public string ActorId { get; private set; }
        public AlphaRig Rig { get; private set; }
        public AlphaAnimationController Controller { get; private set; }
        public AlphaClipPlayer Clips { get; private set; }
        public AlphaLocomotionDriver Locomotion { get; private set; }
        public AlphaActorDefinition Definition { get; private set; }
        public readonly List<string> Problems = new List<string>();
        public bool Ready { get; private set; }

        public string[] AvailableActions => Definition != null ? Definition.RequiredActions : Array.Empty<string>();

        /// <summary>Alpha timing of an action code (windup/actionEnd/recovery), if the clip has one.</summary>
        public bool TryGetActionTiming(int actionCode, out AlphaActionTiming timing)
        {
            timing = default;
            return Definition != null && Definition.ActionTimings.TryGetValue(actionCode, out timing);
        }

        public AlphaActionTiming ActionTimingOf(int actionCode) =>
            Definition != null && Definition.ActionTimings.TryGetValue(actionCode, out var timing) ? timing : default;

        /// <summary>Build geometry and controller from a loaded actor definition.</summary>
        public bool Initialize(AlphaActorDefinition def)
        {
            Problems.Clear();
            Ready = false;
            if (def == null) { Problems.Add("VIEW_NO_DEFINITION"); return false; }
            Definition = def;
            ActorId = def.Id;
            if (!def.Ready) { Problems.AddRange(def.Problems); return false; }

            var model = new GameObject("AlphaModel_" + def.Id);
            model.transform.SetParent(transform, false);
            Rig = AlphaRigBuilder.BuildInto(model, def, Problems);
            if (Rig == null) return false;

            Clips = model.GetComponent<AlphaClipPlayer>();
            Locomotion = model.GetComponent<AlphaLocomotionDriver>();
            Controller = model.AddComponent<AlphaAnimationController>();
            Controller.externalClock = true;                 // the game clock is the only clock
            Controller.requiredClips = def.RequiredActions;
            if (def.Contract != null)
            {
                // contract.StaticObject implies the action-free mode (AlphaRig/controller contract)
                Controller.locomotionOnly = def.Contract.LocomotionOnly;
                Controller.staticObject = def.Contract.StaticObject;
                Controller.actionsBlockedReason = def.Contract.StaticObject
                    ? (string.IsNullOrEmpty(def.Contract.gameUsageReason) ? "STATIC_OBJECT" : def.Contract.gameUsageReason)
                    : def.Contract.actionsReason;
            }
            else if (!def.HasActionLibrary)
            {
                Controller.locomotionOnly = true;
                Controller.actionsBlockedReason = "NO_ACTION_LIBRARY (α: only locomotion was transferred for this actor)";
            }
            if (!Controller.Init())
            {
                Problems.AddRange(Controller.Problems);
                return false;
            }
            Problems.AddRange(Controller.Problems);
            Ready = Problems.Count == 0;
            return Ready;
        }

        /// <summary>Feed the locomotion state of this frame (called by the game every tick).</summary>
        public void ApplyLocomotion(AlphaLocomotionState state)
        {
            if (Locomotion == null) return;
            Locomotion.speedXY = state.SpeedXY;
            Locomotion.amplitude = state.Amplitude;
            Locomotion.boost = state.Boost;
            Locomotion.accelerating = state.Accelerating;
            Locomotion.onGround = state.OnGround;
        }

        /// <summary>
        /// Play an Alpha action by its code, only if this actor's action profile really launched it.
        /// Returns false with a reason for blocked/unknown actions — a request never turns into a
        /// fabricated animation.
        /// </summary>
        public bool TryPlayAction(int alphaAction, out string reason)
        {
            reason = null;
            if (!Ready) { reason = "ACTOR_NOT_READY"; return false; }
            if (Controller.locomotionOnly || Controller.staticObject) { reason = "ACTIONS_BLOCKED"; return false; }
            var clip = "action_" + alphaAction.ToString("000");
            if (Array.IndexOf(Definition.RequiredActions, clip) < 0) { reason = "ACTION_NOT_IN_KIND_PROFILE " + alphaAction; return false; }
            if (!Controller.PlayAction(clip)) { reason = "ACTION_REFUSED " + Controller.Phase; return false; }
            return true;
        }

        /// <summary>Extract the action code of a clip name ("action_054" -> 54); -1 when not an action.</summary>
        public static int ActionCodeOf(string clip)
        {
            if (string.IsNullOrEmpty(clip) || !clip.StartsWith("action_", StringComparison.Ordinal)) return -1;
            return int.TryParse(clip.Substring("action_".Length), out var code) ? code : -1;
        }

        /// <summary>Advance the visual layer by exactly dt (the game clock's delta).</summary>
        public void Tick(float dt)
        {
            if (Controller == null || !Controller.Initialized) return;
            Controller.Step(dt);
        }

        public string Status()
        {
            if (Controller == null) return $"actor={ActorId} view=NOT_INITIALIZED problems={Problems.Count}";
            return $"actor={ActorId} ready={Ready} phase={Controller.Phase} action={Controller.CurrentAction ?? "none"} " +
                   $"clips={(Definition != null ? Definition.Clips.Count : 0)} actions={AvailableActions.Length} " +
                   $"frames={Clips?.FramesApplied ?? 0} problems={Problems.Count}";
        }

        public static AlphaActorView Spawn(AlphaActorDefinition def, Transform parent, List<string> problems)
        {
            var go = new GameObject("AlphaActor_" + (def != null ? def.Id : "null"));
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<AlphaActorView>();
            if (!view.Initialize(def)) problems?.AddRange(view.Problems);
            return view;
        }
    }
}
