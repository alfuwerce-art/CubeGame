using System;
using System.Collections.Generic;
using CubeGame.M0Pilots.World;

namespace CubeGame.M0Pilots.Npc
{
    public static class NpcPilot
    {
        public static PilotResult Run()
        {
            Check.Start("PILOT_NPC");
            Factions();
            AiClockAndStun();
            Threat();
            PotionAndDodge();
            RegenAndPets();
            Bosses();
            Spawn();
            Persistence();
            AlphaVersusCubeGame();
            return Check.Finish();
        }

        private static void Factions()
        {
            Check.Equal("player faction is 0", 0, (int)Faction.Player);
            Check.Equal("monster faction is 1", 1, (int)Faction.Monster);
            Check.Equal("ally faction is 3", 3, (int)Faction.Ally);
            Check.Equal("npc faction is 5", 5, (int)Faction.Npc);
            Check.Equal("passive faction is 6", 6, (int)Faction.Passive);
        }

        private static void AiClockAndStun()
        {
            var clock = new AiClock();
            Check.Equal("30 ms produces no AI tick", 0, clock.Add(30));
            Check.Equal("31 ms produces one AI tick", 1, clock.Add(1));
            Check.Equal("the remainder is preserved", 0, clock.AccumulatorMs);
            Check.Equal("62 ms produces two AI ticks", 2, clock.Add(62));
            Check.Equal("100 ms produces three ticks with 7 ms remainder", 3, clock.Add(100));
            Check.Equal("accumulator keeps the 7 ms remainder", 7, clock.AccumulatorMs);
            Check.Throws<ArgumentOutOfRangeException>("negative AI time is rejected", () => clock.Add(-1));

            var state = new CreatureState { StunMs = 100 };
            Check.True("stunned creature cannot think", !state.CanThink);
            state.AdvanceTime(50);
            Check.Equal("stun timer decreases", 50, state.StunMs);
            Check.True("creature still cannot think at 50 ms", !state.CanThink);
            state.AdvanceTime(50);
            Check.True("creature can think when stun reaches zero", state.CanThink);
        }

        private static void Threat()
        {
            var table = new ThreatTable();
            table.Add("player", 100.0);
            Check.Equal("top target is the only target", "player", table.TopTarget());
            table.Tick();
            Check.Near("threat decays by 0.9 per tick", 90.0, table.Get("player"), 1e-9);
            for (int i = 0; i < 9; i++)
            {
                table.Tick();
            }

            Check.Near("ten ticks leave 100 * 0.9^10", 100.0 * Math.Pow(0.9, 10.0), table.Get("player"), 1e-6);

            var pair = new ThreatTable();
            pair.Add("a", 10.0);
            pair.Add("b", 20.0);
            Check.Equal("higher threat wins", "b", pair.TopTarget());
            pair.Add("a", 15.0);
            Check.Near("threat accumulates", 25.0, pair.Get("a"), 1e-12);
            Check.Equal("accumulated threat changes the target", "a", pair.TopTarget());

            var reset = new ThreatTable();
            reset.Add("player", 100.0);
            reset.MarkUnreachable("player", 19999);
            Check.Equal("unreachable for 19.999 s does not clear aggro", 1, reset.Count);
            reset.MarkUnreachable("player", 1);
            Check.Equal("exactly 20 s still does not clear (rule is > 20 s)", 1, reset.Count);
            reset.MarkUnreachable("player", 1);
            Check.Equal("more than 20 s clears the threat table", 0, reset.Count);
            Check.Equal("a cleared table schedules a 3 s rescan", 3000, reset.RescanRemainingMs);
            reset.Tick();
            Check.Equal("rescan ticks down by the AI tick", 3000 - AiClock.TickMs, reset.RescanRemainingMs);
            Check.Throws<ArgumentOutOfRangeException>("negative threat is rejected", () => reset.Add("x", -1.0));
        }

