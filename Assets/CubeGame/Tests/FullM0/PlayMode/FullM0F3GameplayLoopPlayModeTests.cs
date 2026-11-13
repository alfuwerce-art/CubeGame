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
            Assert.AreEqual(0, hero.ManaCubes, "P5: the Alpha mission reward is platinum plus one class item");
            Assert.Greater(hero.Platinum, 0);
            Assert.AreEqual(2, hero.Sheet.Progression.Level);
            PlaceHero(game, target.Position);
            yield return null;
            game.Session.Clock.StepTicks(1);
            int lootSlot = Enumerable.Range(0, HeroInventory.Capacity).Where(i =>
                hero.Inventory[i]?.IsCore == true && !itemsBefore.Contains(hero.Inventory[i].Core)).DefaultIfEmpty(-1).First();
            Assert.GreaterOrEqual(lootSlot, 0, "the mission reward item is in the bags");
            Assert.AreEqual(game.Session.WorldItems.Count, game.Visuals.GroundItemCount);
            var draft = new HeroSkillRanks();
            Assert.IsTrue(draft.TryRaise(HeroSkill.Climbing, 2));
            Assert.IsTrue(game.TryApplyHeroProgression(draft, hero.Sheet.Specialization), game.Status);

            ZoneKey city = FindZone(game, AlphaZoneRecord.Town);
            TravelTo(game, city);
            yield return null;
            LiveSpawn vendor = TownNpc(game, city, TownNpcPopulation.MerchantClass);
            Assert.IsTrue(game.Visuals.TryGetRoot(vendor.Id, out _));
            if (game.Visuals.TryGetView(vendor.Id, out var vendorView))
            {
                int weaponPart = vendorView.Rig.IndexOf("weapon");
                Assert.GreaterOrEqual(weaponPart, 0);
                Assert.IsFalse(vendorView.Rig.meshes[weaponPart].gameObject.activeSelf, "a staged sword is not NPC equipment");
            }
            PlaceHero(game, vendor.Definition.Position);
            yield return null;
            game.Session.Clock.StepTicks(2);
            Assert.IsTrue(game.Creatures.TryGetAi(vendor.Id, out var vendorAi));
            Assert.IsNull(vendorAi.TargetId);
            Assert.AreEqual("LOOK_AT_PLAYER", vendorAi.LastReason);
            Assert.IsTrue(game.TryInteract(vendor.Id), game.Status);
            int coins = hero.Copper;
            Assert.Greater(coins, 0, "Alpha 0x59B180 coins of the kill were picked up into the wallet");
            int proceeds = AlphaTradeValue.Of(hero.Inventory[lootSlot]);
            Assert.IsTrue(game.TrySellToVendor(lootSlot), game.Status);
            Assert.AreEqual(coins + proceeds, hero.Copper);
            int potions = hero.Inventory.CountOf(LootRules.LifePotion);
            // P5: the 0x80 vendor sells its Alpha 0x5C9B30 stock; offer 0 is the tier-0 flask (0x0B/0x0C, value 2).
            var stock = game.VendorStock(vendor.Id, TownNpcPopulation.MerchantClass);
            Assert.AreEqual(new AlphaItemRecord(0x0B, 0x0C, modifier: 0x18, level: 1), stock[0]);
            int flasks = hero.Inventory.CountOf(new RawAlphaItemId(0x0B, 0x0C));
            Assert.IsTrue(game.TryBuyFromVendor(0), game.Status);
            Assert.AreEqual(flasks + 1, hero.Inventory.CountOf(new RawAlphaItemId(0x0B, 0x0C)));
            Assert.AreEqual(coins + proceeds - 2, hero.Copper);
            Assert.AreEqual(1, game.Session.Buyback.Count, "the sale is on the Alpha buyback list");
            // Diagnostic: Alpha vendors do not stock the Life Potion ingredients; they are placed for the crafting check.
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromRaw(AlphaLifePotionRecipe.Heartflower, 1)));
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromRaw(AlphaLifePotionRecipe.WaterFlask, 1)));
            Assert.IsTrue(game.TryBrewLifePotion(), game.Status);
            Assert.AreEqual(potions + 1, hero.Inventory.CountOf(LootRules.LifePotion));
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
            int walletBeforeChest = hero.Copper;
            Assert.IsTrue(game.TryInteract(chest.Id), game.Status);
            Assert.GreaterOrEqual(hero.Inventory.UsedSlots, used, "chest items (0x5F8750) went to the bags");
            Assert.GreaterOrEqual(hero.Copper, walletBeforeChest, "chest coins went to the wallet");
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
            Assert.AreEqual(0, hero.ManaCubes);
            Assert.AreEqual(2, hero.Sheet.Progression.Level);
            Assert.AreEqual(CharacterSpecialization.Secondary, hero.Sheet.Specialization);
            Assert.AreEqual(1, hero.SkillRanks[HeroSkill.Climbing]);
            Assert.AreEqual(bags, hero.Inventory.UsedSlots);
            Assert.AreEqual(potions + 1, hero.Inventory.CountOf(LootRules.LifePotion));
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
