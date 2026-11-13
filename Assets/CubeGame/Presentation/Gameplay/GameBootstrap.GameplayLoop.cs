using CubeGame.Content;
using CubeGame.Core.Characters;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Gameplay.Streaming;

namespace CubeGame.Gameplay
{
    public sealed partial class GameBootstrap
    {
        // CUBEGAME_CONTRACT: interaction radius.
        public const double InteractionRadius = 4.0;

        // P5: Alpha vendor stock (AlphaShop) per vendor NPC; generated once per session. The roll stream seed
        // (world seed and NPC id) is a CUBEGAME_CONTRACT, Alpha fills it from the global stream at town generation.
        private readonly System.Collections.Generic.Dictionary<StableEntityId, System.Collections.Generic.List<AlphaItemRecord>> _vendorStock =
            new System.Collections.Generic.Dictionary<StableEntityId, System.Collections.Generic.List<AlphaItemRecord>>();

        /// <summary>Alpha stock of the vendor NPC <paramref name="id"/> with role <paramref name="role"/> (empty for non-vendors).</summary>
        public System.Collections.Generic.IReadOnlyList<AlphaItemRecord> VendorStock(StableEntityId id, int role)
        {
            if (!AlphaShop.IsVendor(role))
            {
                return System.Array.Empty<AlphaItemRecord>();
            }

            if (!_vendorStock.TryGetValue(id, out var stock))
            {
                int seed = unchecked((int)(_session.World.Seed.Value * 2246822519u) ^ (int)id.Value ^ (int)(id.Value >> 32) ^ role);
                stock = AlphaShop.Stock(new Features.World.AlphaCrtRandom(seed), role);
                _vendorStock[id] = stock;
            }

            return stock;
        }
        private static readonly string[] SpecializationNames =
        {
            "Berserker", "Guardian", "Sniper", "Scout", "Fire", "Water", "Assassin", "Ninja",
        };

        private StableEntityId _townNpc;

        private StableEntityId NearestInteraction()
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

            return nearest;

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
                ShowPanel(GamePanel.Town);
                _panelDirty = true;
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
            if (!TryTownNpcInReach(_townNpc, out int role) || !AlphaShop.IsVendor(role))
            {
                return Report(false, "no such offer from an in-range merchant");
            }

            var stock = VendorStock(_townNpc, role);
            if ((uint)offerIndex >= stock.Count)
            {
                return Report(false, "no such offer from an in-range merchant");
            }

            AlphaItemRecord offer = stock[offerIndex];
            bool bought = _session.TryBuy(InventoryEntry.FromAlpha(offer));
            return Report(bought, bought ? $"bought {offer}" : _session.LastRejection);
        }

        public bool TrySellToVendor(int bagSlot)
        {
            if (!TryTownNpcInReach(_townNpc, out int role) || !AlphaShop.IsVendor(role))
            {
                return Report(false, "selling requires an in-range merchant");
            }

            bool sold = _session.TrySell(bagSlot);
            return Report(sold, sold ? "sold one item" : _session.LastRejection);
        }

        public bool TryBuyBackFromVendor(int index)
        {
            if (!TryTownNpcInReach(_townNpc, out int role) || !AlphaShop.IsVendor(role))
            {
                return Report(false, "buyback requires an in-range merchant");
            }

            bool bought = _session.TryBuyBack(index);
            return Report(bought, bought ? "bought back" : _session.LastRejection);
        }

        /// <summary>Identification and adaptation are offered by every vendor (CUBEGAME_CONTRACT: the Alpha NPC is not traced).</summary>
        public bool TryIdentifyAtVendor(int bagSlot)
        {
            if (!TryTownNpcInReach(_townNpc, out int role) || !AlphaShop.IsVendor(role))
            {
                return Report(false, "identification requires an in-range merchant");
            }

            bool done = _session.TryIdentify(bagSlot);
            return Report(done, done ? "identified" : _session.LastRejection);
        }

        public bool TryAdaptAtVendor(int bagSlot)
        {
            if (!TryTownNpcInReach(_townNpc, out int role) || !AlphaShop.IsVendor(role))
            {
                return Report(false, "adaptation requires an in-range merchant");
            }

            bool done = _session.TryAdapt(bagSlot);
            return Report(done, done ? "adapted" : _session.LastRejection);
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

            ShowPanel(GamePanel.None);
            Notify("Rested until 07:00");
            return Report(true, "rested until 07:00");
        }

        public bool TryBrewLifePotion()
        {
            bool brewed = _session.TryBrewLifePotion();
            if (brewed)
            {
                _audio.Play("craft.wav");
                Notify("Crafted one Life Potion");
            }
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
            // Alpha 0x5C9F90/0x5F8750: ten container slots at the chest level; delivery into the bags on interaction
            // is CUBEGAME_CONTRACT (the Alpha open path is not traced).
            if (!_session.TryOpenChest(spawn.Id, spawn.Definition.Level, out int delivered))
            {
                return Report(false, _session.LastRejection);
            }

            _population.MarkContainerOpened(spawn, _session.Clock.TimeMs);
            return Report(true, $"claimed {delivered} items from the chest");
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
            if (_population.TryGet(id, out LiveSpawn spawn) && spawn.Definition.BehaviourKey == TownNpcPopulation.BehaviourKey
                && _session.Combat.TryGet(id, out Combatant service) && !service.IsDead)
            {
                role = spawn.Definition.ClassId;
                position = service.Position;
            }
            else if (_recoveredPopulation != null && _recoveredPopulation.TryGet(id, out AlphaSpawnTemplate template)
                && template.Live && template.Definition.Faction == (int)CreatureFaction.Npc
                && _session.Combat.TryGet(id, out Combatant actor) && !actor.IsDead)
            {
                role = template.Definition.ClassId;
                position = actor.Position;
            }

            return AlphaShop.IsVendor(role) || role == TownNpcPopulation.InnkeeperClass
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

        private static string SpecializationName(CharacterClass heroClass, CharacterSpecialization specialization) =>
            SpecializationNames[2 * (int)heroClass + (int)specialization];
    }
}