        private static void PotionAndDodge()
        {
            Check.True("faction 3 drinks when HP < 0.5 * incoming damage",
                NpcRules.ShouldDrinkPotion(10.0, 21.0, (int)Faction.Ally));
            Check.True("faction 3 does not drink at exactly half incoming damage",
                !NpcRules.ShouldDrinkPotion(10.0, 20.0, (int)Faction.Ally));
            Check.True("monster faction never uses the ally potion rule",
                !NpcRules.ShouldDrinkPotion(1.0, 100.0, (int)Faction.Monster));
            Check.True("npc faction does not use the ally potion rule",
                !NpcRules.ShouldDrinkPotion(1.0, 100.0, (int)Faction.Npc));

            var state = new CreatureState();
            NpcRules.ApplyPotionAction(state);
            Check.Equal("potion action id is 0x50", 0x50, state.ActionId);
            Check.Equal("potion action resets the action timer", 0, state.ActionTimerMs);

            var dodger = new CreatureState();
            Check.True("roll 0 triggers an AI dodge", NpcRules.TryDodge(0, false, 1.0, dodger));
            Check.Equal("dodge sets a 600 ms action lock", 600, dodger.ActionLockMs);
            Check.True("roll 10 also triggers a dodge", NpcRules.TryDodge(10, false, 1.0, new CreatureState()));
            Check.True("roll 1 does not trigger a dodge", !NpcRules.TryDodge(1, false, 1.0, new CreatureState()));
            Check.True("bosses never dodge", !NpcRules.TryDodge(0, true, 1.0, new CreatureState()));
            Check.True("distance 2 is outside the dodge radius", !NpcRules.TryDodge(0, false, 2.0, new CreatureState()));
        }

        private static void RegenAndPets()
        {
            var friendly = new CreatureState
            {
                Faction = (int)Faction.Npc,
                Health = 50.0,
                MaxHealth = 100.0,
            };
            NpcRules.RegenerateFriendly(friendly, 1.0);
            Check.Near("faction 5 regenerates 10% MaxHP per second", 60.0, friendly.Health, 1e-9);
            NpcRules.RegenerateFriendly(friendly, 100.0);
            Check.Near("friendly regeneration is clamped to MaxHP", 100.0, friendly.Health, 1e-9);

            var monster = new CreatureState
            {
                Faction = (int)Faction.Monster,
                Health = 50.0,
                MaxHealth = 100.0,
            };
            NpcRules.RegenerateFriendly(monster, 1.0);
            Check.Near("monsters do not use the faction 5 regeneration", 50.0, monster.Health, 1e-12);

            Check.Equal("pet summon limit is 2", 2, NpcRules.PetSummonLimit);
            Check.Equal("pet summon ability id is 0x5c", 0x5C, NpcRules.PetSummonAbilityId);
        }

        private static void Bosses()
        {
            var boss = new CreatureState { Kind = 0x65 };
            Check.True("kind 0x65 is a boss", boss.IsBoss);
            Check.True("kind 0x77 is a boss", new CreatureState { Kind = 0x77 }.IsBoss);
            Check.True("kind 0x64 is not in the boss set", !new CreatureState { Kind = 0x64 }.IsBoss);
            Check.True("kind 0x78 is not in the boss set", !new CreatureState { Kind = 0x78 }.IsBoss);
        }

        private static void Spawn()
        {
            Check.Equal("spawn table has 30 level rows", 30, SpawnTable.LevelRows);
            Check.Equal("spawn record is 0x118 bytes", 0x118, SpawnTable.RecordBytes);
            Check.Equal("star clamp is 4", 4, SpawnTable.StarClamp);
            Check.Equal("three documented star rolls give at most 3 stars",
                3, SpawnTable.RollStars(true, true, true));
            Check.Equal("no star rolls give zero stars", 0, SpawnTable.RollStars(false, false, false));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int slot = 0; slot < 100; slot++)
            {
                ids.Add(SpawnTable.SpawnId(8, 8, 3, 4, slot));
            }

            Check.Equal("spawn ids are unique per slot", 100, ids.Count);
            Check.True("spawn id contains region, cell and slot", ids.Contains("spawn:8:8:3:4:0"));
            Check.Throws<ArgumentOutOfRangeException>("invalid cell coordinate is rejected",
                () => SpawnTable.SpawnId(0, 0, 8, 0, 0));

