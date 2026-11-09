using System.Collections;
using System.IO;
using CubeGame.Core.Characters;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using CubeGame.Gameplay;
using CubeGame.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// M0-02 in the running game: Alpha terrain (voxel scale) at the centre of the 2^24-voxel world, hero on the
    /// generated ground, chunks streaming in/out while travelling, floating origin keeping scene coordinates small,
    /// a terrain change surviving save → menu → load.
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

        private GameBootstrap NewGame()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            return game;
        }

        [UnityTest]
        public IEnumerator HeroStartsOnAlphaTerrainAndChunksStreamWithFloatingOrigin()
        {
            GameBootstrap game = NewGame();
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot0", "Stream", 20131234u, "Walker", CharacterClass.Ranger), game.Status);
            Assert.AreEqual(AlphaTerrainGenerator.ContentId, game.Session.World.GeneratorId);
            Assert.AreEqual(WorldLayout.StartVoxel + 0.5, game.Session.Hero.Position.X, 1e-6);
            Assert.AreEqual(1, game.WorldState.Generator.Region(new RegionKey(512, 512)).Level, "start = centre region (Alpha level 1)");

            Transform player = game.PlayerTransform;
            Assert.Less(player.position.magnitude, 600f, "scene coordinates stay small (floating origin)");

            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(motor.IsGrounded, "hero stands on generated Alpha terrain");
            int ground = game.WorldState.SurfaceAt(WorldLayout.StartVoxel, WorldLayout.StartVoxel);
            Assert.Less(Mathf.Abs(player.position.y - (ground + 1)), 1.6f, $"hero y {player.position.y} vs column top {ground + 1}");

            game.WorldView.CompletePending();
            int full = (2 * 6 + 1) * (2 * 6 + 1);
            Assert.AreEqual(full, game.WorldView.LoadedCount);

            // Travel 40 chunks (1280 voxels) east: the origin rebases, far chunks are released and zones recorded.
            Vector2Int originBefore = game.WorldView.Origin;
            ChunkKey startChunk = game.WorldView.Center;
            int targetX = WorldLayout.StartVoxel + 40 * WorldLayout.ChunkSize;
            int targetGround = Mathf.Max(game.WorldState.SurfaceAt(targetX, WorldLayout.StartVoxel), 0);
            player.position = game.WorldView.ToScene(targetX + 0.5, targetGround + 3.0, WorldLayout.StartVoxel + 0.5);
            Physics.SyncTransforms();
            yield return Frames(2);
            game.WorldView.CompletePending();
            yield return Frames(2);

            Assert.AreNotEqual(originBefore, game.WorldView.Origin, "floating origin moved");
            Assert.Less(player.position.magnitude, 600f);
            Assert.AreEqual(targetX + 0.5, game.Session.Hero.Position.X, 0.6, "world position preserved across rebase");
            Assert.IsTrue(game.WorldView.IsLoaded(WorldLayout.ChunkOf(targetX, WorldLayout.StartVoxel)));
            Assert.IsFalse(game.WorldView.IsLoaded(startChunk), "start chunk released beyond keep radius");
            Assert.LessOrEqual(game.WorldView.LoadedCount, 17 * 17);
            Assert.Greater(game.WorldView.TotalReleased, 0);
            Assert.IsTrue(game.Session.WorldDeltas.IsDiscovered(new ZoneKey(32800, 32800)));
            Assert.IsTrue(game.Session.WorldDeltas.IsDiscovered(new ZoneKey(targetX >> 8, 32800)));

            // The target column exists in the rebased scene at its generated height (terrain colliders only).
            int targetSurface = game.WorldState.SurfaceAt(targetX, WorldLayout.StartVoxel);
            Vector3 above = game.WorldView.ToScene(targetX + 0.5, targetSurface + 60.0, WorldLayout.StartVoxel + 0.5);
            float terrainHit = float.NaN;
            foreach (RaycastHit h in Physics.RaycastAll(above, Vector3.down, 400f))
            {
                if (h.collider.name.StartsWith("Chunk_") && (float.IsNaN(terrainHit) || h.point.y > terrainHit))
                {
                    terrainHit = h.point.y;
                }
            }

            Assert.AreEqual(targetSurface + 1, terrainHit, 0.01f);
            Assert.IsTrue(game.ExitToMenu(false));
        }

        [UnityTest]
        public IEnumerator TerrainEditSurvivesSaveExitAndLoad()
        {
            GameBootstrap game = NewGame();
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot2", "Edit", 99u, "Digger", CharacterClass.Warrior), game.Status);
            int x = WorldLayout.StartVoxel + 5;
            int z = WorldLayout.StartVoxel + 5;
            int original = game.WorldState.SurfaceAt(x, z);
            int edited = original - 6;

            Assert.IsTrue(game.EditColumn(x, z, edited), game.Status);
            Assert.AreEqual(edited, game.WorldState.SurfaceAt(x, z));
            yield return Frames(10);

            Vector3 above = game.WorldView.ToScene(x + 0.5, original + 30, z + 0.5);
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out RaycastHit hit, 200f));
            Assert.AreEqual(edited + 1, hit.point.y, 0.01f, "collider follows the world delta");

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(File.Exists(Path.Combine(_saveDir, "slot2.world.sav")));
            Assert.IsTrue(game.ExitToMenu(false));
            yield return Frames(2);

            Assert.IsTrue(game.LoadWorld("slot2"), game.Status);
            Assert.AreEqual(edited, game.WorldState.SurfaceAt(x, z), "edit restored from the world file");
            yield return Frames(2);
            above = game.WorldView.ToScene(x + 0.5, original + 30, z + 0.5);
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out hit, 200f));
            Assert.AreEqual(edited + 1, hit.point.y, 0.01f);
            Assert.IsTrue(game.ExitToMenu(false));
        }

        [UnityTest]
        public IEnumerator VoxelEditBuildsColliderAndSurvivesSaveLoad()
        {
            GameBootstrap game = NewGame();
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot0", "Voxel", 99u, "Builder", CharacterClass.Warrior), game.Status);
            int x = WorldLayout.StartVoxel + 7;
            int z = WorldLayout.StartVoxel - 4;
            int ground = game.WorldState.SurfaceAt(x, z);
            uint stone = Voxel.Pack(90, 90, 90, 6);

            // A floating block two voxels above the ground: the mesher must give it a top face (collider) of its own.
            Assert.IsTrue(game.EditVoxel(x, ground + 3, z, stone), game.Status);
            Assert.AreEqual(ground + 3, game.WorldState.SurfaceAt(x, z));
            yield return Frames(10);

            Vector3 above = game.WorldView.ToScene(x + 0.5, ground + 30, z + 0.5);
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out RaycastHit hit, 200f));
            Assert.AreEqual(ground + 4, hit.point.y, 0.01f, "collider on the placed voxel");

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false));
            yield return Frames(2);
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            Assert.AreEqual(stone, game.WorldState.VoxelAt(x, ground + 3, z), "voxel restored from the world file");
            Assert.AreEqual(Voxel.Air, game.WorldState.VoxelAt(x, ground + 2, z));
            yield return Frames(2);
            above = game.WorldView.ToScene(x + 0.5, ground + 30, z + 0.5);
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out hit, 200f));
            Assert.AreEqual(ground + 4, hit.point.y, 0.01f);
            Assert.IsTrue(game.ExitToMenu(false));
        }
    }
}
