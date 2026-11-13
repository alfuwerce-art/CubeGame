using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Core.Progression;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.World;

namespace CubeGame.Features.Session
{
    public enum SessionState
    {
        Boot = 0,
        MainMenu = 1,
        LoadingWorld = 2,
        InGame = 3,
        Paused = 4,
        Saving = 5,
        Unloading = 6,
        Quit = 7,
    }

    /// <summary>Identity of one world (a save slot holds exactly one world and one hero, as in Alpha single player).</summary>
    public sealed class WorldInfo
    {
        public WorldInfo(string name, WorldSeed seed, string generatorId, long createdUtcTicks)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "World" : name.Trim();
            Seed = seed;
            GeneratorId = string.IsNullOrEmpty(generatorId) ? throw new ArgumentException("generatorId required") : generatorId;
            CreatedUtcTicks = createdUtcTicks;
        }

        public string Name { get; }

        public WorldSeed Seed { get; }

        /// <summary>Generator id + version; a save made with another generator is refused, never silently regenerated.</summary>
        public string GeneratorId { get; }

        public long CreatedUtcTicks { get; }
    }

    /// <summary>The player's hero inside a session. Rules live in Core <see cref="CharacterSheet"/>.</summary>
    public sealed class HeroState
    {
        public HeroState(string name, int raceId, CharacterSheet sheet)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Hero" : name.Trim();
            HeroAppearance d = AlphaHeroCreation.Default;
            Appearance = AlphaHeroCreation.Normalize(new HeroAppearance(raceId < 0 ? 0 : raceId, d.Gender, d.Face, d.Haircut, d.HairR, d.HairG, d.HairB));
            Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            Loadout = new EquipmentLoadout();
            Inventory = new HeroInventory(Loadout);
            Health = new HealthPool(EquipmentCombatAdapter.ComputeMaxHealth(sheet));
        }

        /// <summary>
        /// Gives the Alpha class start kit (C1, <see cref="AlphaHeroCreation.StarterKit"/>): weapons, rings and lamp
        /// into their recorded slots, the remaining items into the bags.
        /// </summary>
        public void GiveStarterKit()
        {
            foreach (StarterKitItem item in AlphaHeroCreation.StarterKit(Sheet.Class))
            {
                bool done;
                if (item.Core != null && item.Equip.HasValue)
                {
                    done = Inventory.TryEquipDirect(item.Equip.Value, item.Core);
                }
                else
                {
                    InventoryEntry entry = item.Core != null ? InventoryEntry.FromCore(item.Core) : InventoryEntry.FromRaw(item.Raw, item.Count);
                    var extraSlot = (ExtraEquipmentSlot)item.AlphaSlotOffset;
                    done = HeroInventory.CanEquipExtra(extraSlot, entry)
                        ? Inventory.TryEquipExtraDirect(extraSlot, entry)
                        : Inventory.TryAdd(entry);
                }

                if (!done)
                {
                    throw new InvalidOperationException($"start kit item refused: {Inventory.LastRejection}");
                }
            }

            SyncItemsFromInventory();
        }

        public StableEntityId Id => StableEntityId.Hero;

        public string Name { get; }

        /// <summary>Race 0…7 of the hero editor (Alpha widget +0x18C; C1). Saves written before C1 read as Human.</summary>
        public int RaceId => Appearance.Race;

        /// <summary>Hero editor choice: race, gender, face, haircut, hair colour (C1).</summary>
        public HeroAppearance Appearance { get; set; }

        /// <summary>Alpha species of the hero (race + gender, creature +0x64) — what the hero looks like.</summary>
        public KindId Kind => AlphaHeroCreation.KindOf(Appearance);

        public CharacterSheet Sheet { get; private set; }

        public HeroSkillRanks SkillRanks { get; } = new HeroSkillRanks();

        public int Copper { get; internal set; }

        public int ManaCubes { get; internal set; }

        public MissionJournal Missions { get; } = new MissionJournal();

        internal void ChangeSpecialization(CharacterSpecialization specialization)
        {
            var previous = Sheet;
            Sheet = new CharacterSheet(previous.Class, specialization, previous.BaseHealth, previous.WeaponBase)
            {
                EquipmentHealthBonus = previous.EquipmentHealthBonus,
                WeaponUpgrades = previous.WeaponUpgrades,
            };
            Sheet.RestoreProgression(previous.Progression.Level, previous.Progression.Experience);
            double current = Health.CurrentHealth;
            Revive();
            RestoreHealth(current);
        }

        /// <summary>
        /// Core items the Core save v1 stores (bags + equipment). The live state is <see cref="Inventory"/>;
        /// call <see cref="SyncItemsFromInventory"/> before saving.
        /// </summary>
        public List<ItemInstance> Items { get; } = new List<ItemInstance>();

        /// <summary>Equipped Core items (S04 loadout, shared with <see cref="Inventory"/> and the hero combatant).</summary>
        public EquipmentLoadout Loadout { get; }

        /// <summary>Bags (4 × 50, Alpha I-06) and equipment operations (M0-04B).</summary>
        public HeroInventory Inventory { get; }

        public void SyncItemsFromInventory()
        {
            Items.Clear();
            Items.AddRange(Inventory.AllCoreItems());
        }

        /// <summary>Current HP; maximum from Core <see cref="EquipmentCombatAdapter.ComputeMaxHealth"/>.</summary>
        public HealthPool Health { get; private set; }

        /// <summary>Replaces a dead hero's HP pool with a full one (Core HealthPool never revives by design).</summary>
        public void Revive()
        {
            Health = new HealthPool(EquipmentCombatAdapter.ComputeMaxHealth(Sheet));
        }

        /// <summary>
        /// Rebuilds bags and equipment from <see cref="Items"/> (Core save order) and a placement list from the
        /// session file: placement[i] ≥ 0 is a bag slot, −(1+slot) is a Core index or extra Alpha slot offset. Without valid placement
        /// (slots saved before M0-04B) every item goes to the bags and the first weapon to the main hand
        /// (CubeGame rule, PLACEHOLDER_NOT_ALPHA). Returns false when a placement entry could not be honoured.
        /// </summary>
        public bool RestoreInventory(IReadOnlyList<int> placement)
        {
            Inventory.Clear();
            bool exact = placement != null && placement.Count == Items.Count;
            bool ok = true;
            for (int i = 0; i < Items.Count; i++)
            {
                ItemInstance item = Items[i];
                bool placed = false;
                if (exact)
                {
                    int code = placement[i];
                    InventoryEntry entry = InventoryEntry.FromCore(item);
                    placed = code >= 0
                        ? Inventory.TryPlace(code, entry)
                        : Inventory.TryRestoreExtra(code, entry) || Inventory.TryEquipDirect((EquipmentSlot)(-(code + 1)), item);
                    ok &= placed;
                }
                else if (item.Type == ItemType.Weapon && Loadout.IsSlotEmpty(EquipmentSlot.MainHand))
                {
                    placed = Inventory.TryEquipDirect(EquipmentSlot.MainHand, item);
                }

                if (!placed && !Inventory.TryAdd(InventoryEntry.FromCore(item)))
                {
                    ok = false;
                }
            }

            return ok;
        }

        /// <summary>Placement codes for <see cref="Items"/> after <see cref="SyncItemsFromInventory"/> (same order).</summary>
        public int[] InventoryPlacement()
        {
            var codes = new List<int>();
            foreach (EquipmentSlot slot in EquipmentLoadout.Slots)
            {
                if (Loadout.GetEquipped(slot) != null)
                {
                    codes.Add(-1 - (int)slot);
                }
            }

            foreach (ExtraEquipmentSlot slot in HeroInventory.ExtraSlots)
            {
                if (Inventory.GetExtra(slot)?.IsCore == true)
                {
                    codes.Add(HeroInventory.ExtraPlacement(slot));
                }
            }

            for (int i = 0; i < HeroInventory.Capacity; i++)
            {
                InventoryEntry entry = Inventory[i];
                if (entry != null && entry.IsCore)
                {
                    codes.Add(i);
                }
            }

            return codes.ToArray();
        }

        /// <summary>Restores current HP from a save (values above maximum or non-finite are clamped/ignored).</summary>
        public void RestoreHealth(double current)
        {
            if (double.IsNaN(current) || double.IsInfinity(current) || current < 0.0 || current >= Health.CurrentHealth)
            {
                return;
            }

            Health.ApplyDamage(Health.CurrentHealth - current);
        }

        public WorldPosition Position { get; set; }

        public float Yaw { get; set; }

        /// <summary>True until the presentation placed the hero on generated ground (new world only).</summary>
        public bool NeedsGroundPlacement { get; set; }
    }

    public sealed class NewWorldRequest
    {
        public string WorldName { get; set; } = "World";

        public uint Seed { get; set; }

        public string GeneratorId { get; set; } = string.Empty;

        public string HeroName { get; set; } = "Hero";

        /// <summary>Hero editor choice (C1); face/haircut are wrapped into the species ranges on creation.</summary>
        public HeroAppearance Appearance { get; set; } = AlphaHeroCreation.Default;

        public CharacterClass Class { get; set; } = CharacterClass.Warrior;

        public CharacterSpecialization Specialization { get; set; } = CharacterSpecialization.Primary;

        public WorldPosition Spawn { get; set; }
    }

    /// <summary>
    /// Single owner of the running game (M0-00C §2). Holds the world identity, the hero, the clock and
    /// the event bus; everything is created on LoadingWorld and dropped on Unloading. Illegal transitions
    /// are refused with a reason, never applied silently.
    /// </summary>
    public sealed class GameSession
    {
        private static readonly Dictionary<SessionState, SessionState[]> Allowed = new Dictionary<SessionState, SessionState[]>
        {
            { SessionState.Boot, new[] { SessionState.MainMenu, SessionState.Quit } },
            { SessionState.MainMenu, new[] { SessionState.LoadingWorld, SessionState.Quit } },
            { SessionState.LoadingWorld, new[] { SessionState.InGame, SessionState.MainMenu } },
            { SessionState.InGame, new[] { SessionState.Paused, SessionState.Saving, SessionState.Unloading } },
            { SessionState.Paused, new[] { SessionState.InGame, SessionState.Saving, SessionState.Unloading } },
            { SessionState.Saving, new[] { SessionState.InGame, SessionState.Paused, SessionState.Unloading } },
            { SessionState.Unloading, new[] { SessionState.MainMenu, SessionState.Quit } },
            { SessionState.Quit, Array.Empty<SessionState>() },
        };

        private SessionState _stateBeforeSave = SessionState.InGame;

        public GameSession(IActionTimingSource actionTimings = null)
        {
            Events = new GameEventBus();
            ActionTimings = actionTimings;
        }

        /// <summary>Optional explicit action timing overrides for test sessions.</summary>
        public IActionTimingSource ActionTimings { get; set; }

        /// <summary>Combat of the running world (M0-05A first slice); created on LoadingWorld, advanced by clock ticks.</summary>
        public CombatSystem Combat { get; private set; }

        /// <summary>Items lying in the running world (loot); runtime only.</summary>
        public WorldItems WorldItems { get; private set; }

        /// <summary>
        /// Consumable cooldown in ms for an Alpha (type, subtype); injected from the content layer
        /// (AlphaConsumableRules: 3 s life potion, 10 s others — EVIDENCED_COOLDOWN). Null = no cooldown.
        /// </summary>
        public Func<int, int, int> ConsumableCooldownMs { get; set; }

        /// <summary>
        /// Share of max HP a life potion restores. PLACEHOLDER_NOT_ALPHA: the Alpha heal amount is not
        /// restored (B-U1 open).
        /// </summary>
        public const double LifePotionHealShare = 0.5;

        /// <summary>Alpha action started when the hero uses a consumable (80, item layer consumable.actions).</summary>
        public static readonly ActionId UseItemAction = new ActionId(80);

        private readonly Dictionary<RawAlphaItemId, long> _cooldownUntilMs = new Dictionary<RawAlphaItemId, long>();

        public SessionState State { get; private set; } = SessionState.Boot;

        public GameEventBus Events { get; }

        public WorldInfo World { get; private set; }

        public HeroState Hero { get; private set; }

        public GameClock Clock { get; private set; }

        /// <summary>Persistent world changes and explored regions of the running world (M0-02).</summary>
        public WorldDeltaStore WorldDeltas { get; private set; }

        public string SlotKey { get; private set; }

        public string LastRejection { get; private set; } = string.Empty;

        public bool HasWorld => World != null;

        public bool CanTransition(SessionState target)
        {
            return Array.IndexOf(Allowed[State], target) >= 0;
        }

        public bool EnterMainMenu()
        {
            return TryTransition(SessionState.MainMenu);
        }

        /// <summary>
        /// Builds the hero of an editor decision exactly as <see cref="BeginNewWorld"/> does, without attaching a world.
        /// Alpha confirms the character in the editor (0x4821A0 saves the creature) before a world is chosen.
        /// </summary>
        public static HeroState CreateHero(NewWorldRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var sheet = new CharacterSheet(request.Class, request.Specialization);
            var hero = new HeroState(AlphaHeroCreation.NormalizeName(request.HeroName), 0, sheet)
            {
                Position = request.Spawn,
                NeedsGroundPlacement = true,
                Appearance = AlphaHeroCreation.Normalize(request.Appearance),
            };
            hero.GiveStarterKit();
            return hero;
        }

        /// <summary>MainMenu → LoadingWorld with a freshly created world and hero.</summary>
        public bool BeginNewWorld(string slotKey, NewWorldRequest request, long nowUtcTicks)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrEmpty(slotKey))
            {
                return Reject("slot key is empty");
            }

            if (!CanTransition(SessionState.LoadingWorld))
            {
                return Reject($"cannot start a new world from {State}");
            }

            HeroState hero = CreateHero(request);
            var world = new WorldInfo(request.WorldName, new WorldSeed(request.Seed), request.GeneratorId, nowUtcTicks);
            return Attach(slotKey, world, hero, new GameClock(), new WorldDeltaStore());
        }

        /// <summary>MainMenu → LoadingWorld with state restored from a save slot.</summary>
        public bool BeginLoadedWorld(string slotKey, WorldInfo world, HeroState hero, long clockTick, WorldDeltaStore deltas = null,
            long worldTimeOffsetMs = 0)
        {
            if (world == null || hero == null)
            {
                return Reject("loaded world or hero is missing");
            }

            if (!CanTransition(SessionState.LoadingWorld))
            {
                return Reject($"cannot load a world from {State}");
            }

            return Attach(slotKey, world, hero, new GameClock(GameClock.DefaultTicksPerSecond, clockTick, worldTimeOffsetMs),
                deltas ?? new WorldDeltaStore());
        }

        public bool CompleteLoading()
        {
            return TryTransition(SessionState.InGame);
        }

        /// <summary>LoadingWorld → MainMenu when the presentation could not build the world.</summary>
        public bool AbortLoading(string reason)
        {
            if (State != SessionState.LoadingWorld)
            {
                return Reject($"abort requested in {State}");
            }

            DropWorld();
            LastRejection = reason ?? string.Empty;
            return TryTransition(SessionState.MainMenu);
        }

        public bool Pause()
        {
            return TryTransition(SessionState.Paused);
        }

        public bool Resume()
        {
            return TryTransition(SessionState.InGame);
        }

        public bool BeginSave()
        {
            SessionState previous = State;
            if (!TryTransition(SessionState.Saving))
            {
                return false;
            }

            _stateBeforeSave = previous;
            return true;
        }

        public bool EndSave()
        {
            if (State != SessionState.Saving)
            {
                return Reject($"EndSave in {State}");
            }

            return TryTransition(_stateBeforeSave);
        }

        /// <summary>InGame/Paused/Saving → Unloading → MainMenu: drops the world, the hero, the clock and all subscribers.</summary>
        public bool ExitToMenu()
        {
            if (!TryTransition(SessionState.Unloading))
            {
                return false;
            }

            DropWorld();
            return TryTransition(SessionState.MainMenu);
        }

        public bool Quit()
        {
            if (State == SessionState.InGame || State == SessionState.Paused || State == SessionState.Saving)
            {
                if (!TryTransition(SessionState.Unloading))
                {
                    return false;
                }

                DropWorld();
            }

            return TryTransition(SessionState.Quit);
        }

        /// <summary>Advances simulation time; only runs in InGame (pause stops the clock).</summary>
        public int AdvanceFrame(double realSeconds)
        {
            if (State != SessionState.InGame || Clock == null)
            {
                return 0;
            }

            return Clock.Advance(realSeconds);
        }

        private bool Attach(string slotKey, WorldInfo world, HeroState hero, GameClock clock, WorldDeltaStore deltas)
        {
            SlotKey = slotKey;
            World = world;
            Hero = hero;
            Clock = clock;
            WorldDeltas = deltas;
            Combat = new CombatSystem(Events, ActionTimings, unchecked((int)world.Seed.Value));
            RegisterHeroCombatant();
            Clock.Ticked += OnClockTicked;
            WorldItems = new WorldItems(Events);
            _cooldownUntilMs.Clear();
            Events.Subscribe<EntityDied>(OnEntityDied);
            if (!TryTransition(SessionState.LoadingWorld))
            {
                DropWorld();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Species whose action timings the hero's combat uses: Alpha HumanMale (0x02), the only player species with
        /// captured action clips. Alpha player attack timing comes from class/weapon, not species (C4); the hero's look
        /// is <see cref="HeroState.Kind"/>.
        /// </summary>
        public static readonly KindId HeroKind = new KindId(0x02);

        /// <summary>
        /// Brings a dead hero back with full HP and a fresh combat registration. Where and when is decided by the
        /// caller (Alpha respawn rule GS-06 is not restored: PLACEHOLDER_NOT_ALPHA).
        /// </summary>
        public bool ReviveHero()
        {
            if (Hero == null || Combat == null || !Hero.Health.IsDead)
            {
                return Reject("revive refused: no dead hero in a running world");
            }

            Hero.Revive();
            Combat.Unregister(Hero.Id);
            RegisterHeroCombatant();
            return true;
        }

        private void OnClockTicked(GameClock clock)
        {
            if (Combat == null || Hero == null)
            {
                return;
            }

            if (Combat.TryGet(Hero.Id, out Combatant heroCombatant))
            {
                heroCombatant.Position = Hero.Position;
                heroCombatant.Yaw = Hero.Yaw;
            }

            if (!Hero.Health.IsDead && Hero.Missions.TryActivateNear(Hero.Position, Combat.Combatants, out HuntMission hunt))
            {
                Events.Publish(new HuntChanged { TimeMs = clock.TimeMs, Kind = hunt.Kind });
            }
            Combat.Update(clock.TimeMs);
            if (_levelUpHealPending)
            {
                _levelUpHealPending = false;
                if (!Hero.Health.IsDead)
                {
                    Hero.Revive();
                    if (Combat.TryGet(Hero.Id, out Combatant leveledHero)) leveledHero.Health = Hero.Health;
                }
            }
            if (!Hero.Health.IsDead && WorldItems != null && WorldItems.Count > 0)
            {
                WorldItems.PickupNear(Hero, clock.TimeMs);
            }
        }

        private void RegisterHeroCombatant()
        {
            Combat.Register(new Combatant(Hero.Id, HeroKind, Hero.Name, Hero.Sheet, Hero.Loadout, Hero.Health)
            {
                Position = Hero.Position,
                Yaw = Hero.Yaw,
            });
        }

        /// <summary>
        /// Kill experience of Alpha (applyMeleeHit 0x595A60, C3, DIRECT): <c>max(1, curve_level05(level + stars)·20)</c>,
        /// ×20 for a boss species, converted to int; given to the killing player for a hostile creature (role 1).
        /// The ×10 (flag +0x7E&amp;0x200) and 0 (flag 0x800) cases are not mapped (flags unknown in CubeGame).
        /// </summary>
        public static long KillExperience(int level, int stars, bool boss)
        {
            double xp = CubeGame.Core.Progression.AlphaMath.KillExperience(level, stars);
            if (boss)
            {
                xp *= 20.0;
            }

            return (long)xp;
        }

        private bool _levelUpHealPending;

        /// <summary>Loot of a killed creature falls where it died (hero deaths drop nothing); the hero gets kill experience.</summary>
        private void OnEntityDied(EntityDied died)
        {
            if (died.Entity.Equals(StableEntityId.Hero) || Combat == null
                || !Combat.TryGet(died.Entity, out Combatant dead))
            {
                return;
            }

            if (Hero != null && died.Killer.Equals(Hero.Id) && dead.Hostile && !Hero.Health.IsDead)
            {
                if (Hero.Missions.TryComplete(died.Entity))
                {
                    if (Hero.ManaCubes < int.MaxValue)
                    {
                        Hero.ManaCubes++;
                    }
                    Events.Publish(new HuntChanged { TimeMs = died.TimeMs, Kind = dead.Kind, Complete = true });
                }
                long xp = KillExperience(dead.Sheet.Progression.Level, dead.Stars, dead.IsBoss);
                int levels = Hero.Sheet.AddExperience(xp);
                // xp_applyLevelUps 0x447B00: every level-up sets HP to the new maximum; the pool is rebuilt after the
                // combat update (the death arrives from inside it).
                _levelUpHealPending |= levels > 0;
                Events.Publish(new ExperienceGained
                {
                    TimeMs = died.TimeMs,
                    Source = died.Entity,
                    Amount = xp,
                    LevelsGained = levels,
                    Level = Hero.Sheet.Progression.Level,
                });
            }

            if (WorldItems == null)
            {
                return;
            }

            int seed = unchecked((int)(World.Seed.Value * 2654435761u) ^ (int)died.Entity.Value ^ (int)(died.Entity.Value >> 32));
            foreach (InventoryEntry entry in LootRules.Roll(dead.Sheet.Progression.Level, seed))
            {
                WorldItems.Drop(entry, dead.Position, died.TimeMs);
            }
        }

        /// <summary>Confirms increases funded by level points. Rank decreases require the paid reset path.</summary>
        public bool TryApplySkillRanks(HeroSkillRanks draft)
        {
            if (State != SessionState.InGame || Hero == null || Hero.Health.IsDead)
            {
                return Reject("skill changes require a living hero in a running world");
            }

            if (!Hero.SkillRanks.TryApplyIncreases(draft, Hero.Sheet.Progression.Level, Hero.ManaCubes))
            {
                return Reject("invalid skill increases or paid reset required");
            }

            LastRejection = string.Empty;
            return true;
        }

        public bool TryApplyProgression(HeroSkillRanks draft, CharacterSpecialization specialization, bool matchingTrainerNearby = false)
        {
            if (!CanUseServices || Combat.IsActing(Hero.Id) || !Enum.IsDefined(typeof(CharacterSpecialization), specialization))
            {
                return Reject("skill changes require an idle, living hero");
            }

            bool switchSpec = specialization != Hero.Sheet.Specialization;
            int level = Hero.Sheet.Progression.Level;
            if (!Hero.SkillRanks.TryPriceChange(draft, level, Hero.ManaCubes, switchSpec, out long price))
            {
                return Reject("skill prerequisites or rank budget not met");
            }
            if (price > Hero.Copper)
            {
                return Reject($"need {price} copper for these skill changes");
            }
            if (price > 0 && !matchingTrainerNearby)
            {
                return Reject("paid skill changes require a nearby class trainer");
            }

            if (switchSpec)
            {
                Hero.ChangeSpecialization(specialization);
                RegisterHeroCombatant();
            }
            Hero.SkillRanks.TryReplace(draft.ToArray());
            Hero.Copper -= (int)price;
            LastRejection = string.Empty;
            return true;
        }

        public bool TryBuy(InventoryEntry offer)
        {
            if (!CanUseServices || offer == null || offer.Count != 1)
            {
                return Reject("buying requires a living hero and a single offered item");
            }

            int price = AlphaTradeValue.Of(offer);
            if (Hero.Copper < price)
            {
                return Reject($"need {price} copper");
            }
            if (!Hero.Inventory.TryAdd(offer))
            {
                return Reject(Hero.Inventory.LastRejection);
            }

            Hero.Copper -= price;
            LastRejection = string.Empty;
            return true;
        }

        public bool TrySell(int bagSlot)
        {
            InventoryEntry entry = CanUseServices ? Hero.Inventory[bagSlot] : null;
            if (entry == null)
            {
                return Reject("selling requires a living hero and an item in the bags");
            }

            int price = AlphaTradeValue.Of(entry);
            if (Hero.Copper > int.MaxValue - price)
            {
                return Reject("wallet is full");
            }

            Hero.Inventory.TakeFrom(bagSlot, 1);
            Hero.Copper += price;
            LastRejection = string.Empty;
            return true;
        }

        public bool TryRestAtInn()
        {
            if (!CanUseServices || !Clock.TryRestAtInn())
            {
                return Reject("the innkeeper offers rest from 18:00 to 06:00");
            }

            LastRejection = string.Empty;
            return true;
        }

        public bool TryBrewLifePotion()
        {
            if (!CanUseServices)
            {
                return Reject("crafting requires a living hero");
            }
            if (!Hero.Inventory.TryBrewLifePotion())
            {
                return Reject(Hero.Inventory.LastRejection);
            }

            LastRejection = string.Empty;
            return true;
        }

        private bool CanUseServices => State == SessionState.InGame && Hero != null && !Hero.Health.IsDead;

        /// <summary>
        /// Uses the item in a bag slot (M0-04B consumables): only Alpha consumables (type 1); refused while dead,
        /// acting or on cooldown. Starts Alpha action 80 (no hit), removes one item and applies the effect.
        /// </summary>
        public bool TryUseItem(int bagSlot)
        {
            if (Hero == null || Combat == null || State != SessionState.InGame)
            {
                return Reject("item use refused: no running world");
            }

            InventoryEntry entry = Hero.Inventory[bagSlot];
            if (entry == null)
            {
                return Reject($"slot {bagSlot} is empty");
            }

            if (entry.IsCore || entry.Raw.Type != 1)
            {
                return Reject($"{entry} is not a consumable");
            }

            if (Hero.Health.IsDead)
            {
                return Reject("the hero is dead");
            }

            if (_cooldownUntilMs.TryGetValue(entry.Raw, out long until) && Clock.TimeMs < until)
            {
                return Reject($"cooldown {until - Clock.TimeMs} ms");
            }

            if (!Combat.TryStartAction(Hero.Id, UseItemAction, Clock.TimeMs, "action_080", false))
            {
                return Reject(Combat.LastRejection);
            }

            Hero.Inventory.TakeFrom(bagSlot, 1);
            int cooldown = ConsumableCooldownMs?.Invoke(entry.Raw.Type, entry.Raw.Subtype) ?? 0;
            _cooldownUntilMs[entry.Raw] = Clock.TimeMs + cooldown;
            double healthBefore = Hero.Health.CurrentHealth;
            if (entry.Raw.Equals(LootRules.LifePotion))
            {
                Hero.Health.Heal(Hero.Health.MaxHealth * LifePotionHealShare);
            }

            LastRejection = string.Empty;
            Events.Publish(new HeroItemUsed
            {
                TimeMs = Clock.TimeMs, Raw = entry.Raw, HealthRestored = Hero.Health.CurrentHealth - healthBefore,
            });
            return true;
        }

        private void DropWorld()
        {
            if (Clock != null)
            {
                Clock.Ticked -= OnClockTicked;
            }

            Combat = null;
            WorldItems = null;
            World = null;
            Hero = null;
            Clock = null;
            WorldDeltas = null;
            SlotKey = null;
            _levelUpHealPending = false;
            Events.Clear();
        }

        private bool TryTransition(SessionState target)
        {
            if (!CanTransition(target))
            {
                return Reject($"transition {State} -> {target} is not allowed");
            }

            SessionState from = State;
            State = target;
            if (Clock != null)
            {
                Clock.Paused = target != SessionState.InGame;
            }

            LastRejection = string.Empty;
            Events.Publish(new SessionStateChanged
            {
                TimeMs = Clock?.TimeMs ?? 0,
                From = from.ToString(),
                To = target.ToString(),
            });
            return true;
        }

        private bool Reject(string reason)
        {
            LastRejection = reason;
            return false;
        }
    }
}
