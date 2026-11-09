using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
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
        }

        public StableEntityId Id => StableEntityId.Hero;

        public string Name { get; }

        /// <summary>Race id (M0-03A; -1 until the race table is integrated).</summary>
        public int RaceId { get; }

        public CharacterSheet Sheet { get; }

        public List<ItemInstance> Items { get; } = new List<ItemInstance>();

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

        public GameSession()
        {
            Events = new GameEventBus();
        }

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
            if (!TryTransition(SessionState.LoadingWorld))
            {
                DropWorld();
                return false;
            }

            return true;
        }

        private void DropWorld()
        {
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
