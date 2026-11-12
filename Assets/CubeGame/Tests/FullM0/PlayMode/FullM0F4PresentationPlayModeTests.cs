using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
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
using CubeGame.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using EquipmentSlot = CubeGame.Core.Equipment.EquipmentSlot;
using Object = UnityEngine.Object;

namespace CubeGame.Tests.FullM0.PlayMode
{
    public sealed class FullM0F4PresentationPlayModeTests
    {
        private GameObject _host;
        private string _directory;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_host != null)
            {
                var game = _host.GetComponent<GameBootstrap>();
                if (game != null && (game.Session.State == SessionState.InGame || game.Session.State == SessionState.Paused))
                {
                    game.ExitToMenu(false);
                }
                Object.Destroy(_host);
            }
            foreach (string name in new[] { "Main Camera", "Sun" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null) Object.Destroy(leftover);
            }
            yield return null;
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [TestCase(false, 1, 48000)]
        [TestCase(true, 2, 44100)]
        public void RiffDecodesPaddedChunksAndRejectsInvalidLengths(bool floating, int channels, int rate)
        {
            byte[] bytes = Wave(floating, channels, rate);
            AudioClip clip = AlphaWave.Decode(bytes, "synthetic");
            try
            {
                Assert.AreEqual(channels, clip.channels);
                Assert.AreEqual(rate, clip.frequency);
                Assert.AreEqual(2048, clip.samples);
                var samples = new float[clip.samples * channels];
                Assert.IsTrue(clip.GetData(samples, 0));
                Assert.That(samples[0], Is.EqualTo(-0.5f).Within(0.001f));
                Assert.That(samples[1], Is.EqualTo(0.25f).Within(0.001f));
                Assert.Throws<InvalidDataException>(() => AlphaWave.Decode(bytes.Take(bytes.Length - 1).ToArray(), "truncated"));
                // A RIFF size can match the file while the internal chunk length still overflows it.
                bytes[16] = 255;
                Assert.Throws<InvalidDataException>(() => AlphaWave.Decode(bytes, "truncated chunk"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [UnityTest]
        public IEnumerator NewWorldToSaveLoadThroughThePresentation()
        {
            _directory = Path.Combine(Path.GetTempPath(), "cubegame_f4_" + Guid.NewGuid().ToString("N"));
            string sounds = Path.Combine(_directory, AlphaSoundLibrary.DirectoryName);
            Directory.CreateDirectory(sounds);
            string localCorpus = Environment.GetEnvironmentVariable("CUBEGAME_F4_VERIFY_AUDIO");
            if (!string.IsNullOrEmpty(localCorpus))
            {
                foreach (string name in AlphaSoundCatalog.Names)
                {
                    AudioClip decoded = AlphaWave.Decode(File.ReadAllBytes(Path.Combine(localCorpus, name)), name);
                    Assert.Greater(decoded.samples, 0, name);
                    Object.DestroyImmediate(decoded);
                }
                Debug.Log($"[F4] Decoded all {AlphaSoundCatalog.Names.Length} staged Alpha WAVs.");
            }

            // Original files are optional for CI. The ordinary runtime path receives both supported encodings.
            string[] cues =
            {
                AlphaSoundCatalog.MissionComplete, "hit.wav", "drink2.wav", "pickup.wav",
                "menu-open2.wav", "menu-close2.wav", "levelup2.wav", "craft.wav",
            };
            for (int i = 0; string.IsNullOrEmpty(localCorpus) && i < cues.Length; i++)
            {
                File.WriteAllBytes(Path.Combine(sounds, cues[i]), Wave(i % 2 == 0, 2, 44100));
            }

            _host = new GameObject("F4 GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = Path.Combine(_directory, "saves");
            game.SoundContentRoot = string.IsNullOrEmpty(localCorpus) ? _directory : Directory.GetParent(localCorpus).FullName;
            yield return null;
            yield return null;
            Press(game, "start-game");
            yield return null;
            Assert.IsNull(Root(game).Q("world-name"));
            Press(game, "new-character");
            yield return null;
            Press(game, "menu-back");
            yield return null;
            Assert.IsNull(Root(game).Q("hero-name"));
            Press(game, "new-character");
            yield return null;
            Root(game).Q<TextField>("hero-name").value = "Brena";
            Press(game, "create-character");
            yield return null;
            Press(game, "new-world");
            yield return null;
            Root(game).Q<TextField>("world-name").value = "F4 world";
            // Alpha has no default seed (0x482530 parses the field as typed); the fixture keeps its known world.
            Root(game).Q<TextField>("world-seed").value = "666666";
            yield return Capture(game, "menu");
            Press(game, "create-world");
            yield return null;
            for (int i = 0; i < 300 && game.Session.State != SessionState.InGame; i++) yield return null;
            yield return null;
            Assert.AreEqual(SessionState.InGame, game.Session.State, game.Status);
            Assert.AreEqual("Brena", game.Session.Hero.Name);
            Assert.AreEqual("F4 world", game.Session.World.Name);
            HeroState hero = game.Session.Hero;
            AssertHud(game);
            yield return Capture(game, "hud");
            Assert.AreEqual(1, game.GetComponentsInChildren<UIDocument>().Length);

            // Seed fixture, same generation and hit/loot runtime as the accepted F3 scenario.
            game.TeleportHero(8397145, 8396899);
            yield return null;
            game.Session.Clock.StepTicks(2);
            hero.Sheet.AddExperience(AlphaMath.ExperienceCostForNextLevel(1) - 1);
            Combatant target = game.Session.Combat.Combatants.Where(c => c.Hostile && c.Id.Kind == EntityIdKind.ZoneSpawn)
                .OrderBy(c => DistanceSquared(c.Position, hero.Position)).First();
            PlaceHero(game, target.Position);
            yield return null;
            game.Session.Clock.StepTicks(1);
            HuntMission hunt = hero.Missions.Hunts.Single(m => !m.Complete);
            Assert.IsTrue(game.Session.Combat.TryGet(hunt.Target, out target));
            Press(game, "open-journal");
            yield return null;
            Assert.IsTrue(Root(game).Q<Label>($"hunt-{hunt.Target.Value}").ClassListContains("hunt-active"));
            Press(game, "close-panel");
            yield return null;

            var itemsBefore = hero.Inventory.AllCoreItems().ToArray();
            PlaceHero(game, target.Position);
            target.Health.ApplyDamage(target.Health.CurrentHealth - 0.01);
            for (int i = 0; i < 5 && !target.IsDead; i++)
            {
                PlaceHero(game, target.Position);
                Assert.IsTrue(game.HeroAttack(), game.CombatLog);
                game.Session.Clock.StepTicks(game.Session.Clock.TicksPerSecond * 2);
            }
            Assert.IsTrue(target.IsDead);
            Assert.AreEqual(2, hero.Sheet.Progression.Level);
            AudioClip mission = Clip(AlphaSoundCatalog.MissionComplete);
            Assert.IsNotNull(mission, "a live hunt transition loaded and played its staged cue");
            Assert.IsNotNull(Clip("hit.wav"));
            Assert.IsNotNull(Clip("levelup2.wav"));
            PlaceHero(game, target.Position);
            yield return null;
            game.Session.Clock.StepTicks(1);
            int loot = Enumerable.Range(0, HeroInventory.Capacity)
                .First(i => hero.Inventory[i]?.IsCore == true && !itemsBefore.Contains(hero.Inventory[i].Core));
            Assert.IsNotNull(Clip("pickup.wav"));
            Assert.AreEqual(game.Session.WorldItems.Count, game.Visuals.GroundItemCount);

            Press(game, "open-inventory");
            yield return null;
            yield return Capture(game, "inventory");
            Assert.IsNotNull(Root(game).Q($"bag-slot-{loot}"));
            var weapon = hero.Loadout.GetEquipped(EquipmentSlot.MainHand);
            Press(game, "unequip-MainHand");
            yield return null;
            Assert.IsNull(hero.Loadout.GetEquipped(EquipmentSlot.MainHand));
            int weaponSlot = Enumerable.Range(0, HeroInventory.Capacity).First(i => ReferenceEquals(weapon, hero.Inventory[i]?.Core));
            Press(game, $"equip-{weaponSlot}");
            yield return null;
            Assert.AreSame(weapon, hero.Loadout.GetEquipped(EquipmentSlot.MainHand));

            Press(game, "open-skills");
            yield return null;
            Press(game, "raise-Climbing");
            yield return null;
            Assert.AreEqual(0, hero.SkillRanks[HeroSkill.Climbing], "draft never mutates live ranks");
            Press(game, "apply-skills");
            yield return null;
            Assert.AreEqual(1, hero.SkillRanks[HeroSkill.Climbing]);
            Press(game, "close-panel");
            yield return null;

            ZoneKey city = FindCity(game);
            game.TeleportHero(city.X * WorldLayout.ZoneSize + 128, city.Z * WorldLayout.ZoneSize + 128);
            yield return null;
            LiveSpawn merchant = TownNpc(game, city, TownNpcPopulation.MerchantClass);
            PlaceHero(game, merchant.Definition.Position);
            yield return null;
            yield return null;
            Assert.IsTrue(Root(game).Q<Button>("interact").enabledInHierarchy,
                $"Hero at {hero.Position}, vendor at {merchant.Definition.Position}");
            Press(game, "interact");
            yield return null;
            Assert.IsNotNull(Root(game).Q<Label>("vendor-wallet"));
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CUBEGAME_F4_SCREENSHOTS")))
            {
                Assert.AreEqual("alpha_icon_heartflower", Root(game).Q<Button>("buy-1").parent.Q<Image>()?.sprite?.name);
                Assert.AreEqual("alpha_icon_water-flask", Root(game).Q<Button>("buy-2").parent.Q<Image>()?.sprite?.name);
            }
            yield return Capture(game, "services");
            int potions = hero.Inventory.CountOf(LootRules.LifePotion);
            Press(game, "buy-0");
            yield return null;
            Assert.AreEqual(0, hero.Copper);
            Assert.AreEqual(potions, hero.Inventory.CountOf(LootRules.LifePotion));
            StringAssert.Contains("copper", Root(game).Q<Label>("panel-status").text);
            int proceeds = AlphaTradeValue.Of(hero.Inventory[loot]);
            Press(game, $"sell-{loot}");
            yield return null;
            Assert.AreEqual(proceeds, hero.Copper);
            Press(game, "buy-1");
            yield return null;
            Press(game, "buy-2");
            yield return null;
            Assert.AreEqual(1, hero.Inventory.CountOf(AlphaLifePotionRecipe.Heartflower));
            Assert.AreEqual(1, hero.Inventory.CountOf(AlphaLifePotionRecipe.WaterFlask));

            Press(game, "open-inventory");
            yield return null;
            Press(game, "brew");
            yield return null;
            Assert.AreEqual(potions + 1, hero.Inventory.CountOf(LootRules.LifePotion));
            Assert.AreEqual(0, hero.Inventory.CountOf(AlphaLifePotionRecipe.Heartflower));
            Assert.IsNotNull(Clip("craft.wav"));
            hero.Health.ApplyDamage(20);
            double injured = hero.Health.CurrentHealth;
            Press(game, $"use-{hero.Inventory.FindFirst(LootRules.LifePotion)}");
            yield return null;
            Assert.Greater(hero.Health.CurrentHealth, injured);
            Assert.IsNotNull(Clip("drink2.wav"));
            StringAssert.Contains("Restored", Root(game).Q<Label>("game-feedback").text);
            game.Session.Clock.StepTicks(game.Session.Clock.TicksPerSecond * 3);
            Press(game, "close-panel");
            yield return null;

            LiveSpawn trainer = TownNpc(game, city, (int)hero.Sheet.Class + 1);
            PlaceHero(game, trainer.Definition.Position);
            yield return null;
            yield return null;
            Press(game, "interact");
            yield return null;
            Press(game, "trainer-skills");
            yield return null;
            int trainingWallet = hero.Copper;
            Press(game, "lower-Climbing");
            yield return null;
            Assert.AreEqual(1, hero.SkillRanks[HeroSkill.Climbing]);
            Press(game, "apply-skills");
            yield return null;
            Assert.AreEqual(0, hero.SkillRanks[HeroSkill.Climbing]);
            Assert.Less(hero.Copper, trainingWallet, "the paid reset used the existing trainer transaction");
            Press(game, "raise-Climbing");
            yield return null;
            Press(game, "apply-skills");
            yield return null;
            Assert.AreEqual(1, hero.SkillRanks[HeroSkill.Climbing]);
            Press(game, "close-panel");
            yield return null;

            LiveSpawn innkeeper = TownNpc(game, city, TownNpcPopulation.InnkeeperClass);
            PlaceHero(game, innkeeper.Definition.Position);
            yield return null;
            yield return null;
            Press(game, "interact");
            yield return null;
            Press(game, "rest");
            yield return null;
            StringAssert.Contains("18:00", Root(game).Q<Label>("panel-status").text);
            GameClock clock = game.Session.Clock;
            clock.Restore(9L * 360 * clock.TicksPerSecond);
            int day = clock.WorldDay;
            Press(game, "rest");
            Assert.AreEqual(day + 1, clock.WorldDay);
            Assert.AreEqual(GameClock.InnDawnMs, clock.WorldTimeMs);
            yield return null;

            Press(game, "open-journal");
            yield return null;
            Assert.IsTrue(Root(game).Q<Label>($"hunt-{hunt.Target.Value}").ClassListContains("hunt-complete"));
            yield return Capture(game, "journal");
            Press(game, "open-map");
            yield return null;
            Image map = Root(game).Q<Image>("map-image");
            Assert.IsNotNull(map?.image);
            Assert.IsNotNull(Root(game).Q("map-hero"));
            Assert.IsNotNull(Root(game).Q($"map-zone-{city.X}-{city.Z}"));
            yield return Capture(game, "map");
            Texture mapTexture = map.image;
            int wallet = hero.Copper;
            int bagSlots = hero.Inventory.UsedSlots;
            long calendarOffset = clock.WorldTimeOffsetMs;

            Press(game, "system-menu");
            yield return null;
            Press(game, "system-start-menu");
            yield return null;
            yield return null;
            Assert.AreEqual(SessionState.MainMenu, game.Session.State, game.Status);
            Assert.IsNull(game.Session.Hero);
            Assert.IsNull(Root(game).Q("hero-hud"));
            Assert.IsTrue(mapTexture == null);
            Assert.IsTrue(mission == null);
            Assert.IsTrue(game.GetComponentsInChildren<AudioSource>().All(voice => voice.clip == null));
            Assert.AreEqual(0, CountNamed("Player"));

            Press(game, "start-game");
            yield return null;
            Press(game, "character-0");
            yield return null;
            Press(game, "world-0");
            yield return null;
            for (int i = 0; i < 300 && game.Session.State != SessionState.InGame; i++) yield return null;
            yield return null;
            hero = game.Session.Hero;
            Assert.AreEqual(SessionState.InGame, game.Session.State, game.Status);
            AssertHud(game);
            Assert.AreEqual(1, game.GetComponentsInChildren<UIDocument>().Length);
            Assert.AreEqual(1, CountNamed("Player"));
            Assert.AreEqual(1, CountNamed("WorldRoot"));
            Assert.AreEqual(wallet, hero.Copper);
            Assert.AreEqual(bagSlots, hero.Inventory.UsedSlots);
            Assert.AreEqual(calendarOffset, game.Session.Clock.WorldTimeOffsetMs);
            Assert.AreEqual(1, hero.SkillRanks[HeroSkill.Climbing]);
            Assert.AreEqual(1, hero.ManaCubes);
            Assert.AreEqual(2, hero.Sheet.Progression.Level);
            Assert.AreEqual(weapon, hero.Loadout.GetEquipped(EquipmentSlot.MainHand));
            Assert.IsNull(Clip(AlphaSoundCatalog.MissionComplete), "reloading a completed hunt does not play its cue");
            Assert.IsNull(Root(game).Q("game-panel"));

            Press(game, "open-journal");
            yield return null;
            Assert.IsTrue(Root(game).Q<Label>($"hunt-{hunt.Target.Value}").ClassListContains("hunt-complete"));
            Assert.IsNull(Clip(AlphaSoundCatalog.MissionComplete));
            Press(game, "close-panel");
            yield return null;
            LiveSpawn loadedMerchant = TownNpc(game, city, TownNpcPopulation.MerchantClass);
            Assert.AreEqual(merchant.Id, loadedMerchant.Id);
            PlaceHero(game, loadedMerchant.Definition.Position);
            yield return null;
            yield return null;
            Press(game, "interact");
            yield return null;
            Press(game, $"sell-{hero.Inventory.FindFirst(LootRules.LifePotion)}");
            yield return null;
            Assert.AreEqual(wallet + 2, hero.Copper);
            Press(game, "close-panel");
            yield return null;
            Combatant attacker = game.SpawnTrainingTarget();
            hero.Health.ApplyDamage(hero.Health.CurrentHealth - 0.01);
            for (int i = 0; i < 30 && !game.HeroDead; i++)
            {
                game.Session.Combat.ResolveHit(attacker.Id, hero.Id, game.Session.Clock.TimeMs, i);
            }
            Assert.IsTrue(game.HeroDead);
            yield return null;
            StringAssert.Contains("Downed", Root(game).Q<Label>("hero-resources").text);
            StringAssert.Contains("You fell", Root(game).Q<Label>("game-feedback").text);
            Assert.That(game.GetComponentsInChildren<AudioSource>().Length, Is.LessThanOrEqualTo(8));
            Assert.IsEmpty(game.Session.Events.HandlerErrors);
        }

        private static VisualElement Root(GameBootstrap game) => game.GetComponentInChildren<UIDocument>().rootVisualElement;

        private static IEnumerator Capture(GameBootstrap game, string name)
        {
            string directory = Environment.GetEnvironmentVariable("CUBEGAME_F4_SCREENSHOTS");
            if (string.IsNullOrEmpty(directory)) yield break;
            Directory.CreateDirectory(directory);
            var settings = game.GetComponentInChildren<UIDocument>().panelSettings;
            var target = RenderTexture.GetTemporary(1280, 720);
            var previous = RenderTexture.active;
            var panelTarget = settings.targetTexture;
            Texture2D frame = null;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, new Color(0.25f, 0.39f, 0.52f));
                RenderTexture.active = previous;
                settings.targetTexture = target;
                yield return null;
                yield return null;
                RenderTexture.active = target;
                frame = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), frame.EncodeToPNG());
            }
            finally
            {
                settings.targetTexture = panelTarget;
                RenderTexture.active = previous;
                if (frame != null) Object.Destroy(frame);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void Press(GameBootstrap game, string name)
        {
            var button = Root(game).Q<Button>(name);
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.enabledInHierarchy, name);
            // UI Toolkit dispatches navigation events from the focused element; focusing the target first keeps the
            // press on the intended button instead of whichever element focused earlier.
            button.Focus();
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        private static void AssertHud(GameBootstrap game)
        {
            var hero = game.Session.Hero;
            Assert.AreEqual(hero.Health.CurrentHealth, Root(game).Q<ProgressBar>("hero-health").value, 0.01);
            Assert.AreEqual(hero.Sheet.Progression.Experience, Root(game).Q<ProgressBar>("hero-experience").value, 0.01);
            StringAssert.Contains($"Level {hero.Sheet.Progression.Level}", Root(game).Q<Label>("hero-summary").text);
            StringAssert.Contains($"{hero.Copper} copper", Root(game).Q<Label>("hero-resources").text);
        }

        private static AudioClip Clip(string name) =>
            Resources.FindObjectsOfTypeAll<AudioClip>().SingleOrDefault(clip => clip.name == name);

        private static int CountNamed(string name) =>
            Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Count(go => go.name == name);

        private static LiveSpawn TownNpc(GameBootstrap game, ZoneKey city, int role) => game.Population.Live.Single(s =>
            s.Zone.Equals(city) && s.Definition.BehaviourKey == TownNpcPopulation.BehaviourKey && s.Definition.ClassId == role);

        private static ZoneKey FindCity(GameBootstrap game)
        {
            const int index = WorldLayout.CenterRegion;
            AlphaRegion region = ((AlphaTerrainGenerator)game.WorldState.Generator).RegionRecord(index, index);
            int slot = Array.FindIndex(region.Zones, r => r.Type == AlphaZoneRecord.Town);
            Assert.GreaterOrEqual(slot, 0);
            return new ZoneKey(index * 64 + slot / 64, index * 64 + slot % 64);
        }

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

        private static byte[] Wave(bool floating, int channels, int rate)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream, Encoding.ASCII, true);
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(0);
            w.Write(Encoding.ASCII.GetBytes("WAVEJUNK"));
            w.Write(1);
            w.Write((ushort)0x34); // one byte plus RIFF padding
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((ushort)(floating ? 3 : 1));
            w.Write((ushort)channels);
            w.Write(rate);
            int sampleBytes = floating ? 4 : 2;
            w.Write(rate * channels * sampleBytes);
            w.Write((ushort)(channels * sampleBytes));
            w.Write((ushort)(sampleBytes * 8));
            w.Write(Encoding.ASCII.GetBytes("data"));
            int count = 2048 * channels;
            w.Write(count * sampleBytes);
            for (int i = 0; i < count; i++)
            {
                if (floating) w.Write(i % 2 == 0 ? -0.5f : 0.25f);
                else w.Write((short)(i % 2 == 0 ? -16384 : 8192));
            }
            w.Seek(4, SeekOrigin.Begin);
            w.Write((int)stream.Length - 8);
            return stream.ToArray();
        }
    }
}
