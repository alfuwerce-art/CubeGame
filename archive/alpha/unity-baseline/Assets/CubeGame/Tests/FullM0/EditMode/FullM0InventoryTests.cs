using System.Collections.Generic;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Persistence;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// M0-04B/C/D: bags 4 × 50 (Alpha I-06), stacks, equip/unequip without duplication or loss, consumables
    /// with Alpha cooldown, loot with the Alpha rarity rule, and the whole inventory surviving a checkpoint.
    /// </summary>
    public sealed class FullM0InventoryTests
    {
        private static readonly RawAlphaItemId Potion = LootRules.LifePotion;

        private static ItemInstance Weapon(byte subtype, int seed) => new ItemInstance(ItemType.Weapon, subtype, 1, 0, seed);

        [Test]
        public void BagsHaveAlphaCapacityAndRefuseWhenFull()
        {
            var inventory = new HeroInventory(new EquipmentLoadout());
            Assert.AreEqual(200, HeroInventory.Capacity);
            for (int i = 0; i < HeroInventory.Capacity; i++)
            {
                Assert.IsTrue(inventory.TryAdd(InventoryEntry.FromCore(Weapon(15, i + 1))));
            }

            Assert.IsFalse(inventory.TryAdd(InventoryEntry.FromCore(Weapon(15, 999))));
            StringAssert.Contains("full", inventory.LastRejection);
            Assert.AreEqual(200, inventory.UsedSlots);
        }

        [Test]
        public void ConsumablesStackUpToTheLimitThenUseNewSlots()
        {
            var inventory = new HeroInventory(new EquipmentLoadout());
            Assert.IsTrue(inventory.TryAdd(InventoryEntry.FromRaw(Potion, 30)));
            Assert.IsTrue(inventory.TryAdd(InventoryEntry.FromRaw(Potion, 30)));
            Assert.AreEqual(60, inventory.CountOf(Potion));
            Assert.AreEqual(HeroInventory.MaxStack, inventory[0].Count);
            Assert.AreEqual(60 - HeroInventory.MaxStack, inventory[1].Count);
            Assert.AreEqual(2, inventory.UsedSlots);
        }

        [Test]
        public void EquipSwapsAndUnequipReturnsWithoutDuplicates()
        {
            var inventory = new HeroInventory(new EquipmentLoadout());
            ItemInstance a = Weapon(15, 1);
            ItemInstance b = Weapon(16, 2);
            inventory.TryEquipDirect(EquipmentSlot.MainHand, a);
            inventory.TryAdd(InventoryEntry.FromCore(b));
            var changes = new List<(EquipmentSlot, ItemInstance)>();
            inventory.EquipmentChanged += (slot, item) => changes.Add((slot, item));

            Assert.IsTrue(inventory.TryEquip(0, out EquipmentSlot slotUsed), inventory.LastRejection);
            Assert.AreEqual(EquipmentSlot.MainHand, slotUsed);
            Assert.AreSame(b, inventory.Loadout.GetEquipped(EquipmentSlot.MainHand));
            Assert.AreSame(a, inventory[0].Core, "the old weapon went back to the same bag slot");
            Assert.AreEqual(2, inventory.AllCoreItems().Count, "no item lost or duplicated");

            Assert.IsTrue(inventory.TryUnequip(EquipmentSlot.MainHand));
            Assert.IsNull(inventory.Loadout.GetEquipped(EquipmentSlot.MainHand));
            Assert.AreEqual(2, inventory.UsedSlots);
            Assert.AreEqual(2, inventory.AllCoreItems().Count);
            Assert.AreEqual(2, changes.Count);

            Assert.IsFalse(inventory.TryEquip(5, out _), "empty slot");
            inventory.TryAdd(InventoryEntry.FromRaw(Potion, 1));
            Assert.IsFalse(inventory.TryEquip(2, out _), "a consumable cannot be equipped");
        }

        [Test]
        public void ArmourGoesToItsSlotAndShieldToTheOffHand()
        {
            var inventory = new HeroInventory(new EquipmentLoadout());
            inventory.TryAdd(InventoryEntry.FromCore(new ItemInstance(ItemType.Chest, 0, 1, 0, 3)));
            inventory.TryAdd(InventoryEntry.FromCore(Weapon(HeroInventory.ShieldSubtype, 4)));
            Assert.IsTrue(inventory.TryEquip(0, out EquipmentSlot chest));
            Assert.AreEqual(EquipmentSlot.Chest, chest);
            Assert.IsTrue(inventory.TryEquip(1, out EquipmentSlot shield));
            Assert.AreEqual(EquipmentSlot.OffHand, shield);
            Assert.AreEqual(VisualEquipmentSlot.Hands, GameBootstrap.VisualSlotOf(EquipmentSlot.Gloves, null), "byte 5 = Alpha gloves");
            Assert.AreEqual(VisualEquipmentSlot.Feet, GameBootstrap.VisualSlotOf(EquipmentSlot.Boots, null), "byte 6 = Alpha boots");
        }

        [Test]
        public void LootUsesTheAlphaRarityRuleAndIsDeterministic()
        {
            List<InventoryEntry> first = LootRules.Roll(3, 1234);
            List<InventoryEntry> second = LootRules.Roll(3, 1234);
            Assert.AreEqual(first.Count, second.Count);
            Assert.AreEqual(first[0].Core, second[0].Core);
            Assert.AreEqual(3, first[0].Core.Level);

            int rare = 0;
            for (int seed = 0; seed < 4000; seed++)
            {
                List<InventoryEntry> drop = LootRules.Roll(1, seed);
                Assert.IsTrue(drop[0].IsCore);
                if (drop[0].Core.Rarity > 0)
                {
                    rare++;
                }
            }

            // Alpha I-03: +1 rarity with 1 % (then 0.1 %, 0.01 %) → about 1 % of drops are not common.
            Assert.That(rare, Is.InRange(15, 80), $"rare drops {rare}/4000");
        }

        [Test]
        public void PotionHealsUsesActionAndRespectsTheAlphaCooldown()
        {
            GameSession session = Running();
            session.ConsumableCooldownMs = Content.AlphaConsumableRules.CooldownMs;
            int kit = session.Hero.Inventory.CountOf(Potion);   // C1: the start kit already carries five (1,1)
            session.Hero.Inventory.TryAdd(InventoryEntry.FromRaw(Potion, 2));
            session.Hero.Health.ApplyDamage(200.0);
            double hurt = session.Hero.Health.CurrentHealth;
            int slot = session.Hero.Inventory.FindFirst(Potion);

            Assert.IsTrue(session.TryUseItem(slot), session.LastRejection);
            Assert.Greater(session.Hero.Health.CurrentHealth, hurt);
            Assert.AreEqual(kit + 1, session.Hero.Inventory.CountOf(Potion));
            Assert.IsTrue(session.Combat.IsActing(session.Hero.Id), "the use action 80 runs");

            Run(session, 2.0);
            Assert.IsFalse(session.TryUseItem(slot), "3 s cooldown of the life potion (Alpha item layer)");
            StringAssert.Contains("cooldown", session.LastRejection);
            Run(session, 1.2);
            Assert.IsTrue(session.TryUseItem(slot), session.LastRejection);
            Assert.AreEqual(kit, session.Hero.Inventory.CountOf(Potion));
        }

        [Test]
        public void KilledCreatureDropsLootThatTheHeroPicksUp()
        {
            GameSession session = Running();
            var wolf = Features.Combat.Combatant.CreateCreature(StableEntityId.ForRuntime(5), new KindId(0x1c), "Wolf", 2, 0);
            wolf.Position = new WorldPosition(30, 5, 30);
            session.Combat.Register(wolf);
            session.Hero.Position = new WorldPosition(0, 5, 0);
            wolf.Health.ApplyDamage(wolf.Health.MaxHealth);
            session.Events.Publish(new EntityDied { TimeMs = 0, Entity = wolf.Id, Killer = StableEntityId.Hero });
            Assert.GreaterOrEqual(session.WorldItems.Count, 1);
            int before = session.Hero.Inventory.UsedSlots;

            Run(session, 0.2);
            Assert.AreEqual(before, session.Hero.Inventory.UsedSlots, "too far to pick up");
            session.Hero.Position = new WorldPosition(30.5, 5, 30);
            Run(session, 0.2);
            Assert.AreEqual(0, session.WorldItems.Count, "picked up");
            Assert.Greater(session.Hero.Inventory.UsedSlots, before);
        }

        [Test]
        public void TravelStackSwapIsAtomicWhenBagsAreFull()
        {
            var inventory = new HeroInventory(new EquipmentLoadout());
            var glider = new RawAlphaItemId(0x17, 0);
            var boat = new RawAlphaItemId(0x17, 1);
            var lamp = new RawAlphaItemId(0x18, 0);
            Assert.IsTrue(inventory.TryPlace(0, InventoryEntry.FromRaw(glider, 1)));
            Assert.IsTrue(inventory.TryEquipExtra(0, ExtraEquipmentSlot.Travel));
            Assert.IsTrue(inventory.HangGliderEquipped);
            Assert.IsTrue(inventory.TryPlace(0, InventoryEntry.FromRaw(boat, 2)));
            for (int i = 1; i < HeroInventory.Capacity; i++)
            {
                Assert.IsTrue(inventory.TryPlace(i, InventoryEntry.FromRaw(lamp, 1)));
            }

            Assert.IsFalse(inventory.TryEquipExtra(0, ExtraEquipmentSlot.Light), "boat belongs in the travel slot");
            Assert.IsFalse(inventory.TryEquipExtra(0, ExtraEquipmentSlot.Travel));
            Assert.IsFalse(inventory.TryUnequipExtra(ExtraEquipmentSlot.Travel));
            Assert.IsTrue(inventory.HangGliderEquipped);
            Assert.AreEqual(2, inventory[0].Count);

            Assert.IsNotNull(inventory.TakeFrom(1));
            Assert.IsTrue(inventory.TryEquipExtra(0, ExtraEquipmentSlot.Travel));
            Assert.AreEqual(glider, inventory[1].Raw);
            Assert.AreEqual(1, inventory[0].Count);
            Assert.AreEqual(boat, inventory.GetExtra(ExtraEquipmentSlot.Travel).Raw);
            Assert.IsFalse(inventory.HangGliderEquipped, "a boat never enables gliding");
        }

        [Test]
        public void WholeInventorySurvivesACheckpoint()
        {
            var store = new MemoryStore();
            var slots = new GameSlotStore(store, new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());
            GameSession session = Running();
            HeroInventory inv = session.Hero.Inventory;
            inv.TryPlace(7, InventoryEntry.FromCore(Weapon(16, 77)));
            inv.TryPlace(60, InventoryEntry.FromCore(new ItemInstance(ItemType.Chest, 0, 2, 1, 5)));
            Assert.IsTrue(inv.TryEquip(60, out _));
            inv.TryPlace(12, InventoryEntry.FromRaw(Potion, 9));
            var glider = new RawAlphaItemId(0x17, 0);
            var necklace = new ItemInstance(ItemType.Amulet, 0, 2, 1, 42);
            Assert.IsTrue(inv.TryPlace(13, InventoryEntry.FromRaw(glider, 2)));
            Assert.IsTrue(inv.TryEquipExtra(13, ExtraEquipmentSlot.Travel));
            Assert.IsTrue(inv.TryPlace(14, InventoryEntry.FromCore(necklace)));
            Assert.IsTrue(inv.TryEquipExtra(14, ExtraEquipmentSlot.Neck));

            Assert.IsTrue(slots.Save(session, 1L).IsSuccess);
            SlotLoadResult loaded = slots.Load("slot0", "test");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            HeroInventory back = loaded.Hero.Inventory;

            Assert.AreEqual(Weapon(16, 77), back[7].Core);
            Assert.AreEqual(9, back[12].Count);
            Assert.AreEqual(Potion, back[12].Raw);
            Assert.AreEqual(ItemType.Chest, back.Loadout.GetEquipped(EquipmentSlot.Chest).Type);
            Assert.AreEqual(inv.Loadout.GetEquipped(EquipmentSlot.MainHand), back.Loadout.GetEquipped(EquipmentSlot.MainHand), "start kit weapon");
            Assert.IsTrue(back.HangGliderEquipped);
            Assert.AreEqual(glider, back[13].Raw);
            Assert.AreEqual(1, back[13].Count);
            Assert.AreEqual(necklace, back.GetExtra(ExtraEquipmentSlot.Neck).Core);
            Assert.AreEqual(inv.GetExtra(ExtraEquipmentSlot.Light).Raw, back.GetExtra(ExtraEquipmentSlot.Light).Raw);
            Assert.AreEqual(inv.GetExtra(ExtraEquipmentSlot.LeftRing).Core, back.GetExtra(ExtraEquipmentSlot.LeftRing).Core);
            Assert.AreEqual(inv.GetExtra(ExtraEquipmentSlot.RightRing).Core, back.GetExtra(ExtraEquipmentSlot.RightRing).Core);
            Assert.AreEqual(inv.UsedSlots, back.UsedSlots);
        }

        [Test]
        public void OldBagPlacementDoesNotAutoEquipTheAccessoriesOnLoad()
        {
            GameSession session = Running();
            HeroInventory inv = session.Hero.Inventory;
            Assert.IsTrue(inv.TryUnequipExtra(ExtraEquipmentSlot.Light));
            Assert.IsTrue(inv.TryUnequipExtra(ExtraEquipmentSlot.LeftRing));
            Assert.IsTrue(inv.TryUnequipExtra(ExtraEquipmentSlot.RightRing));
            var slots = new GameSlotStore(new MemoryStore(), new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());
            Assert.IsTrue(slots.Save(session, 1L).IsSuccess);

            SlotLoadResult loaded = slots.Load("slot0", "test");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            Assert.IsNull(loaded.Hero.Inventory.GetExtra(ExtraEquipmentSlot.Light));
            Assert.IsNull(loaded.Hero.Inventory.GetExtra(ExtraEquipmentSlot.LeftRing));
            Assert.IsNull(loaded.Hero.Inventory.GetExtra(ExtraEquipmentSlot.RightRing));
            Assert.AreEqual(1, loaded.Hero.Inventory.CountOf(new RawAlphaItemId(0x18, 0)));
            Assert.AreEqual(2, loaded.Hero.Inventory.CountOf(new RawAlphaItemId(9, 0)));
        }

        private static GameSession Running()
        {
            var session = new GameSession();
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 5, GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            return session;
        }

        private static void Run(GameSession session, double seconds)
        {
            for (double t = 0.0; t < seconds - 1e-9; t += 0.1)
            {
                session.AdvanceFrame(0.1);
            }
        }

        private sealed class MemoryStore : ISaveStore
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public bool Exists(string key) => _files.ContainsKey(key);

            public string Read(string key) => _files.TryGetValue(key, out string text) ? text : null;

            public void Write(string key, string text) => _files[key] = text;

            public void Delete(string key) => _files.Remove(key);
        }
    }
}
