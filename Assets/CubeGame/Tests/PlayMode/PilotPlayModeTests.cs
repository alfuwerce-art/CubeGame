using System.Collections;
using CubeGame.Core.Generation;
using CubeGame.Presentation.Player;
using CubeGame.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke cases of S01-PILOT P2: the world view really produces chunks with colliders
    /// and the temporary character really stands on the generated terrain.
    /// </summary>
    public sealed class PilotPlayModeTests
    {
        private const uint Seed = 666666u;

        [UnityTest]
        public IEnumerator WorldViewBuildsChunksWithColliders()
        {
            var root = new GameObject("TestWorldRoot");
            var view = root.AddComponent<PilotWorldView>();

            view.Build(Seed, 2, 0, 0);
            yield return null;

            Assert.AreEqual(4, view.ChunkCount, "2x2 chunks expected");
            Assert.Greater(view.LastVertexCount, 0, "no vertices generated");
            Assert.GreaterOrEqual(view.LastBuildMilliseconds, 0d);

            int colliders = 0;
            int children = root.transform.childCount;
            for (int i = 0; i < children; i++)
            {
                Transform chunk = root.transform.GetChild(i);
                if (chunk.GetComponent<MeshCollider>() != null && chunk.GetComponent<MeshRenderer>() != null)
                {
                    colliders++;
                }
            }

            Assert.AreEqual(4, colliders, "every chunk needs a MeshRenderer and a MeshCollider");
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerStandsOnGeneratedTerrain()
        {
            var root = new GameObject("TerrainRoot");
            var view = root.AddComponent<PilotWorldView>();
            view.Build(Seed, 3, 0, 0);
            yield return null;

            var field = new PilotHeightField(Seed);
            const int spawnX = 48;
            const int spawnZ = 48;
            int groundHeight = field.HeightAt(spawnX, spawnZ);

            var player = new GameObject("TestPlayer");
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            var motor = player.AddComponent<PlayerMotor>();

            player.transform.position = new Vector3(spawnX + 0.5f, groundHeight + 12f, spawnZ + 0.5f);
            // The controller keeps its own PhysX position; without the explicit sync the first
            // Move() starts from the origin (see PilotBootstrap.BuildScene).
            Physics.SyncTransforms();
            yield return null;

            float startY = player.transform.position.y;
            float deadline = Time.time + 6f;
            while (Time.time < deadline && !motor.IsGrounded)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(motor.IsGrounded,
                $"character did not land: pos={player.transform.position} startY={startY:F2} " +
                $"ground={groundHeight} velocity={controller.velocity}");
            Assert.Less(player.transform.position.y, startY, "character did not fall");
            Assert.AreEqual(groundHeight, player.transform.position.y, 1.6f, "landing height is far from the height field");

            Object.Destroy(player);
            Object.Destroy(root);
            yield return null;
        }
    }
}