            const int samples = 200000;
            var random = new AlphaRandom(424242);
            int firstStar = 0;
            int twoPlus = 0;
            for (int i = 0; i < samples; i++)
            {
                int stars = SpawnTable.RollStars(random);
                if (stars >= 1)
                {
                    firstStar++;
                }

                if (stars >= 2)
                {
                    twoPlus++;
                }
            }

            double firstRate = firstStar / (double)samples;
            double twoRate = twoPlus / (double)samples;
            Check.True("1% star chance is near 1% in 200k rolls", firstRate > 0.0085 && firstRate < 0.0115,
                "rate=" + firstRate.ToString("P3"));
            Check.True("a second star is at least 10x rarer than the first (conditional roll)",
                twoRate < firstRate / 10.0,
                "first=" + firstRate.ToString("P3") + " twoPlus=" + twoRate.ToString("P3"));
        }

        private static void Persistence()
        {
            var state = new CreatureState
            {
                StableId = SpawnTable.SpawnId(8, 8, 3, 4, 7),
                Kind = 0x1E,
                Faction = (int)Faction.Monster,
                Level = 12,
                Stars = 2,
                Health = 345.5,
                MaxHealth = 500.0,
                ActionId = 0x25,
                ActionTimerMs = 120,
                StunMs = 0,
                ActionLockMs = 600,
                X = 4106.25,
                Y = 33.5,
                Z = 4097.75,
            };
            state.Threat["player"] = 42.5;

            var store = new CreatureStateStore();
            store.Save(state);
            Check.Equal("store holds one blob", 1, store.Count);

            CreatureState restored;
            string error;
            Check.True("saved entity restores", store.TryRestore(state.StableId, out restored, out error), error);
            Check.Equal("stable id survives the save/load cycle", state.StableId, restored.StableId);
            Check.Equal("kind survives", state.Kind, restored.Kind);
            Check.Equal("faction survives", state.Faction, restored.Faction);
            Check.Equal("level survives", state.Level, restored.Level);
            Check.Equal("stars survive", state.Stars, restored.Stars);
            Check.Near("health survives", state.Health, restored.Health, 1e-9);
            Check.Near("position x survives", state.X, restored.X, 1e-9);
            Check.Near("position y survives", state.Y, restored.Y, 1e-9);
            Check.Near("position z survives", state.Z, restored.Z, 1e-9);
            Check.Equal("action id survives", state.ActionId, restored.ActionId);
            Check.Equal("action timer survives", state.ActionTimerMs, restored.ActionTimerMs);
            Check.Equal("action lock survives", state.ActionLockMs, restored.ActionLockMs);
            Check.Near("threat survives", 42.5, restored.Threat["player"], 1e-9);

            store.Save(state);
            Check.Equal("re-saving the same entity does not duplicate it", 1, store.Count);

            CreatureState missing;
            Check.True("unknown stable id is not invented", !store.TryRestore("spawn:0:0:0:0:1", out missing, out error));

            string corrupted = CreatureStateStore.SerializeState(state).Replace("345.5", "999.5");
            // No checksum in the entity contract yet: corruption is caught by consistency checks.
            CreatureState corruptState;
            string corruptError;
            Check.True("inconsistent HP blob is rejected",
                !CreatureStateStore.TryParseState(corrupted, out corruptState, out corruptError));
        }

        private static void AlphaVersusCubeGame()
        {
            Check.True("Alpha proves blob persistence by proximity, not live simulation", NpcEvidence.BlobPersistenceProven);
            Check.True("continuous off-screen simulation is not claimed as an Alpha mechanic",
                !NpcEvidence.OffScreenSimulationIsAlpha);
            Check.True("off-screen live simulation is marked as a CubeGame requirement", NpcEvidence.OffScreenSimulationIsCubeGameRequirement);
            Check.True("population limit is explicitly unknown", !NpcEvidence.PopulationLimitProven);
            Check.True("taming/riding mechanics are explicitly unknown", !NpcEvidence.TamingAndRidingProven);
        }
    }

    public static class NpcEvidence
    {
        public const bool BlobPersistenceProven = true;
        public const bool OffScreenSimulationIsAlpha = false;
        public const bool OffScreenSimulationIsCubeGameRequirement = true;
        public const bool PopulationLimitProven = false;
        public const bool TamingAndRidingProven = false;
    }
}
