using System;
using System.Collections.Generic;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Session
{
    public readonly struct HuntMission
    {
        public HuntMission(StableEntityId target, KindId kind, bool complete)
        {
            Target = target;
            Kind = kind;
            Complete = complete;
        }

        public StableEntityId Target { get; }
        public KindId Kind { get; }
        public bool Complete { get; }
        // A mission cell spans eight of Alpha's 256-block zones on each axis (J4/J5).
        public int CellX => (int)((Target.Value >> 35) & 0x1FFF);
        public int CellZ => (int)((Target.Value >> 19) & 0x1FFF);
    }

    /// <summary>
    /// Alpha J4: proximity activates a regional hunt and a matching kill completes it once.
    /// CUBEGAME_CONTRACT: select a nearby streamed hostile as the cell target, use a 32-block
    /// activation radius and award one mana cube. Alpha mission generation and rewards are PARTIAL.
    /// </summary>
    public sealed class MissionJournal
    {
        public const double ActivationRadius = 32.0;
        private readonly SortedDictionary<int, HuntMission> _hunts = new SortedDictionary<int, HuntMission>();

        public IReadOnlyCollection<HuntMission> Hunts => _hunts.Values;

        internal bool TryActivateNear(WorldPosition hero, IReadOnlyCollection<Combatant> combatants, out HuntMission activated)
        {
            Combatant nearest = null;
            double nearestSquared = ActivationRadius * ActivationRadius;
            foreach (Combatant candidate in combatants)
            {
                if (!candidate.Hostile || candidate.IsDead || candidate.Id.Kind != EntityIdKind.ZoneSpawn)
                {
                    continue;
                }

                double dx = candidate.Position.X - hero.X;
                double dy = candidate.Position.Y - hero.Y;
                double dz = candidate.Position.Z - hero.Z;
                double distanceSquared = dx * dx + dy * dy + dz * dz;
                if (distanceSquared > nearestSquared || (distanceSquared == nearestSquared && nearest != null
                    && nearest.Id.Value < candidate.Id.Value))
                {
                    continue;
                }

                var mission = new HuntMission(candidate.Id, candidate.Kind, false);
                if (!_hunts.ContainsKey(CellKey(mission)))
                {
                    nearest = candidate;
                    nearestSquared = distanceSquared;
                }
            }

            activated = nearest == null ? default : new HuntMission(nearest.Id, nearest.Kind, false);
            return nearest != null && TryRestore(activated);
        }

        internal bool TryComplete(StableEntityId target)
        {
            if (target.Kind != EntityIdKind.ZoneSpawn)
            {
                return false;
            }

            int key = CellKey(new HuntMission(target, default, false));
            if (!_hunts.TryGetValue(key, out HuntMission mission) || mission.Complete || !mission.Target.Equals(target))
            {
                return false;
            }

            _hunts[key] = new HuntMission(target, mission.Kind, true);
            return true;
        }

        internal bool TryRestore(HuntMission mission)
        {
            int key = CellKey(mission);
            if (mission.Target.Kind != EntityIdKind.ZoneSpawn || (uint)mission.Kind.Value > byte.MaxValue
                || _hunts.ContainsKey(key))
            {
                return false;
            }

            _hunts.Add(key, mission);
            return true;
        }

        private static int CellKey(HuntMission mission) => (mission.CellX << 13) | mission.CellZ;
    }
}
