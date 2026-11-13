using System;
using System.Collections;
using System.IO;
using CubeGame.Core.Characters;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.World;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// M0 P0 acceptance scenario over the Alpha start screens:
    /// boot → Start Game → new character → new world → game → move → Esc system menu → Start Menu (save) →
    /// Start Game → same character → same world → the saved position comes back.
    /// Input contexts are asserted at each step: a start screen or overlay owns the frame, gameplay never does.
    /// </summary>
    public sealed class FullM0P0StartupFlowPlayModeTests
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

        [UnityTest]
        public IEnumerator AlphaStartupFlowSavesAndRestoresThePerWorldPosition()
        {
            _directory = Path.Combine(Path.GetTempPath(), "cubegame-p0-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _host = new GameObject("P0 GameBootstrap");
            var game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = Path.Combine(_directory, "saves");
            game.SoundContentRoot = _directory;
            yield return null;
            yield return null;

            // BOOT: the start screen, not the editor and not a world.
            Assert.AreEqual(SessionState.MainMenu, game.Session.State);
            Assert.IsNotNull(Root(game).Q<Button>("start-game"), "Start Game");
            Assert.IsNotNull(Root(game).Q<Button>("menu-options"), "Options");
            Assert.IsNotNull(Root(game).Q<Button>("menu-exit"), "Exit");
            Assert.IsNull(Root(game).Q("character-0"));
            Assert.IsTrue(game.GuiActive, "the start menu owns the input context");

            // MAIN MENU → CHARACTER LIST
            Press(game, "start-game");
            yield return null;
            Assert.IsNull(Root(game).Q("start-game"));
            Assert.IsNotNull(Root(game).Q<Button>("new-character"));

            // CHARACTER LIST → EDITOR (new character)
            Press(game, "new-character");
            yield return null;
            Assert.IsNotNull(Root(game).Q<TextField>("hero-name"));
            Assert.IsNotNull(Root(game).Q("character-preview"), "Alpha shows the character model in the editor");
            Root(game).Q<TextField>("hero-name").value = "P0 Hero";
            // The editor arrows are the same state machine Alpha cycles; the Windows Player run clicks them with the
            // real mouse (see the P0 report), while PlayMode drives the public step API.
            game.CycleEditorClass(1);
            yield return null;
            Assert.AreEqual("Ranger", Root(game).Q<Label>("hero-class").text);
            game.CycleEditorRace(1);
            yield return null;
            Assert.AreEqual("Elf", Root(game).Q<Label>("hero-race").text);

            // EDITOR confirm → WORLD LIST
            Press(game, "create-character");
            yield return null;
            Assert.AreEqual(1, game.Profiles.Characters.Count, "the character is in characters.db before a world exists");
            Assert.IsNotNull(Root(game).Q<Button>("new-world"));
            Assert.IsNull(Root(game).Q<Button>("world-0"), "no world exists yet");

            // WORLD LIST → CREATE WORLD (no uint range rule: Alpha parses an integer field)
            Press(game, "new-world");
            yield return null;
            Root(game).Q<TextField>("world-name").value = "P0 World";
            Root(game).Q<TextField>("world-seed").value = "666666";
            Press(game, "create-world");
            yield return null;
            Assert.IsNotNull(Root(game).Q("wait-screen"), "the Alpha wait screen is shown while the world opens");
            yield return WaitForWorld(game);

            Assert.AreEqual(SessionState.InGame, game.Session.State, game.Status);
            Assert.AreEqual("P0 World", game.Session.World.Name);
            Assert.AreEqual(666666u, game.Session.World.Seed.Value);
            Assert.AreEqual("P0 Hero", game.Session.Hero.Name);
            Assert.AreEqual(CharacterClass.Ranger, game.Session.Hero.Sheet.Class);
            Assert.AreEqual(1, game.Session.Hero.Appearance.Race, "Elf is race index 1");
            Assert.AreEqual(1, game.Profiles.Worlds.Count);
            Assert.IsFalse(game.GuiActive, "gameplay owns the input context");
            Assert.IsTrue(game.Session.Hero.NeedsGroundPlacement == false);
            Assert.IsNotNull(Root(game).Q("hero-hud"));

            // MOVE, then leave through the Alpha system menu (Esc → Start Menu saves character + world).
            game.TeleportHero(WorldLayout.StartVoxel + 40, WorldLayout.StartVoxel + 25);
            yield return null;
            yield return null;
            HeroState hero = game.Session.Hero;
            WorldPosition saved = hero.Position;
            var start = CubeGame.Features.World.WorldState.StartPosition(game.WorldState.Generator, game.Session.WorldDeltas);
            Assert.Greater(Mathf.Abs((float)(saved.X - start.X)) + Mathf.Abs((float)(saved.Z - start.Z)), 8f,
                "the hero left the world start so the saved position is distinguishable");

            PressMenu(game, "system-menu");
            yield return null;
            Assert.IsTrue(game.GuiActive, "the system menu owns the input context");
            PlayerMotor motor = game.PlayerTransform.GetComponent<PlayerMotor>();
            Assert.IsFalse(motor.enabled, "no movement while the system menu is open");
            Press(game, "system-start-menu");
            yield return null;
            yield return null;

            Assert.AreEqual(SessionState.MainMenu, game.Session.State, game.Status);
            Assert.IsNull(game.Session.Hero);
            Assert.IsNotNull(Root(game).Q<Button>("start-game"), "Start Menu returns to the start screen");
            Assert.AreEqual(1, game.Profiles.Characters.Count);
            Assert.AreEqual(1, game.Profiles.Worlds.Count);
            AlphaWorldVisit visit = game.Profiles.Characters[0].FindVisit(game.Profiles.Worlds[0]);
            Assert.IsNotNull(visit, "the character keeps its position for that world");
            Assert.AreEqual(saved.X, visit.Position.X, 0.75, "position by world");

            // CONTINUE: same character, same world.
            Press(game, "start-game");
            yield return null;
            Press(game, "character-0");
            yield return null;
            Assert.IsNotNull(Root(game).Q<Button>("world-0"));
            Press(game, "world-0");
            yield return null;
            yield return WaitForWorld(game);

            Assert.AreEqual(SessionState.InGame, game.Session.State, game.Status);
            Assert.AreEqual("P0 World", game.Session.World.Name);
            Assert.AreEqual("P0 Hero", game.Session.Hero.Name);
            WorldPosition restored = game.Session.Hero.Position;
            Assert.AreEqual(saved.X, restored.X, 0.75, "x restored from the per-world entry");
            Assert.AreEqual(saved.Z, restored.Z, 0.75, "z restored from the per-world entry");
            Assert.Less(Mathf.Abs((float)(restored.Y - saved.Y)), 3f, "y restored from the per-world entry");

            // A second world of the same character starts at the world start, not at the first world's position.
            PressMenu(game, "system-menu");
            yield return null;
            Press(game, "system-start-menu");
            yield return null;
            yield return null;
            Press(game, "start-game");
            yield return null;
            Press(game, "character-0");
            yield return null;
            Press(game, "new-world");
            yield return null;
            Root(game).Q<TextField>("world-name").value = "Second";
            Root(game).Q<TextField>("world-seed").value = "777";
            Press(game, "create-world");
            yield return null;
            yield return WaitForWorld(game);
            Assert.AreEqual(SessionState.InGame, game.Session.State, game.Status);
            Assert.AreEqual("Second", game.Session.World.Name);
            Assert.AreEqual(2, game.Profiles.Worlds.Count, "worlds are a shared list, not paired with the character");
            WorldPosition second = game.Session.Hero.Position;
            WorldPosition secondStart = CubeGame.Features.World.WorldState.StartPosition(game.WorldState.Generator,
                game.Session.WorldDeltas);
            Assert.Less(Mathf.Abs((float)(second.X - secondStart.X)), 2f, "a new world starts at the world start");
            Assert.Less(Mathf.Abs((float)(second.Z - secondStart.Z)), 2f, "a new world starts at the world start");
            Assert.Greater(Mathf.Abs((float)(second.X - saved.X)) + Mathf.Abs((float)(second.Z - saved.Z)), 8f,
                "the first world's position is not reused in another world");
        }

        private static IEnumerator WaitForWorld(GameBootstrap game)
        {
            for (int i = 0; i < 300 && game.Session.State != SessionState.InGame; i++)
            {
                yield return null;
            }
        }

        private static VisualElement Root(GameBootstrap game) => game.GetComponentInChildren<UIDocument>().rootVisualElement;

        /// <summary>Alpha menu buttons exist only behind the Esc GUI flag (0x47E770): switch it on, then press.</summary>
        private static void PressMenu(GameBootstrap game, string name)
        {
            if (!game.MenuMode) game.ToggleMenuMode();
            Assert.AreEqual(DisplayStyle.Flex, Root(game).Q("menu-row").style.display.value, "menu row visible after Esc");
            Press(game, name);
        }

        private static void Press(GameBootstrap game, string name)
        {
            var button = Root(game).Q<Button>(name);
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.enabledInHierarchy, name);
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }
    }
}
