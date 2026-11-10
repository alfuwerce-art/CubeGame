using CubeGame.Content;
using CubeGame.Core.Characters;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Gameplay.Streaming;
using UnityEngine;

namespace CubeGame.Gameplay
{
    public sealed partial class GameBootstrap
    {
        // CUBEGAME_CONTRACT: interaction radius and small repeatable assortment.
        public const double InteractionRadius = 4.0;
        private static readonly (RawAlphaItemId Item, string Label)[] ShopStock =
        {
            (LootRules.LifePotion, "Life Potion"),
            (AlphaLifePotionRecipe.Heartflower, "Heartflower"),
            (AlphaLifePotionRecipe.WaterFlask, "Water Flask"),
        };
        private static readonly string[] SpecializationNames =
        {
            "Berserker", "Guardian", "Sniper", "Scout", "Fire", "Water", "Assassin", "Ninja",
        };

        private StableEntityId _townNpc;
        private bool _journalOpen;
        private Vector2 _gameplayPanelScroll;
        private CharacterSpecialization _specializationDraft;

        private bool GameplayPanelOpen => !_townNpc.IsNone || _journalOpen;

        private void UpdateGameplayInteractions()
        {
            if (HeroDead || _inventoryOpen || _localMap != null)
            {
                return;
            }

            if (!_townNpc.IsNone && !TryTownNpcInReach(_townNpc, out _))
            {
                CloseGameplayPanel();
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                if (_journalOpen)
                {
                    CloseGameplayPanel();
                }
                else
                {
                    _townNpc = default;
                    _journalOpen = true;
                    _gameplayPanelScroll = Vector2.zero;
                    SetGameplayInput(false);
                }
            }
            if (!GameplayPanelOpen && Input.GetKeyDown(KeyCode.E))
            {
                InteractNearest();
            }
        }

        private void InteractNearest()
        {
            StableEntityId nearest = default;
            double bestDistance = InteractionRadius * InteractionRadius;
            foreach (LiveSpawn spawn in _population.Live)
            {
                if (spawn.Definition.BehaviourKey == AlphaDungeonContentProvider.ChestBehaviourKey
                    || spawn.Definition.BehaviourKey == TownNpcPopulation.BehaviourKey)
                {
                    Consider(spawn.Id, spawn.Definition.Position);
                }
            }

            foreach (Combatant actor in _session.Combat.Combatants)
            {
                if (TryTownNpc(actor.Id, out _, out WorldPosition position))
                {
                    Consider(actor.Id, position);
                }
            }

            if (!nearest.IsNone)
            {
                TryInteract(nearest);
            }

            void Consider(StableEntityId id, WorldPosition position)
            {
                double distance = DistanceToHeroSquared(position);
                if (distance <= bestDistance && (distance < bestDistance || nearest.IsNone || id.Value < nearest.Value))
                {
                    nearest = id;
                    bestDistance = distance;
                }
            }
        }

        /// <summary>Opens an in-range NPC service or claims an in-range dungeon chest.</summary>
        public bool TryInteract(StableEntityId id)
        {
            if (_session.State != SessionState.InGame || HeroDead)
            {
                return Report(false, "interaction requires a living hero");
            }

            SyncHeroFromView();
            if (TryTownNpcInReach(id, out _))
            {
                _townNpc = id;
                _journalOpen = false;
                _gameplayPanelScroll = Vector2.zero;
                SetGameplayInput(false);
                return true;
            }

            if (_population.TryGet(id, out LiveSpawn spawn)
                && spawn.Definition.BehaviourKey == AlphaDungeonContentProvider.ChestBehaviourKey
                && InReach(spawn.Definition.Position))
            {
                return TryClaimChest(spawn);
            }

            return Report(false, "nothing to interact with in reach");
        }

        public bool TryBuyFromVendor(int offerIndex)
        {
            if (!TryTownNpcInReach(_townNpc, out int role) || role != TownNpcPopulation.MerchantClass
                || (uint)offerIndex >= ShopStock.Length)
            {
                return Report(false, "no such offer from an in-range merchant");
            }

            var offer = ShopStock[offerIndex];
            bool bought = _session.TryBuy(InventoryEntry.FromRaw(offer.Item, 1));
            return Report(bought, bought ? $"bought {offer.Label}" : _session.LastRejection);
        }

