// Separate back-weapon layer (action 0); body formula and its established contract are unchanged.
// RECONSTRUCTED_FROM_EVIDENCE. Checked against Alpha drawModel by action_baker.py.
using System;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    public static class AlphaWeaponLocomotion
    {
        public static float[] Joint(AlphaLocomotionSpec spec, AlphaWalkInput s)
        {
            if (Array.IndexOf(AlphaRig.TwoHandedSubtypes, spec.weaponSubtype) < 0)
                throw new InvalidOperationException("WEAPON_LOCOMOTION_SUBTYPE_UNVERIFIED");
            float w = s.amp * AlphaParamWalk.SwingK * Mathf.Sin(s.phase + Mathf.PI / 2f);
            if (s.boost) w *= 1.25f;
            if ((spec.@params.flags & 2) != 0 && !s.onGround) w = 0;
            if ((spec.@params.flags & 1) != 0) w *= .75f;
            return AlphaParamWalk.Chain(AlphaSpace.Translate(new Vector3(0, 0, 4f * Mathf.Abs(w))),
                AlphaParamWalk.RotZ(w * .25f * Mathf.Rad2Deg), AlphaSpace.Translate(new Vector3(0, -7.1f, .1f)),
                AlphaParamWalk.RotY(-135), AlphaParamWalk.RotZ(90), AlphaParamWalk.Scale(spec.weaponScale));
        }
    }
}
