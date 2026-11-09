// CubeGame.Content — creature/NPC kind catalog (D86 M0-06A/06B data side).
//
// One entry per Alpha kind from the D65/D69 catalog: 130 combat creatures and 26 passive
// objects (faction 6 / producer rule). The catalog says what a kind IS, which motion family it
// uses and which actions its AI really launches; it never decides gameplay outcomes.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public enum AlphaCreatureRole
    {
        /// <summary>Passive object (plant, ore, egg, dummy...): no walk, no actions in the game.</summary>
        PassiveObject = 0,
        /// <summary>Creature with a verified motion family; actions only where the AI trigger launched them.</summary>
        Combat = 1,
    }

    public static class AlphaCreatureCatalog
    {
        static readonly List<int> CombatIds = new List<int>();
        static readonly List<int> PassiveIds = new List<int>();

        static AlphaCreatureCatalog()
        {
            foreach (var k in AlphaContentTables.CreatureKinds)
                (k.Passive ? PassiveIds : CombatIds).Add(k.Kind);
        }

        public static IReadOnlyList<AlphaCreatureKindDef> All => AlphaContentTables.CreatureKinds;
        public static IReadOnlyList<int> CombatKinds => CombatIds;
        public static IReadOnlyList<int> PassiveKinds => PassiveIds;

        public static bool TryKind(int kind, out AlphaCreatureKindDef def) => AlphaContentDatabase.TryCreatureKind(kind, out def);

        public static AlphaCreatureRole RoleOf(int kind) =>
            AlphaContentDatabase.TryCreatureKind(kind, out var def) && def.Passive ? AlphaCreatureRole.PassiveObject : AlphaCreatureRole.Combat;

        public static bool IsPassiveObject(int kind) => RoleOf(kind) == AlphaCreatureRole.PassiveObject;

        /// <summary>Faction for passive objects is only known where Alpha writes it (6) or the producer rule applies.</summary>
        public static int FactionOf(int kind) =>
            AlphaContentDatabase.TryCreatureKind(kind, out var def) ? def.Faction : int.MinValue;

        public static int[] ActionsOf(int kind) =>
            AlphaContentDatabase.TryCreatureKind(kind, out var def) ? def.Actions ?? Array.Empty<int>() : Array.Empty<int>();

        /// <summary>Clip name convention of the transfer pipeline (action_001 .. action_110).</summary>
        public static string ActionClipName(int action) => "action_" + action.ToString("000");

        /// <summary>
        /// True only when this kind's AI actually launches the action (npc_action_evidence). A clip that
        /// exists in the library but was never launched for the kind is not a game action of that kind.
        /// </summary>
        public static bool TryActionClipName(int kind, int action, out string clip)
        {
            clip = null;
            var actions = ActionsOf(kind);
            if (Array.IndexOf(actions, action) < 0) return false;
            clip = ActionClipName(action);
            return true;
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (CombatIds.Count + PassiveIds.Count != AlphaContentTables.CreatureKinds.Length) p.Add("CREATURE_KIND_SPLIT");
            if (PassiveIds.Count != 26) p.Add($"CREATURE_PASSIVE_COUNT {PassiveIds.Count} != 26");
            if (CombatIds.Count != 130) p.Add($"CREATURE_COMBAT_COUNT {CombatIds.Count} != 130");
            foreach (var kind in PassiveIds)
            {
                var actions = ActionsOf(kind);
                if (actions.Length > 0) p.Add($"CREATURE_PASSIVE_WITH_ACTIONS 0x{kind:x2} {actions.Length}");
            }
            foreach (var kind in CombatIds)
            {
                // The motion family is only evidenced for kinds whose AI was trigger-emulated (15 kinds);
                // for the rest the catalog says so by leaving it empty instead of inventing a family.
                if (!AlphaContentDatabase.TryCreatureKind(kind, out var def)) { p.Add($"CREATURE_KIND_MISSING 0x{kind:x2}"); continue; }
                if (def.Actions != null && def.Actions.Length > 0 && string.IsNullOrEmpty(def.MotionFamily))
                    p.Add($"CREATURE_ACTION_KIND_NO_FAMILY 0x{kind:x2}");
            }
            return p;
        }
    }
}
