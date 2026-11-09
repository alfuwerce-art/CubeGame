using System.Collections;
using System.IO;
using CubeGame.Core.Characters;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using CubeGame.Gameplay;
using CubeGame.Gameplay.Streaming;
using CubeGame.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// M0-02 in the running game: Alpha-structured world at the centre of the 1024×1024 region grid, hero on the
    /// generated ground, regions stream in/out while travelling, a terrain change survives save → menu → load.
    /// </summary>
    public sealed class FullM0WorldStreamingPlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_fullm0_world_" + System.Guid.NewGuid().ToString("N"));
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

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
            }
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            player.position = position;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator HeroStartsOnAlphaTerrainAndRegionsStreamWhileTravelling()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot0", "Stream", 20131234u, "Walker", CharacterClass.Ranger), game.Status);
            Assert.AreEqual(AlphaTerrainGenerator.Id, game.Session.World.GeneratorId);
            Transform player = game.PlayerTransform;
            Assert.AreEqual(new RegionKey(512, 512), StreamingWorldView.RegionAt(player.position), "start = centre region (Alpha level 1)");
            Assert.AreEqual(1, game.WorldState.Generator.Region(new RegionKey(512, 512)).Level);

            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(motor.IsGrounded, "hero stands on generated Alpha terrain");
            int ground = game.WorldState.SurfaceAt(Mathf.FloorToInt(player.position.x), Mathf.FloorToInt(player.position.z));
            Assert.Less(Mathf.Abs(player.position.y - (ground + 1)), 1.6f, $"hero y {player.position.y} vs column top {ground + 1}");

            // Let the 5×5 view fill in.
            deadline = Time.realtimeSinceStartup + 10f;
            while (game.WorldView.PendingCount > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(25, game.WorldView.LoadedCount);

            // Travel 6 regions east: old regions beyond the keep radius are released and recorded as discovered.
            Vector3 east = player.position + new Vector3(6 * WorldLayout.RegionSize, 0f, 0f);
            int eastGround = game.WorldState.SurfaceAt(Mathf.FloorToInt(east.x), Mathf.FloorToInt(east.z));
            Teleport(player, new Vector3(east.x, Mathf.Max(eastGround, game.WorldState.Generator.WaterLevel) + 3f, east.z));
            deadline = Time.realtimeSinceStartup + 15f;
            while ((game.WorldView.PendingCount > 0 || !game.WorldView.IsLoaded(new RegionKey(518, 512))) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(game.WorldView.IsLoaded(new RegionKey(518, 512)));
            Assert.IsFalse(game.WorldView.IsLoaded(new RegionKey(512, 512)), "start region released beyond keep radius");
            Assert.LessOrEqual(game.WorldView.LoadedCount, 49);
            Assert.Greater(game.WorldView.TotalReleased, 0);
            Assert.IsTrue(game.Session.WorldDeltas.IsDiscovered(new RegionKey(512, 512)));
            Assert.IsTrue(game.Session.WorldDeltas.IsDiscovered(new RegionKey(518, 512)));
            yield return null;
            int regionObjects = 0;
            foreach (Transform child in game.WorldView.transform)
            {
                if (child.name.StartsWith("Region_"))
                {
                    regionObjects++;
                }
            }

            Assert.LessOrEqual(regionObjects, game.WorldView.LoadedCount + 2 * game.WorldView.BuildsPerFrame, "released regions are destroyed");
            Assert.IsTrue(game.ExitToMenu(false));
        }

        [UnityTest]
        public IEnumerator TerrainEditSurvivesSaveExitAndLoad()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot2", "Edit", 99u, "Digger", CharacterClass.Warrior), game.Status);
            int x = WorldLayout.StartBlock + 5;
            int z = WorldLayout.StartBlock + 5;
            int original = game.WorldState.SurfaceAt(x, z);
            int edited = original - 6;

            Assert.IsTrue(game.EditColumn(x, z, edited), game.Status);
            Assert.AreEqual(edited, game.WorldState.SurfaceAt(x, z));
            yield return Frames(8);

            // The rebuilt region collider has the pit: a ray straight down hits 6 blocks lower.
            Assert.IsTrue(Physics.Raycast(new Vector3(x + 0.5f, original + 30f, z + 0.5f), Vector3.down, out RaycastHit hit, 200f));
            Assert.AreEqual(edited + 1, hit.point.y, 0.01f, "collider follows the world delta");

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(File.Exists(Path.Combine(_saveDir, "slot2.world.sav")));
            Assert.IsTrue(game.ExitToMenu(false));
            yield return Frames(2);

            Assert.IsTrue(game.LoadWorld("slot2"), game.Status);
            Assert.AreEqual(edited, game.WorldState.SurfaceAt(x, z), "edit restored from the world file");
            yield return Frames(2);
            Assert.IsTrue(Physics.Raycast(new Vector3(x + 0.5f, original + 30f, z + 0.5f), Vector3.down, out hit, 200f));
            Assert.AreEqual(edited + 1, hit.point.y, 0.01f);
            Assert.IsTrue(game.ExitToMenu(false));
        }
    }
}
