using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    public sealed class FullM0ClockTests
    {
        private static List<long> RunTenSeconds(IEnumerable<double> frames)
        {
            var clock = new GameClock();
            var stamps = new List<long>();
            clock.Ticked += c => stamps.Add(c.TimeMs);
            foreach (double dt in frames)
            {
                clock.Advance(dt);
            }

            return stamps;
        }

        private static IEnumerable<double> Fixed(int hz, double seconds)
        {
            int n = (int)Math.Round(hz * seconds);
            for (int i = 0; i < n; i++)
            {
                yield return 1.0 / hz;
            }
        }

        [Test]
        public void SameTicksAndTimestampsAt30_60_120Hz()
        {
            List<long> at30 = RunTenSeconds(Fixed(30, 10.0));
            List<long> at60 = RunTenSeconds(Fixed(60, 10.0));
            List<long> at120 = RunTenSeconds(Fixed(120, 10.0));

            Assert.AreEqual(300, at30.Count);
            CollectionAssert.AreEqual(at30, at60);
            CollectionAssert.AreEqual(at30, at120);
            Assert.AreEqual(10000L, at30[at30.Count - 1]);
        }

        [Test]
        public void JitteredFramesProduceSameTicks()
        {
            var random = new Random(4242);
            var frames = new List<double>();
            double total = 0.0;
            while (total < 9.95)
            {
                double dt = 0.004 + random.NextDouble() * 0.03;
                frames.Add(dt);
                total += dt;
            }

            frames.Add(10.0 - total + 1e-7);
            List<long> jitter = RunTenSeconds(frames);
            CollectionAssert.AreEqual(RunTenSeconds(Fixed(30, 10.0)), jitter);
        }

        [Test]
        public void PauseStopsTimeAndHugeFrameIsClamped()
        {
            var clock = new GameClock();
            clock.Paused = true;
            Assert.AreEqual(0, clock.Advance(1.0));
            Assert.AreEqual(0L, clock.Tick);

            clock.Paused = false;
            Assert.AreEqual(GameClock.MaxTicksPerFrame, clock.Advance(5.0));
            Assert.Greater(clock.DroppedSeconds, 4.0);
            Assert.AreEqual(0, clock.Advance(double.NaN));
            Assert.AreEqual(0, clock.Advance(-1.0));
        }

        [Test]
        public void RestoreKeepsTimeline()
        {
            var clock = new GameClock(GameClock.DefaultTicksPerSecond, 900);
            Assert.AreEqual(30000L, clock.TimeMs);
            clock.StepTicks(3);
            Assert.AreEqual(903L, clock.Tick);
        }
    }

    public sealed class FullM0SessionTests
    {
        private static NewWorldRequest Request(uint seed = 7u) => new NewWorldRequest
        {
            WorldName = "Test",
            Seed = seed,
            GeneratorId = "test.gen/1",
            HeroName = "Ada",
            Class = CharacterClass.Mage,
            Spawn = new WorldPosition(1, 2, 3),
        };

        [Test]
        public void FullLifecycleMenuWorldPauseSaveExitQuit()
        {
            var session = new GameSession();
            var transitions = new List<string>();
            Assert.AreEqual(SessionState.Boot, session.State);
            Assert.IsTrue(session.EnterMainMenu());

            Assert.IsTrue(session.BeginNewWorld("slot0", Request(), 1L));
            session.Events.Subscribe<SessionStateChanged>(e => transitions.Add(e.From + ">" + e.To));
            Assert.AreEqual(SessionState.LoadingWorld, session.State);
            Assert.AreEqual(0, session.AdvanceFrame(1.0), "clock must not run while loading");

            Assert.IsTrue(session.CompleteLoading());
            int ticks = 0;
            for (int i = 0; i < 4; i++)
            {
                ticks += session.AdvanceFrame(0.25);
            }

            Assert.AreEqual(30, ticks, "one second of frames = 30 ticks");
            Assert.AreEqual(CharacterClass.Mage, session.Hero.Sheet.Class);
            Assert.AreEqual(7u, session.World.Seed.Value);

            Assert.IsTrue(session.Pause());
            Assert.AreEqual(0, session.AdvanceFrame(1.0), "pause stops the clock");
            Assert.IsTrue(session.BeginSave());
            Assert.IsTrue(session.EndSave());
            Assert.AreEqual(SessionState.Paused, session.State, "save returns to the state it started from");

            Assert.IsTrue(session.ExitToMenu());
            Assert.AreEqual(SessionState.MainMenu, session.State);
            Assert.IsNull(session.World);
            Assert.IsNull(session.Hero);
            Assert.IsNull(session.Clock);
            CollectionAssert.AreEqual(new[] { "LoadingWorld>InGame", "InGame>Paused", "Paused>Saving", "Saving>Paused", "Paused>Unloading" }, transitions);

            Assert.IsTrue(session.Quit());
            Assert.AreEqual(SessionState.Quit, session.State);
        }

        [Test]
        public void IllegalTransitionsAreRefusedWithReason()
        {
            var session = new GameSession();
            Assert.IsFalse(session.BeginNewWorld("slot0", Request(), 0L), "no new world before main menu");
            StringAssert.Contains("Boot", session.LastRejection);

            session.EnterMainMenu();
            Assert.IsFalse(session.Pause());
            Assert.IsFalse(session.CompleteLoading());
            Assert.IsFalse(session.ExitToMenu());
            Assert.IsFalse(session.BeginNewWorld(string.Empty, Request(), 0L));
            Assert.AreEqual(SessionState.MainMenu, session.State);

            session.BeginNewWorld("slot0", Request(), 0L);
            Assert.IsFalse(session.BeginNewWorld("slot1", Request(), 0L), "second world while loading");
            Assert.IsTrue(session.AbortLoading("build failed"));
            Assert.AreEqual(SessionState.MainMenu, session.State);
            Assert.IsNull(session.World);
        }

        [Test]
        public void RepeatedWorldsDoNotLeakSubscribersOrState()
        {
            var session = new GameSession();
            session.EnterMainMenu();
            int calls = 0;
            for (int i = 0; i < 20; i++)
            {
                Assert.IsTrue(session.BeginNewWorld("slot0", Request((uint)i), i));
                Assert.IsTrue(session.CompleteLoading());
                session.Events.Subscribe<EntityDied>(_ => calls++);
                session.Events.Publish(new EntityDied());
                Assert.AreEqual((uint)i, session.World.Seed.Value);
                Assert.AreEqual(0L, session.Clock.Tick);
                Assert.IsTrue(session.ExitToMenu());
            }

            Assert.AreEqual(20, calls, "each world sees only its own subscriber");
        }

        [Test]
        public void EventBusIsolatesFaultyHandlersAndFeedsVisualSink()
        {
            var bus = new GameEventBus();
            var sink = new RecordingSink();
            bus.AddVisualSink(sink);
            int good = 0;
            bus.Subscribe<ActionStarted>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<ActionStarted>(_ => good++);

            bus.Publish(new ActionStarted { Action = new ActionId(57), PhaseDurationsMs = new long[] { 300, 100, 400 } });
            bus.Publish(new HitResolved { Damage = 12.5 });

            Assert.AreEqual(1, good);
            Assert.AreEqual(1, bus.HandlerErrors.Count);
            CollectionAssert.AreEqual(new[] { "action:57", "hit:12.5" }, sink.Log);
        }

        [Test]
        public void StableEntityIdsAreDeterministicAndDistinct()
        {
            var region = new RegionKey(-3, 17);
            StableEntityId a = StableEntityId.ForSpawn(region, 5, 0);
            Assert.AreEqual(a, StableEntityId.ForSpawn(region, 5, 0));
            Assert.AreNotEqual(a, StableEntityId.ForSpawn(region, 6, 0));
            Assert.AreNotEqual(a, StableEntityId.ForSpawn(region, 5, 1));
            Assert.AreNotEqual(a, StableEntityId.ForSpawn(new RegionKey(-3, 18), 5, 0));
            Assert.AreEqual(EntityIdKind.WorldSpawn, a.Kind);
            Assert.AreEqual(EntityIdKind.Hero, StableEntityId.Hero.Kind);
            Assert.Throws<ArgumentOutOfRangeException>(() => StableEntityId.ForSpawn(region, 4096, 0));
        }

        private sealed class RecordingSink : IEntityVisualSink
        {
            public List<string> Log { get; } = new List<string>();

            public void OnEntitySpawned(EntitySpawned e) => Log.Add("spawn");

            public void OnEntityDespawned(EntityDespawned e) => Log.Add("despawn");

            public void OnEntityMoved(EntityMoved e) => Log.Add("move");

            public void OnEquipmentChanged(EquipmentVisualChanged e) => Log.Add("equip");

            public void OnActionStarted(ActionStarted e) => Log.Add(e.Action.ToString());

            public void OnActionPhaseChanged(ActionPhaseChanged e) => Log.Add("phase");

            public void OnActionResolved(ActionResolved e) => Log.Add("resolved");

            public void OnHitResolved(HitResolved e) => Log.Add("hit:" + e.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture));

            public void OnEntityDied(EntityDied e) => Log.Add("died");

            public void OnFrame(long gameTimeMs, double interpolationAlpha)
            {
            }
        }
    }
}
