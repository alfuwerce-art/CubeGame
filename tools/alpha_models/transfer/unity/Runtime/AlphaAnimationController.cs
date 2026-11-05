// AlphaAnimationController.cs — one controller for every compatible Alpha creature:
//   locomotion  -> AlphaLocomotionDriver (Alpha's walk formula evaluated each frame, any speed)
//   actions     -> AlphaClipPlayer time clips baked offline by the Alpha oracle (Alpha's own
//                  drawModel executed on this creature kind: idle -> action -> back to idle)
// Mirrors Alpha's own structure (docs/research/alpha2013/ALPHA_ANIMATION_SYSTEM.md):
//   AnimationStateResolver  = action code -> drawModel state (states.json, from the renderer block)
//   PoseEvaluator           = formula (locomotion) | baked oracle track (actions)
//   TransitionController    = Alpha's own smoothing, already inside the baked tracks; handover is
//                             exact when the creature stands (amplitude 0) at the action start
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [RequireComponent(typeof(AlphaRig))]
    public class AlphaAnimationController : MonoBehaviour
    {
        public AlphaLocomotionDriver locomotion;
        public AlphaClipPlayer actions;
        public string CurrentAction { get; private set; }
        public bool InAction => CurrentAction != null;
        public List<string> Problems { get; } = new List<string>();

        public void Init()
        {
            if (locomotion == null) locomotion = GetComponent<AlphaLocomotionDriver>();
            if (actions == null) actions = GetComponent<AlphaClipPlayer>();
            if (locomotion == null) Problems.Add("CONTROLLER_NO_LOCOMOTION");
            if (actions == null) Problems.Add("CONTROLLER_NO_ACTION_PLAYER");
            if (Problems.Count > 0) return;
            locomotion.Load();
            Problems.AddRange(locomotion.Problems);
            actions.autoPlay = false;
            actions.LoadClips();
            Problems.AddRange(actions.Problems);
            actions.enabled = false;
            locomotion.enabled = true;
        }

        /// <summary>Start an action clip (baked from Alpha's drawModel). The walk should be stopped
        /// (amplitude 0): the clip begins from Alpha's settled standing pose.</summary>
        public bool PlayAction(string clip)
        {
            if (actions.Get(clip) == null) { Problems.Add($"ACTION_CLIP_MISSING {clip}"); return false; }
            locomotion.enabled = false;
            actions.enabled = true;
            actions.Play(clip);
            CurrentAction = clip;
            return true;
        }

        void LateUpdate()
        {
            if (!InAction) return;
            var d = actions.Current.Data;
            if (actions.Cursor >= d.duration)
            {
                actions.enabled = false;
                CurrentAction = null;
                locomotion.enabled = true;
                locomotion.Apply();          // back to the formula: the clip ended in Alpha's idle pose
            }
        }
    }
}