        public bool TrySellToVendor(int bagSlot)
        {
            if (!TryTownNpcInReach(_townNpc, out int role) || role != TownNpcPopulation.MerchantClass)
            {
                return Report(false, "selling requires an in-range merchant");
            }

            bool sold = _session.TrySell(bagSlot);
            return Report(sold, sold ? "sold one item" : _session.LastRejection);
        }

        public bool TryRestAtInn()
        {
            if (!TryTownNpcInReach(_townNpc, out int role) || role != TownNpcPopulation.InnkeeperClass)
            {
                return Report(false, "rest requires an in-range innkeeper");
            }

            if (!_session.TryRestAtInn())
            {
                return Report(false, _session.LastRejection);
            }

            CloseGameplayPanel();
            return Report(true, "rested until 07:00");
        }

        public bool TryBrewLifePotion()
        {
            bool brewed = _session.TryBrewLifePotion();
            return Report(brewed, brewed ? "crafted one Life Potion" : _session.LastRejection);
        }

        public bool TryApplyHeroProgression(HeroSkillRanks draft, CharacterSpecialization specialization)
        {
            bool trainerNearby = false;
            if (_session.State == SessionState.InGame && !HeroDead)
            {
                SyncHeroFromView();
                int trainerClass = (int)_session.Hero.Sheet.Class + 1;
                foreach (LiveSpawn spawn in _population.Live)
                {
                    // 0x47F030: same class and strictly within 4 blocks. Trainer placement is a CubeGame contract.
                    if (spawn.Definition.BehaviourKey == TownNpcPopulation.BehaviourKey && spawn.Definition.ClassId == trainerClass
                        && DistanceToHeroSquared(spawn.Definition.Position) < InteractionRadius * InteractionRadius)
                    {
                        trainerNearby = true;
                        break;
                    }
                }
            }

            bool applied = _session.TryApplyProgression(draft, specialization, trainerNearby);
            return Report(applied, applied ? "skill changes applied" : _session.LastRejection);
        }

        private void BeginSkillDraft(HeroState hero)
        {
            _skillDraft = new HeroSkillRanks();
            _skillDraft.TryReplace(hero.SkillRanks.ToArray());
            _specializationDraft = hero.Sheet.Specialization;
        }

        private bool TryClaimChest(LiveSpawn spawn)
        {
            HeroInventory inventory = _session.Hero.Inventory;
            if (HeroInventory.Capacity - inventory.UsedSlots < AlphaDungeonRules.ChestItemCount)
            {
                return Report(false, $"the chest needs {AlphaDungeonRules.ChestItemCount} free bag slots");
            }

            // Q-17: 10 items. The composition is CubeGame's existing equipment loot rule.
            int seed = unchecked((int)(_session.World.Seed.Value ^ spawn.Id.Value ^ (spawn.Id.Value >> 32)));
            for (int i = 0; i < AlphaDungeonRules.ChestItemCount; i++)
            {
                inventory.TryAdd(LootRules.Roll(spawn.Definition.Level, unchecked(seed + i * 1_000_003))[0]);
            }
            _population.MarkContainerOpened(spawn, _session.Clock.TimeMs);
            return Report(true, $"claimed {AlphaDungeonRules.ChestItemCount} items from the chest");
        }

        private bool TryTownNpcInReach(StableEntityId id, out int role)
        {
            role = -1;
            return _session.State == SessionState.InGame && !HeroDead && !id.IsNone
                && TryTownNpc(id, out role, out WorldPosition position) && InReach(position);
        }

        private bool TryTownNpc(StableEntityId id, out int role, out WorldPosition position)
        {
            role = -1;
            position = default;
            if (_population.TryGet(id, out LiveSpawn spawn) && spawn.Definition.BehaviourKey == TownNpcPopulation.BehaviourKey)
            {
                role = spawn.Definition.ClassId;
                position = spawn.Definition.Position;
            }
            else if (_recoveredPopulation != null && _recoveredPopulation.TryGet(id, out AlphaSpawnTemplate template)
                && template.Live && template.Definition.Faction == (int)CreatureFaction.Npc
                && _session.Combat.TryGet(id, out Combatant actor) && !actor.IsDead)
            {
                role = template.Definition.ClassId;
                position = actor.Position;
            }

            return role == TownNpcPopulation.MerchantClass || role == TownNpcPopulation.InnkeeperClass
                || (TownNpcPopulation.IsTrainer(role) && _population.TryGet(id, out _));
        }

