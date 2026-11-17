using System.Collections;
using System.IO;
using System.Linq;
using CubeGame.Core.Characters;
using CubeGame.Features.World;
using CubeGame.Features.World.Foundation;
using CubeGame.Gameplay;
using CubeGame.Gameplay.Streaming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// G2 runtime integration: entering a world through the real bootstrap produces a World Foundation context —
    /// biome table, budget applied to the streamer, distance strategy, debug overlay, metrics and a debug snapshot
    /// of the live world. Existing streaming/combat/save paths are covered by the rest of this suite.
    /// </summary>
    public sealed class G2WorldFoundationPlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_g2_" + System.Guid.NewGuid().ToString("N"));
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

        [UnityTest]
        public IEnumerator EnteringAWorldBuildsTheFoundationContext()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot0", "Foundation", 666666u, "Walker", CharacterClass.Ranger),
                game.Status);
            yield return null;

            CollectionAssert.IsEmpty(game.WorldFoundationProblems.ToArray(),
                string.Join("; ", game.WorldFoundationProblems));

            Assert.IsNotNull(game.WorldBiomes, "biome catalog must be created with the world");
            Assert.IsNotNull(game.WorldBudget, "streaming budget must be applied to the world");
            Assert.IsNotNull(game.WorldDistance, "distance strategy must be created with the world");
            Assert.IsNotNull(game.WorldMetrics, "performance recorder must exist");
            Assert.IsNotNull(game.DebugOverlay, "debug overlay must exist in the world root");
            Assert.IsNotNull(game.MetricsProbe, "metrics probe must exist in the world root");
            Assert.AreEqual(game.WorldBudget.VertexBudget, game.WorldView.VertexBudget,
                "the view must run with the budget's vertex limit");

            // Load a small area around the hero without draining the full 41x41 window.
            game.WorldView.LoadAround(game.PlayerTransform.position, 1);
            yield return null;

            WorldBudgetMeasurement measurement = game.MeasureWorldBudget();
            Assert.Greater(measurement.LoadedChunks, 0, "chunks must be installed");
            Assert.Greater(measurement.Vertices, 0, "chunks must carry vertices");
            Assert.LessOrEqual(measurement.Vertices, game.WorldBudget.VertexBudget);

            WorldBudgetReport report = WorldBudgetEvaluator.Evaluate(game.WorldBudget, measurement);
            Assert.AreNotEqual(WorldBudgetStatus.Over, report.Status, report.ToString());

            WorldDebugSnapshot snapshot = game.WorldFoundationSnapshot();
            Assert.AreEqual(666666u, snapshot.Seed);
            Assert.IsNotEmpty(snapshot.BiomeId, "biome must resolve for the hero position");
            Assert.Greater(snapshot.Lines.Count, 5);
            StringAssert.Contains("seed 666666", snapshot.Text);
            StringAssert.Contains("chunk", snapshot.Text);
            StringAssert.Contains("zone", snapshot.Text);
            StringAssert.Contains("budget", snapshot.Text);
            Assert.AreNotEqual(WorldBudgetStatus.Over, snapshot.BudgetStatus);

            WorldDebugOverlay overlay = game.DebugOverlay;
            overlay.SetVisible(true);
            yield return null;
            StringAssert.Contains("biome", overlay.CachedText);

            WorldPerformanceSnapshot performance = game.WorldMetrics.Snapshot();
            Assert.Greater(performance.ChunksGenerated, 0, "chunk builds must be measured");
            Assert.Greater(performance.FrameSamples, 0, "frame times must be sampled");
            string json = performance.ToJson();
            StringAssert.Contains("\"chunksGenerated\":", json);
            StringAssert.Contains("\"meanFrameMs\":", json);

            // A floating-origin rebase keeps the hero at the same world position: only scene coordinates change.
            // The bootstrap rebases back to the hero's own chunk on the next frame (RebaseIfFar), so the origin change
            // is asserted immediately after the call and the world identity before and after the frame.
            Vector2Int originBefore = game.WorldView.Origin;
            game.ShiftOrigin(600, 0);
            Assert.AreNotEqual(originBefore, game.WorldView.Origin, "the origin must move");
            WorldDebugSnapshot movedSameFrame = game.WorldFoundationSnapshot();
            Assert.AreEqual(666666u, movedSameFrame.Seed);
            Assert.AreEqual(snapshot.ChunkX, movedSameFrame.ChunkX,
                "a floating-origin shift must not move the hero in world space");
            Assert.AreEqual(snapshot.ZoneX, movedSameFrame.ZoneX);
            Assert.AreEqual(snapshot.BiomeId, movedSameFrame.BiomeId);

            yield return null;
            WorldDebugSnapshot movedNextFrame = game.WorldFoundationSnapshot();
            Assert.AreEqual(snapshot.ChunkX, movedNextFrame.ChunkX,
                "after the automatic rebase the hero is still in the same chunk");
            Assert.AreEqual(snapshot.BiomeId, movedNextFrame.BiomeId);
        }
    }
}
