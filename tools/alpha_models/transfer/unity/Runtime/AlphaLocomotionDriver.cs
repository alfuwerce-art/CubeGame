// AlphaLocomotionDriver.cs — drives an AlphaRig with the parametric Alpha walk (AlphaParamWalk)
// instead of a captured clip: any speed / amplitude / boost, continuous transitions, no keyframes.
//
// Inputs are game-side quantities (what Alpha's creature update feeds drawModel):
//   speedXY      horizontal speed, Alpha units/s  -> phase rate (INFERRED, see AlphaParamWalk.PhaseRate)
//   amplitude    walk amplitude creature+0x1188 (0 = standing, ~0.19 = steady walk in the captures)
//   boost        x1.25 swing (player run input)        accelerating  AI-style acceleration (head lean)
// The amplitude follower below is PLACEHOLDER_NOT_ALPHA: Alpha's amplitude dynamics were not
// reconstructed; set `amplitude` directly (followAmplitude = false) for exact Alpha states.
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [RequireComponent(typeof(AlphaRig))]
    public class AlphaLocomotionDriver : MonoBehaviour
    {
        public TextAsset specAsset;
        public float speedXY = 3.7f;
        public float amplitude = 0.19f;
        public bool followAmplitude;
        public float amplitudeTarget = 0.19f;
        public bool boost;
        public bool accelerating;
        public bool onGround = true;
        public float phase;
        public float headLeanDeg;

        AlphaRig _rig;
        public AlphaLocomotionSpec Spec { get; private set; }
        public List<string> Problems { get; } = new List<string>();
        int[] _parts = new int[0];
        string[] _names = new string[0];
        public int FramesApplied { get; private set; }

        public bool Load()
        {
            _rig = GetComponent<AlphaRig>();
            Problems.Clear();
            if (specAsset == null) { Problems.Add("LOCOMOTION_SPEC_MISSING"); return false; }
            Spec = AlphaLocomotionSpec.Parse(specAsset.text, Problems);
            if (Spec == null) return false;
            if (!string.IsNullOrEmpty(_rig.rigId) && Spec.rig != _rig.rigId)
                Problems.Add($"LOCOMOTION_RIG_MISMATCH spec={Spec.rig} rig={_rig.rigId}");
            var idx = new List<int>(); var names = new List<string>();
            for (int i = 0; i < _rig.partNames.Length; i++)
            {
                var n = _rig.partNames[i];
                if (!AlphaParamWalk.IsModelled(n) && !(n == "weapon" && Spec.weaponSubtype >= 0))
                { if (n != "weapon") Problems.Add($"LOCOMOTION_PART_NOT_MODELLED {n}"); continue; }
                idx.Add(i); names.Add(n);
            }
            _parts = idx.ToArray(); _names = names.ToArray();
            return Problems.Count == 0;
        }

        public AlphaWalkInput Input => new AlphaWalkInput
            { phase = phase, amp = amplitude, boost = boost, headLeanDeg = headLeanDeg, onGround = onGround };

        /// <summary>Advance the runtime state by dt seconds (phase, lean, optional amplitude follower).</summary>
        public void Step(float dt)
        {
            if (Spec == null) return;
            phase += AlphaParamWalk.PhaseRate(Spec.@params, speedXY) * dt;
            float ms = dt * 1000f;
            headLeanDeg = AlphaParamWalk.LeanStep(headLeanDeg, AlphaParamWalk.HeadLeanTarget(Spec.@params, boost, accelerating), ms);
            if (followAmplitude) amplitude = AlphaParamWalk.LeanStep(amplitude, amplitudeTarget, ms);  // PLACEHOLDER_NOT_ALPHA
        }

        /// <summary>Pose every modelled rig part from the current state (exact formula, no interpolation).</summary>
        public void Apply()
        {
            if (Spec == null) return;
            var s = Input;
            _rig.SetAllVisible(true);
            for (int k = 0; k < _parts.Length; k++)
            {
                var j = Joint(_names[k]);
                var u = AlphaSpace.ToUnity(j);
                if (!AlphaSpace.Decompose(u, out var pos, out var rot, out var scl)) { Problems.Add($"LOCOMOTION_NOT_TRS {_names[k]}"); continue; }
                var t = _rig.parts[_parts[k]];
                t.localPosition = pos; t.localRotation = rot; t.localScale = scl;
            }
            FramesApplied++;
        }

        public IReadOnlyList<string> PartNames => _names;
        public float[] Joint(string part) => part == "weapon" ? AlphaWeaponLocomotion.Joint(Spec, Input)
            : AlphaParamWalk.Joint(Spec.@params, Input, part);

        void Start() { Load(); Apply(); }

        void Update()
        {
            Step(Time.deltaTime);
            Apply();
        }
    }
}
