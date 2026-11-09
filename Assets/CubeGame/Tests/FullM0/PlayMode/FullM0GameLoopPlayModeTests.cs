using System.Collections;
using System.IO;
using CubeGame.Core.Characters;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// M0-01 golden scenario in the real runtime: main menu → new world (terrain + hero on ground) →
    /// clock runs → move → save → exit to menu (views destroyed) → load → same world/hero/time → pause.
    /// </summary>
    public sealed class FullM0GameLoopPlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_fullm0_play_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }

            foreach (string name in new[] { "Main Camera", "Sun", "WorldRoot", "Player" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null)
                {
                    Object.Destroy(leftover);
                }
            }

            if (Directory.Exists(_saveDir))
            {
                Directory.Delete(_saveDir, true);
            }
        }

        private GameBootstrap CreateGame()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            return game;
        }

        private static int CountNamed(string name)
        {
            int count = 0;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name == name)
                {
                    count++;
                }
            }

            return count;
        }

        [UnityTest]
        public IEnumerator NewWorldSaveExitLoadContinuesSameGame()
        {
            GameBootstrap game = CreateGame();
            yield return null;
            Assert.AreEqual(SessionState.MainMenu, game.Session.State);
            Assert.IsFalse(game.Slots.Exists("slot0"));

            Assert.IsTrue(game.StartNewWorld("slot0", "Golden", 424242u, "Tester", CharacterClass.Mage), game.Status);
            Assert.AreEqual(SessionState.InGame, game.Session.State);
            Assert.Greater(game.WorldView.ChunkCount, 0);
            Assert.Greater(game.WorldView.LastVertexCount, 0);

            PlayerMotor motor = game.PlayerTransform.GetComponent<PlayerMotor>();
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(motor.IsGrounded, "hero must stand on generated terrain");

            long tickBefore = game.Session.Clock.Tick;
            deadline = Time.realtimeSinceStartup + 3f;
            while (game.Session.Clock.Tick < tickBefore + 15 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.GreaterOrEqual(game.Session.Clock.Tick, tickBefore + 15, "game clock advances in game");

            CharacterController controller = game.PlayerTransform.GetComponent<CharacterController>();
            for (int i = 0; i < 20; i++)
            {
                controller.Move(new Vector3(0.1f, -0.05f, 0.05f));
                yield return null;
            }

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Vector3 savedPosition = game.PlayerTransform.position;
            long savedTick = game.Session.Clock.Tick;
            Assert.IsTrue(game.Slots.Exists("slot0"));

            Assert.IsTrue(game.ExitToMenu(false), game.Status);
            yield return null;
            yield return null;
            Assert.AreEqual(SessionState.MainMenu, game.Session.State);
            Assert.IsNull(game.Session.World);
            Assert.AreEqual(0, CountNamed("WorldRoot"), "world view destroyed on exit");
            Assert.AreEqual(0, CountNamed("Player"), "player destroyed on exit");

            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            Assert.AreEqual(SessionState.InGame, game.Session.State);
            Assert.AreEqual(424242u, game.Session.World.Seed.Value);
            Assert.AreEqual("Golden", game.Session.World.Name);
            Assert.AreEqual(CharacterClass.Mage, game.Session.Hero.Sheet.Class);
            Assert.AreEqual(savedTick, game.Session.Clock.Tick);
            Assert.Less(Vector3.Distance(savedPosition, game.PlayerTransform.position), 0.01f, "hero restored at saved position");
            yield return null;
            Assert.AreEqual(1, CountNamed("WorldRoot"));
            Assert.AreEqual(1, CountNamed("Player"));

            game.TogglePause();
            Assert.AreEqual(SessionState.Paused, game.Session.State);
            long pausedTick = game.Session.Clock.Tick;
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(pausedTick, game.Session.Clock.Tick, "pause stops game time");
            Assert.IsFalse(game.PlayerTransform.GetComponent<PlayerMotor>().enabled, "no movement input while paused");

            Assert.IsTrue(game.ExitToMenu(true), game.Status);
            Assert.AreEqual(SessionState.MainMenu, game.Session.State);
        }

        [UnityTest]
        public IEnumerator RepeatedWorldsLeaveNoDuplicateViews()
        {
            GameBootstrap game = CreateGame();
            yield return null;
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(game.StartNewWorld("slot1", "Cycle" + i, (uint)(1000 + i), "Hero", CharacterClass.Warrior), game.Status);
                yield return null;
                Assert.AreEqual(1, CountNamed("WorldRoot"));
                Assert.AreEqual(1, CountNamed("Player"));
                Assert.IsTrue(game.ExitToMenu(false));
                yield return null;
            }

            Assert.AreEqual(0, CountNamed("WorldRoot"));
            Assert.AreEqual(0, CountNamed("Player"));
            Assert.IsFalse(game.LoadWorld("slot1"), "nothing was saved in slot1");
            Assert.AreEqual(SessionState.MainMenu, game.Session.State);
        }
    }
}
