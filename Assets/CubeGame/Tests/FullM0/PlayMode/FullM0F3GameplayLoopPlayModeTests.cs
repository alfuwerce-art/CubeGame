using System.Collections;
using System.IO;
using System.Linq;
using CubeGame.Content;
using CubeGame.Core.Characters;
using CubeGame.Core.Progression;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay;
using CubeGame.Gameplay.Streaming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    public sealed class FullM0F3GameplayLoopPlayModeTests
    {
        private GameObject _host;
        private string _saveDirectory;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            foreach (string name in new[] { "Main Camera", "Sun", "WorldRoot", "Player" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null) Object.Destroy(leftover);
            }

            if (Directory.Exists(_saveDirectory)) Directory.Delete(_saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator HuntLootTradeAndChestSurviveTheSameSaveAndContinue()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(), "cubegame_f3_" + System.Guid.NewGuid().ToString("N"));
            _host = new GameObject("F3 GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDirectory;
            yield return null;
            Assert.IsTrue(game.StartNewWorld("slot0", "F3 world", 666666u, "Brena", CharacterClass.Warrior), game.Status);
            game.TeleportHero(8397145, 8396899);
            yield return null;
            game.Session.Clock.StepTicks(2);

            HeroState hero = game.Session.Hero;
            Assert.IsTrue(game.TryApplyHeroProgression(new HeroSkillRanks(), CharacterSpecialization.Secondary), game.Status);
            hero.Sheet.AddExperience(AlphaMath.ExperienceCostForNextLevel(1) - 1);
            Combatant target = game.Session.Combat.Combatants
                .Where(c => c.Hostile && c.Id.Kind == EntityIdKind.ZoneSpawn)
                .OrderBy(c => DistanceSquared(c.Position, hero.Position))
                .FirstOrDefault();
            Assert.IsNotNull(target, "the wilderness fixture contains a live hostile world spawn");
            PlaceHero(game, target.Position);
            yield return null;
            game.Session.Clock.StepTicks(1);
            HuntMission hunt = hero.Missions.Hunts.Single(m => !m.Complete);
            Assert.IsTrue(game.Session.Combat.TryGet(hunt.Target, out target), "the journal tracks a real world spawn");
            PlaceHero(game, target.Position);
            yield return null;

            var itemsBefore = hero.Inventory.AllCoreItems().ToArray();
            target.Health.ApplyDamage(target.Health.CurrentHealth - 0.01);
            for (int attempt = 0; attempt < 30 && !target.IsDead; attempt++)
            {
                game.Session.Combat.ResolveHit(hero.Id, target.Id, game.Session.Clock.TimeMs, attempt);
            }
            Assert.IsTrue(target.IsDead);
            Assert.IsTrue(hero.Missions.Hunts.Single(m => m.Target.Equals(hunt.Target)).Complete);
            Assert.AreEqual(1, hero.ManaCubes);
            Assert.AreEqual(2, hero.Sheet.Progression.Level);
            PlaceHero(game, target.Position);
            yield return null;
            game.Session.Clock.StepTicks(1);
            int lootSlot = Enumerable.Range(0, HeroInventory.Capacity).Where(i =>
                hero.Inventory[i]?.IsCore == true && !itemsBefore.Contains(hero.Inventory[i].Core)).DefaultIfEmpty(-1).First();
            Assert.GreaterOrEqual(lootSlot, 0, "combat loot moved through ground pickup into the bags");
            Assert.AreEqual(game.Session.WorldItems.Count, game.Visuals.GroundItemCount);
            var draft = new HeroSkillRanks();
            Assert.IsTrue(draft.TryRaise(HeroSkill.Climbing, 2));
            Assert.IsTrue(game.TryApplyHeroProgression(draft, hero.Sheet.Specialization), game.Status);

            ZoneKey city = FindZone(game, AlphaZoneRecord.Town);
            TravelTo(game, city);
            yield return null;
            LiveSpawn vendor = TownNpc(game, city, TownNpcPopulation.MerchantClass);
            Assert.IsTrue(game.Visuals.TryGetRoot(vendor.Id, out _));
            PlaceHero(game, vendor.Definition.Position);
            yield return null;
            game.Session.Clock.StepTicks(2);
            Assert.IsTrue(game.Creatures.TryGetAi(vendor.Id, out var vendorAi));
            Assert.IsNull(vendorAi.TargetId);
            Assert.AreEqual("LOOK_AT_PLAYER", vendorAi.LastReason);
            Assert.IsTrue(game.TryInteract(vendor.Id), game.Status);
            Assert.IsFalse(game.TryBuyFromVendor(0), "zero starting copper");
            int proceeds = AlphaTradeValue.Of(hero.Inventory[lootSlot]);
            Assert.IsTrue(game.TrySellToVendor(lootSlot), game.Status);
            Assert.AreEqual(proceeds, hero.Copper);
            int potions = hero.Inventory.CountOf(LootRules.LifePotion);
            Assert.IsTrue(game.TryBuyFromVendor(0), game.Status);
            Assert.AreEqual(potions + 1, hero.Inventory.CountOf(LootRules.LifePotion));
            Assert.AreEqual(proceeds - 2, hero.Copper);
            Assert.IsTrue(game.TryBuyFromVendor(1), game.Status);
            Assert.IsTrue(game.TryBuyFromVendor(2), game.Status);
            Assert.IsTrue(game.TryBrewLifePotion(), game.Status);
            Assert.AreEqual(potions + 2, hero.Inventory.CountOf(LootRules.LifePotion));
            Assert.AreEqual(0, hero.Inventory.CountOf(AlphaLifePotionRecipe.Heartflower));
            Assert.AreEqual(0, hero.Inventory.CountOf(AlphaLifePotionRecipe.WaterFlask));

            LiveSpawn innkeeper = TownNpc(game, city, TownNpcPopulation.InnkeeperClass);
            PlaceHero(game, innkeeper.Definition.Position);
            Assert.IsTrue(game.TryInteract(innkeeper.Id), game.Status);
            Assert.IsFalse(game.TryRestAtInn(), "the inn is closed during the day");
            GameClock clock = game.Session.Clock;
            clock.Restore(9L * 360 * clock.TicksPerSecond);
            long tickBeforeRest = clock.Tick;
            int day = clock.WorldDay;
            Assert.IsTrue(game.TryRestAtInn(), game.Status);
            Assert.AreEqual(tickBeforeRest, clock.Tick);
            Assert.AreEqual(day + 1, clock.WorldDay);
            Assert.AreEqual(GameClock.InnDawnMs, clock.WorldTimeMs);
            long offset = clock.WorldTimeOffsetMs;

            ZoneKey dungeon = FindZone(game, AlphaZoneRecord.Type3);
            TravelTo(game, dungeon);
            yield return null;
            LiveSpawn chest = game.Population.Live.First(s => s.Zone.Equals(dungeon)
                && s.Definition.BehaviourKey == AlphaDungeonContentProvider.ChestBehaviourKey);
            PlaceHero(game, chest.Definition.Position);
            int used = hero.Inventory.UsedSlots;
            Assert.IsTrue(game.TryInteract(chest.Id), game.Status);
            Assert.AreEqual(used + AlphaDungeonRules.ChestItemCount, hero.Inventory.UsedSlots);
            Assert.IsFalse(game.TryInteract(chest.Id), "claim cannot be repeated in the same frame");
            Assert.IsTrue(game.Session.WorldDeltas.IsContainerOpened(dungeon, chest.Channel, chest.Definition.SpawnIndex));
            int wallet = hero.Copper;
            int bags = hero.Inventory.UsedSlots;

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false), game.Status);
            yield return null;
            Assert.AreEqual(0, CountNamed("Player"));
            Assert.AreEqual(0, CountNamed("WorldRoot"));
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            yield return null;

            hero = game.Session.Hero;
            Assert.AreEqual(1, CountNamed("Player"));
            Assert.AreEqual(1, CountNamed("WorldRoot"));
            Assert.AreEqual(wallet, hero.Copper);
            Assert.AreEqual(1, hero.ManaCubes);
            Assert.AreEqual(2, hero.Sheet.Progression.Level);
            Assert.AreEqual(CharacterSpecialization.Secondary, hero.Sheet.Specialization);
            Assert.AreEqual(1, hero.SkillRanks[HeroSkill.Climbing]);
            Assert.AreEqual(bags, hero.Inventory.UsedSlots);
            Assert.AreEqual(potions + 2, hero.Inventory.CountOf(LootRules.LifePotion));
            Assert.AreEqual(offset, game.Session.Clock.WorldTimeOffsetMs);
            Assert.IsTrue(hero.Missions.Hunts.Single(m => m.Target.Equals(hunt.Target)).Complete);
            Assert.IsFalse(game.Population.TryGet(chest.Id, out _));
            Assert.IsFalse(game.TryInteract(chest.Id));
            Assert.AreEqual(0, game.Session.WorldItems.Count);

            TravelTo(game, city);
            yield return null;
            LiveSpawn reloadedVendor = TownNpc(game, city, TownNpcPopulation.MerchantClass);
            Assert.AreEqual(vendor.Id, reloadedVendor.Id);
            PlaceHero(game, reloadedVendor.Definition.Position);
            Assert.IsTrue(game.TryInteract(reloadedVendor.Id), game.Status);
            Assert.IsTrue(game.TrySellToVendor(hero.Inventory.FindFirst(LootRules.LifePotion)), game.Status);
            Assert.AreEqual(wallet + 2, hero.Copper);
            hero.Health.ApplyDamage(20);
            double injured = hero.Health.CurrentHealth;
            Assert.IsTrue(game.UseFirst(LootRules.LifePotion), game.CombatLog);
            Assert.Greater(hero.Health.CurrentHealth, injured);
            Assert.IsEmpty(game.Session.Events.HandlerErrors);
            Assert.IsTrue(game.ExitToMenu(false), game.Status);
        }

        private static LiveSpawn TownNpc(GameBootstrap game, ZoneKey city, int role) => game.Population.Live.Single(s =>
            s.Zone.Equals(city) && s.Definition.BehaviourKey == TownNpcPopulation.BehaviourKey && s.Definition.ClassId == role);

        private static ZoneKey FindZone(GameBootstrap game, int type)
        {
            const int index = WorldLayout.CenterRegion;
            AlphaRegion region = ((AlphaTerrainGenerator)game.WorldState.Generator).RegionRecord(index, index);
            int slot = System.Array.FindIndex(region.Zones, r => r.Type == type);
            Assert.GreaterOrEqual(slot, 0, $"fixture seed has zone type {type}");
            return new ZoneKey(index * 64 + slot / 64, index * 64 + slot % 64);
        }

        private static void TravelTo(GameBootstrap game, ZoneKey zone) =>
            game.TeleportHero(zone.X * WorldLayout.ZoneSize + 128, zone.Z * WorldLayout.ZoneSize + 128);

        private static void PlaceHero(GameBootstrap game, WorldPosition position)
        {
            game.PlayerTransform.position = game.WorldView.ToScene(position.X, position.Y, position.Z);
            Physics.SyncTransforms();
        }

        private static double DistanceSquared(WorldPosition a, WorldPosition b)
        {
            double dx = a.X - b.X;
            double dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private static int CountNamed(string name) =>
            Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Count(go => go.name == name);
    }
}
