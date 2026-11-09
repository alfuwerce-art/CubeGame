// CubeGame.Features.Creatures — patrol route of a creature/NPC (D2, N-14).
//
// Alpha's own NPC schedule/patrol semantics are not recovered (N-13/N-14 UNKNOWN): no table of waypoints,
// no day/night rule and no city schedule were proven. What exists is the observation that Alpha creatures
// walk (RandomWalkBehaviour) and that some NPCs keep to an area. This route is therefore a
// CUBEGAME_CONTRACT: a deterministic ring of waypoints around the spawn point, used by the AI instead of a
// random direction when the game attaches one. Nothing here claims Alpha parity.
using System;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Creatures
{
    /// <summary>Deterministic patrol route (ring of waypoints) for one creature.</summary>
    public sealed class PatrolRoute
    {
        public const string Provenance = "CUBEGAME_CONTRACT: waypoints around the spawn point; Alpha patrol/schedule UNKNOWN (N-13/N-14)";

        /// <summary>Arrival radius used by the AI to advance to the next waypoint (CUBEGAME_CONTRACT).</summary>
        public const double ArrivalRadius = 1.0;

        readonly double[] _x;
        readonly double[] _z;
        int _index;

        public PatrolRoute(double centerX, double centerZ, double radius, int waypoints, ulong seed)
        {
            if (waypoints <= 0) throw new ArgumentOutOfRangeException(nameof(waypoints));
            if (radius <= 0.0) throw new ArgumentOutOfRangeException(nameof(radius));
            _x = new double[waypoints];
            _z = new double[waypoints];
            ulong state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
            for (int i = 0; i < waypoints; i++)
            {
                state ^= state >> 33;
                state *= 0xFF51AFD7ED558CCDUL;
                state ^= state >> 29;
                double angle = 2.0 * Math.PI * i / waypoints;
                double jitter = 0.75 + (state % 1000UL) / 4000.0;   // radius ×0.75…×1.0, deterministic
                _x[i] = centerX + Math.Cos(angle) * radius * jitter;
                _z[i] = centerZ + Math.Sin(angle) * radius * jitter;
            }
        }

        public int Count => _x.Length;

        public int Index => _index;

        public double X => _x[_index];

        public double Z => _z[_index];

        /// <summary>Current waypoint and advance once the creature stands inside the arrival radius.</summary>
        public void AdvanceIfArrived(double x, double z)
        {
            double dx = x - X, dz = z - Z;
            if (dx * dx + dz * dz <= ArrivalRadius * ArrivalRadius)
            {
                _index = (_index + 1) % _x.Length;
            }
        }

        public void Reset() => _index = 0;

        public (double X, double Z) Waypoint(int index)
        {
            int i = ((index % _x.Length) + _x.Length) % _x.Length;
            return (_x[i], _z[i]);
        }
    }
}
