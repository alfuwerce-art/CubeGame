using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
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
            RaceId = raceId;
            Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            Loadout = new EquipmentLoadout();
            Health = new HealthPool(EquipmentCombatAdapter.ComputeMaxHealth(sheet));
        }

        /// <summary>
        /// Starting weapon of a new hero: one level-1 common greatsword (Core S04 item), matching the S05 player
        /// visual (human with greatsword, action 57). PLACEHOLDER_NOT_ALPHA: Alpha's class start kit is not restored.
        /// </summary>
        public static ItemInstance CreateStartingWeapon()
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Greatsword, 1, 0, 1);
        }

        public StableEntityId Id => StableEntityId.Hero;

        public string Name { get; }

        /// <summary>Race id (M0-03A; -1 until the race table is integrated).</summary>
        public int RaceId { get; }

        public CharacterSheet Sheet { get; }

        public List<ItemInstance> Items { get; } = new List<ItemInstance>();

        /// <summary>Equipped items (Core S04). Not stored by save v1 (SAVE-EQUIPMENT-GAP): rebuilt by <see cref="EquipFromItems"/>.</summary>
        public EquipmentLoadout Loadout { get; }

        /// <summary>Current HP; maximum from Core <see cref="EquipmentCombatAdapter.ComputeMaxHealth"/>.</summary>
        public HealthPool Health { get; private set; }

        /// <summary>Replaces a dead hero's HP pool with a full one (Core HealthPool never revives by design).</summary>
        public void Revive()
        {
            Health = new HealthPool(EquipmentCombatAdapter.ComputeMaxHealth(Sheet));
        }

        /// <summary>
        /// Rebuilds the loadout from the carried items: the first weapon goes to the main hand. CubeGame rule
        /// (PLACEHOLDER_NOT_ALPHA) until equipment slots are part of the save (M0-04D, decision on save v2).
        /// </summary>
        public void EquipFromItems()
        {
            foreach (ItemInstance item in Items)
            {
                if (item.Type == ItemType.Weapon && Loadout.IsSlotEmpty(EquipmentSlot.MainHand))
                {
                    Loadout.Equip(EquipmentSlot.MainHand, item);
                }
            }
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

        public int RaceId { get; set; } = -1;

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

        /// <summary>Attack phase source for the combat system (Alpha clip timings in the game, fixed table in tests).</summary>
        public IActionTimingSource ActionTimings { get; set; }

        /// <summary>Combat of the running world (M0-05A first slice); created on LoadingWorld, advanced by clock ticks.</summary>
        public CombatSystem Combat { get; private set; }

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

            var sheet = new CharacterSheet(request.Class, request.Specialization);
            var hero = new HeroState(request.HeroName, request.RaceId, sheet)
            {
                Position = request.Spawn,
                NeedsGroundPlacement = true,
            };
            hero.Items.Add(HeroState.CreateStartingWeapon());
            hero.EquipFromItems();

            var world = new WorldInfo(request.WorldName, new WorldSeed(request.Seed), request.GeneratorId, nowUtcTicks);
            return Attach(slotKey, world, hero, new GameClock(), new WorldDeltaStore());
        }

        /// <summary>MainMenu → LoadingWorld with state restored from a save slot.</summary>
        public bool BeginLoadedWorld(string slotKey, WorldInfo world, HeroState hero, long clockTick, WorldDeltaStore deltas = null)
        {
            if (world == null || hero == null)
            {
                return Reject("loaded world or hero is missing");
            }

            if (!CanTransition(SessionState.LoadingWorld))
            {
                return Reject($"cannot load a world from {State}");
            }

            return Attach(slotKey, world, hero, new GameClock(GameClock.DefaultTicksPerSecond, clockTick), deltas ?? new WorldDeltaStore());
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
            Combat.Register(new Combatant(hero.Id, HeroKind, hero.Name, hero.Sheet, hero.Loadout, hero.Health)
            {
                Position = hero.Position,
                Yaw = hero.Yaw,
            });
            Clock.Ticked += OnClockTicked;
            if (!TryTransition(SessionState.LoadingWorld))
            {
                DropWorld();
                return false;
            }

            return true;
        }

        /// <summary>Species of the hero for visuals and action timings: Alpha HumanMale (0x02), until race selection (M0-03A).</summary>
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
            Combat.Register(new Combatant(Hero.Id, HeroKind, Hero.Name, Hero.Sheet, Hero.Loadout, Hero.Health)
            {
                Position = Hero.Position,
                Yaw = Hero.Yaw,
            });
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

            Combat.Update(clock.TimeMs);
        }

        private void DropWorld()
        {
            if (Clock != null)
            {
                Clock.Ticked -= OnClockTicked;
            }

            Combat = null;
            World = null;
            Hero = null;
            Clock = null;
            WorldDeltas = null;
            SlotKey = null;
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