        private bool InReach(WorldPosition position) => DistanceToHeroSquared(position) <= InteractionRadius * InteractionRadius;

        private double DistanceToHeroSquared(WorldPosition position)
        {
            WorldPosition hero = _session.Hero.Position;
            double dx = position.X - hero.X;
            double dy = position.Y - hero.Y;
            double dz = position.Z - hero.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        private void CloseGameplayPanel()
        {
            if (!GameplayPanelOpen)
            {
                return;
            }

            _townNpc = default;
            _journalOpen = false;
            SetGameplayInput(_session.State == SessionState.InGame && !HeroDead && !_inventoryOpen && _localMap == null);
        }

        private void DrawGameplayPanel()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 470f, 40f, 450f, Screen.height - 80f), GUI.skin.box);
            if (GUILayout.Button("Close [Esc]"))
            {
                CloseGameplayPanel();
                GUILayout.EndArea();
                return;
            }

            _gameplayPanelScroll = GUILayout.BeginScrollView(_gameplayPanelScroll);
            if (_journalOpen)
            {
                GUILayout.Label("Mission journal");
                if (_session.Hero.Missions.Hunts.Count == 0)
                {
                    GUILayout.Label("Approach a hostile creature to discover a regional hunt.");
                }
                foreach (HuntMission hunt in _session.Hero.Missions.Hunts)
                {
                    string name = AlphaCreatureCatalog.TryKind(hunt.Kind.Value, out var kind) && !string.IsNullOrEmpty(kind.NameHint)
                        ? kind.NameHint : hunt.Kind.ToString();
                    GUILayout.Label($"Cell {hunt.CellX}, {hunt.CellZ}  |  {name}  |  {(hunt.Complete ? "Completed (+1 mana cube)" : "Active")}");
                }
            }
            else if (TryTownNpcInReach(_townNpc, out int role))
            {
                if (role == TownNpcPopulation.MerchantClass)
                {
                    DrawVendor();
                }
                else if (role == TownNpcPopulation.InnkeeperClass)
                {
                    GUILayout.Label("Innkeeper  |  Rest from 18:00 to 06:00");
                    if (GUILayout.Button("Rest until 07:00"))
                    {
                        TryRestAtInn();
                    }
                }
                else
                {
                    CharacterClass trainerClass = CharacterClasses.FromId(role - 1);
                    GUILayout.Label($"{trainerClass} trainer");
                    if (trainerClass == _session.Hero.Sheet.Class && GUILayout.Button("Open Skills"))
                    {
                        CloseGameplayPanel();
                        ToggleInventory();
                        BeginSkillDraft(_session.Hero);
                    }
                }
            }
            GUILayout.Label(_status);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawVendor()
        {
            GUILayout.Label($"Merchant  |  Wallet {_session.Hero.Copper} copper");
            for (int i = 0; i < ShopStock.Length; i++)
            {
                var offer = ShopStock[i];
                int price = AlphaTradeValue.Of(InventoryEntry.FromRaw(offer.Item, 1));
                if (GUILayout.Button($"Buy {offer.Label} ({price} copper)"))
                {
                    TryBuyFromVendor(i);
                }
            }

            GUILayout.Label("Sell one item");
            for (int slot = 0; slot < HeroInventory.Capacity; slot++)
            {
                InventoryEntry item = _session.Hero.Inventory[slot];
                if (item != null && GUILayout.Button($"{item} (+{AlphaTradeValue.Of(item)} copper)"))
                {
                    TrySellToVendor(slot);
                }
            }
        }

        private static string SpecializationName(CharacterClass heroClass, CharacterSpecialization specialization) =>
            SpecializationNames[2 * (int)heroClass + (int)specialization];
    }
}
