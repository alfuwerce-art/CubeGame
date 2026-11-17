using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Features.World.Foundation;
using CubeGame.Gameplay;
using CubeGame.Persistence;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// G2 World Foundation (data-driven layer, no world generation): climate/biome, budget, distance bands,
    /// vegetation rules, structure pipeline, seams, entity contract, debug snapshot, metrics and generation identity.
    /// </summary>
    public sealed class G2WorldFoundationTests
    {
        private static readonly ClimateAxes Grassland = new ClimateAxes(0.5f, 0.5f, 0f, 0.1f, 2, 30, 0);

        // ---------------------------------------------------------------- G2-J generation identity

        [Test]
        public void GenerationProfileIsVersionedAndHashed()
        {
            CollectionAssert.IsEmpty(WorldGenerationProfile.Validate());
            StringAssert.Contains("cubegame.world/v1", WorldGenerationProfile.Describe());
            Assert.AreNotEqual(0UL, WorldGenerationProfile.Hash());
            Assert.AreEqual(WorldGenerationProfile.Hash(), WorldGenerationProfile.Hash());
        }

        [Test]
        public void GeneratorVersionIsParsedFromTheIdSuffix()
        {
            Assert.AreEqual(9, WorldGenerationIdentity.VersionOf(AlphaTerrainGenerator.WorldRuntimeId));
            Assert.AreEqual(7, WorldGenerationIdentity.VersionOf(AlphaTerrainGenerator.EnvironmentRuntimeId));
            Assert.AreEqual(0, WorldGenerationIdentity.VersionOf("no-version"));
            Assert.AreEqual(0, WorldGenerationIdentity.VersionOf(null));
            Assert.AreEqual(0, WorldGenerationIdentity.VersionOf("cubegame.alpha_terrain/"));
        }

        [Test]
        public void FingerprintDependsOnGeneratorSeedAndProfile()
        {
            string a = WorldGenerationIdentity.Fingerprint("cubegame.alpha_terrain/9", 666666u);
            Assert.AreEqual(16, a.Length);
            Assert.AreEqual(a, WorldGenerationIdentity.Fingerprint("cubegame.alpha_terrain/9", 666666u));
            Assert.AreNotEqual(a, WorldGenerationIdentity.Fingerprint("cubegame.alpha_terrain/9", 666667u));
            Assert.AreNotEqual(a, WorldGenerationIdentity.Fingerprint("cubegame.alpha_terrain/8", 666666u));
        }

        [Test]
        public void CompatibilityAcceptsMatchingAndLegacyIdentities()
        {
            const string gen = "cubegame.alpha_terrain/9";
            string fingerprint = WorldGenerationIdentity.Fingerprint(gen, 42u);

            WorldCompatibilityReport ok = WorldGenerationIdentity.Check(gen, 42u, fingerprint, gen, 42u);
            Assert.AreEqual(WorldCompatibility.Compatible, ok.Status);
            Assert.IsTrue(ok.CanLoad);

            WorldCompatibilityReport legacy = WorldGenerationIdentity.Check(gen, 42u, string.Empty, gen, 42u);
            Assert.AreEqual(WorldCompatibility.LegacyWorld, legacy.Status);
            Assert.IsTrue(legacy.CanLoad);
        }

        [Test]
        public void CompatibilityRefusesAChangedProfileOrGenerator()
        {
            const string gen = "cubegame.alpha_terrain/9";
            string fingerprint = WorldGenerationIdentity.Fingerprint(gen, 42u);

            WorldCompatibilityReport profile =
                WorldGenerationIdentity.Check(gen, 42u, "0000000000000000", gen, 42u);
            Assert.AreEqual(WorldCompatibility.ProfileChanged, profile.Status);
            Assert.IsFalse(profile.CanLoad);
            StringAssert.Contains("profile", profile.Reason);

            WorldCompatibilityReport generator =
                WorldGenerationIdentity.Check("cubegame.alpha_terrain/8", 42u, fingerprint, gen, 42u);
            Assert.AreEqual(WorldCompatibility.GeneratorChanged, generator.Status);
            Assert.IsFalse(generator.CanLoad);

            WorldCompatibilityReport malformed = WorldGenerationIdentity.Check(gen, 42u, "not-hex", gen, 42u);
            Assert.AreEqual(WorldCompatibility.Malformed, malformed.Status);
            Assert.IsFalse(malformed.CanLoad);
        }

        private sealed class MemoryStore : ISaveStore
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();

            public bool Exists(string key) => Files.ContainsKey(key);

            public string Read(string key) => Files.TryGetValue(key, out string value) ? value : null;

            public void Write(string key, string contents) => Files[key] = contents;

            public void Delete(string key) => Files.Remove(key);
        }

        [Test]
        public void WorldFileRecordsTheGenerationFingerprint()
        {
            var store = new MemoryStore();
            var slots = new GameSlotStore(store, new JsonSaveSerializer(), new JsonSessionSerializer(),
                new JsonWorldSerializer());
            var session = new GameSession();
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest
            {
                WorldName = "G2 Identity",
                Seed = 424242u,
                GeneratorId = AlphaTerrainGenerator.WorldRuntimeId,
                HeroName = "Bo",
                Class = CharacterClass.Warrior,
                Spawn = new WorldPosition(10.5, 40, 5.5),
            }, 7L));
            Assert.IsTrue(session.CompleteLoading());
            SaveResult saved = slots.Save(session, 11L);
            Assert.IsTrue(saved.IsSuccess, saved.ToString());

            string framed = store.Read(GameSlotStore.WorldKey("slot0"));
            Assert.IsTrue(GameSlotStore.TryUnframe(framed, WorldSaveFormat.Header, out _, out string payload,
                out string error), error);
            WorldSaveData world = new JsonWorldSerializer().Deserialize(payload);
            Assert.IsNotNull(world);
            Assert.AreEqual(
                WorldGenerationIdentity.Fingerprint(AlphaTerrainGenerator.WorldRuntimeId, 424242u),
                world.generationFingerprint);

            SlotLoadResult loaded = slots.Load("slot0", AlphaTerrainGenerator.WorldRuntimeId);
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
        }

        // ---------------------------------------------------------------- seeds

        [Test]
        public void NamedSeedTableMatchesTheVerifiedAlphaTable()
        {
            CollectionAssert.IsEmpty(WorldSeedTable.Validate());
            var named = new WorldSeedTable(777u);
            var alpha = new AlphaWorldSeeds(777u);
            Assert.AreEqual(777u, named.MasterSeed);
            Assert.AreEqual(alpha.WorldSeed, named.WorldSeed);
            Assert.AreEqual(alpha.At(0x800168), named.HeightFineX);
            Assert.AreEqual(alpha.At(0x80016C), named.HeightFineZ);
            Assert.AreEqual(alpha.At(0x800170), named.HeightBroadX);
            Assert.AreEqual(alpha.At(0x800174), named.HeightBroadZ);
            Assert.AreEqual(alpha.At(0x800178), named.CaveX);
            Assert.AreEqual(alpha.At(0x80017C), named.CaveZ);
            Assert.AreEqual(alpha.At(0x8001BC), named.BaseLowX);
            Assert.AreEqual(alpha.At(0x8001DC), named.MountainX);
            Assert.AreEqual(13, named.Describe().Count);
            foreach (WorldSeedValue value in named.Describe())
            {
                Assert.AreEqual(value.Value, named.At(value.Axis));
                Assert.AreNotEqual(0, value.AlphaOffset);
            }
        }

        // ---------------------------------------------------------------- climate + biomes

        [Test]
        public void ClimateAxesClampAndDescribeWater()
        {
            CollectionAssert.IsEmpty(ClimateMath.Validate());
            var axes = new ClimateAxes(5f, -2f, float.NaN, 2f, 1, 12, 0);
            Assert.AreEqual(1f, axes.Temperature);
            Assert.AreEqual(0f, axes.Humidity);
            Assert.AreEqual(0f, axes.Rareness);
            Assert.AreEqual(1f, axes.Relief);
            Assert.AreEqual(12, axes.HeightAboveWater);
            Assert.IsFalse(axes.IsUnderwater);

            var deep = new ClimateAxes(0.5f, 0.5f, 0f, 0f, 1, -4, 0);
            Assert.IsTrue(deep.IsUnderwater);
            Assert.AreEqual(-4, deep.HeightAboveWater);
            StringAssert.Contains("level=1", deep.Describe());
        }

        [Test]
        public void BiomeCatalogIsValidAndCoversTheClimateSpace()
        {
            BiomeCatalog biomes = BiomeCatalog.Default();
            CollectionAssert.IsEmpty(biomes.Validate(), string.Join("; ", biomes.Validate()));
            Assert.GreaterOrEqual(biomes.Biomes.Count, 10);
            Assert.IsNotNull(biomes.Resolve(Grassland));
            Assert.IsNotNull(biomes.Resolve(new ClimateAxes(0.1f, 0.2f, 0f, 0.1f, 1, 20, 0)));
            Assert.IsNotNull(biomes.Resolve(new ClimateAxes(0.9f, 0.95f, 0f, 0.2f, 1, 20, 0)));
            Assert.IsNotNull(biomes.Resolve(new ClimateAxes(0.9f, 0.9f, 0f, 0.9f, 5, 200, 0)));
            Assert.AreEqual("biome.water",
                biomes.Resolve(new ClimateAxes(0.5f, 0.5f, 0f, 0f, 1, -20, 0)).Id);
            Assert.IsFalse(biomes.Resolve(new ClimateAxes(0.5f, 0.5f, 0f, 0f, 1, 5, 0)).Id.Contains("water"));
        }

        [Test]
        public void BiomeResolutionIsDeterministicAndPriorityAware()
        {
            var wide = new BiomeDefinition
            {
                Id = "biome.wide", DisplayName = "Wide", Priority = 10,
                Climate = new ClimateEnvelope(),
                SurfaceProfileId = "surface.grass", VegetationProfileId = "vegetation.grassland",
                SpawnProfileId = "spawn.grassland",
            };
            var narrow = new BiomeDefinition
            {
                Id = "biome.narrow", DisplayName = "Narrow", Priority = 5,
                Climate = new ClimateEnvelope { MinTemperature = 0.4f, MaxTemperature = 0.6f },
                SurfaceProfileId = "surface.grass", VegetationProfileId = "vegetation.grassland",
                SpawnProfileId = "spawn.grassland",
            };
            var equal = new BiomeDefinition
            {
                Id = "biome.equal", DisplayName = "Equal", Priority = 10,
                Climate = new ClimateEnvelope { MinTemperature = 0.4f, MaxTemperature = 0.6f },
                SurfaceProfileId = "surface.grass", VegetationProfileId = "vegetation.grassland",
                SpawnProfileId = "spawn.grassland",
            };

            BiomeCatalog defaults = BiomeCatalog.Default();
            var catalog = new BiomeCatalog(new[] { wide, narrow, equal }, defaults.Surfaces, defaults.Vegetation,
                defaults.Spawns);
            BiomeDefinition first = catalog.Resolve(Grassland);
            Assert.AreEqual("biome.equal", first.Id, "equal priority: the narrower envelope must win");
            Assert.AreEqual(first.Id, catalog.Resolve(Grassland).Id, "resolution must be deterministic");
            Assert.AreEqual("biome.wide",
                catalog.Resolve(new ClimateAxes(0.9f, 0.5f, 0f, 0.1f, 2, 30, 0)).Id);
        }

        // ---------------------------------------------------------------- budget + distance

        [Test]
        public void StreamingBudgetExplainsTheCurrentWindow()
        {
            CollectionAssert.IsEmpty(WorldStreamingBudget.Validate());
            WorldStreamingBudget budget = WorldStreamingBudget.Default();
            Assert.AreEqual(1681, budget.VisibleChunks);
            Assert.AreEqual(1681, budget.RetainedChunks);
            Assert.AreEqual(12582912L, budget.VertexBudget);
            Assert.IsTrue(budget.WindowExceedsVertexBudget, "the current window is a known soft overflow");
            Assert.Greater(budget.AffordableChunks, 1300);
            Assert.Less(budget.AffordableChunks, budget.VisibleChunks);
            StringAssert.Contains("affordable=", budget.Describe());
        }

        [Test]
        public void BudgetEvaluatorSeparatesSoftAndHardOverages()
        {
            WorldStreamingBudget budget = WorldStreamingBudget.Default();
            var within = new WorldBudgetMeasurement(25, 25, 100000, 10, 5, 10, 4, 4);
            Assert.AreEqual(WorldBudgetStatus.WithinBudget, WorldBudgetEvaluator.Evaluate(budget, within).Status);

            var soft = new WorldBudgetMeasurement(25, 25, (int)budget.VertexBudget + 1000, 10, 5, 5000, 4, 4);
            WorldBudgetReport softReport = WorldBudgetEvaluator.Evaluate(budget, soft);
            Assert.AreEqual(WorldBudgetStatus.SoftOver, softReport.Status,
                "a 5 % vertex overshoot is the streamer releasing the surplus, not a failure");
            Assert.IsTrue(WorldBudgetEvaluator.IsAcceptable(softReport));
            Assert.Greater(softReport.Overages.Count, 0);

            var hard = new WorldBudgetMeasurement(25, 25, (int)(budget.VertexBudget * 1.2), 10, 5, 10, 4, 4);
            WorldBudgetReport hardReport = WorldBudgetEvaluator.Evaluate(budget, hard);
            Assert.AreEqual(WorldBudgetStatus.Over, hardReport.Status);
            Assert.IsFalse(WorldBudgetEvaluator.IsAcceptable(hardReport));

            Assert.AreEqual(WorldBudgetStatus.Unknown, WorldBudgetEvaluator.Evaluate(null, hard).Status);
        }

        [Test]
        public void DistanceStrategyTilesTheViewAndKeepsFogInsideIt()
        {
            CollectionAssert.IsEmpty(WorldDistanceStrategy.Validate());
            WorldDistanceStrategy strategy = WorldDistanceStrategy.Default();
            Assert.AreEqual(WorldStreamingPolicy.AlphaChunkRadius, strategy.ViewRadius);
            Assert.AreEqual(WorldDistanceBand.Near, strategy.Classify(0));
            Assert.AreEqual(WorldDistanceBand.Far, strategy.Classify(strategy.ViewRadius));
            Assert.AreEqual(WorldDistanceBand.Offscreen, strategy.Classify(strategy.ViewRadius + 1));
            Assert.IsTrue(strategy.AllowsVegetation(1));
            Assert.IsFalse(strategy.AllowsVegetation(strategy.ViewRadius));
            Assert.IsTrue(strategy.AllowsCreatureSimulation(1));
            Assert.IsFalse(strategy.AllowsCreatureSimulation(strategy.ViewRadius));
            Assert.Less(strategy.FogStartVoxels(32), strategy.FogEndVoxels(32));
            Assert.LessOrEqual(strategy.FogEndVoxels(32), strategy.ViewRadius * 32);
            StringAssert.Contains("view=20", strategy.Describe());
        }

        // ---------------------------------------------------------------- vegetation

        [Test]
        public void VegetationRulesRejectSlopeWaterAndDrySites()
        {
            VegetationRuleSet grassland = VegetationRuleSet.Grassland();
            CollectionAssert.IsEmpty(grassland.Validate(), string.Join("; ", grassland.Validate()));

            var grassRule = new VegetationRule
            {
                Id = "test.grass", Kinds = new[] { VegetationKind.Grass }, MaxSlope = 0.35f,
                MinHeightAboveWater = 1, UnderwaterRequirement = 0, MinMoisture = 0.1f,
            };
            var set = new VegetationRuleSet("vegetation.rules.test", new[] { grassRule });
            CollectionAssert.IsEmpty(set.Validate());

            var good = new VegetationSiteSample("biome.grassland", 100, 100, 0.1f, 3, 0.5f, 0.5f, 4, false);
            Assert.IsTrue(set.Evaluate(good).Allowed);

            var steep = new VegetationSiteSample("biome.grassland", 100, 100, 0.9f, 3, 0.5f, 0.5f, 4, false);
            VegetationDecision steepDecision = set.Evaluate(steep);
            Assert.IsFalse(steepDecision.Allowed);
            Assert.AreEqual(VegetationRejection.SlopeOutOfRange, steepDecision.Rejection);

            var atWaterLevel = new VegetationSiteSample("biome.grassland", 100, 100, 0.1f, 0, 0.5f, 0.5f, 0, false);
            Assert.AreEqual(VegetationRejection.HeightOutOfRange, set.Evaluate(atWaterLevel).Rejection);

            var wet = new VegetationSiteSample("biome.grassland", 100, 100, 0.1f, 2, 0.05f, 0.5f, 0, false);
            Assert.AreEqual(VegetationRejection.MoistureOutOfRange, set.Evaluate(wet).Rejection);

            // The water rule set serves one biome with a shore rule and deep-water rules: a deep site must be
            // accepted by the underwater rule even though the shore rule rejects it first.
            VegetationRuleSet waterSet = VegetationRuleSet.Water();
            var shore = new VegetationSiteSample("biome.water", 100, 100, 0.1f, 2, 0.9f, 0.5f, 3, false);
            var deep = new VegetationSiteSample("biome.water", 100, 100, 0.1f, -2, 0.9f, 0.5f, 0, true);
            Assert.IsTrue(waterSet.Evaluate(shore).Allowed);
            Assert.IsTrue(waterSet.Evaluate(deep).Allowed);
            Assert.IsFalse(VegetationRuleSet.Sparse().Evaluate(deep).Allowed);
        }

        [Test]
        public void VegetationDensityAndKindAreDeterministic()
        {
            VegetationRuleSet grassland = VegetationRuleSet.Grassland();
            VegetationRule rule = grassland.ResolveRule(
                new VegetationSiteSample("biome.grassland", 10, 20, 0.1f, 2, 0.5f, 0.5f, 2, false));
            Assert.IsNotNull(rule);
            var site = new VegetationSiteSample("biome.grassland", 120, 130, 0.2f, 2, 0.5f, 0.5f, 2, false);
            bool first = VegetationPolicy.PassesDensityRoll(rule, site, 99u, 0);
            Assert.AreEqual(first, VegetationPolicy.PassesDensityRoll(rule, site, 99u, 0));
            Assert.AreEqual(VegetationPolicy.ChooseKind(rule, site, 99u, 0),
                VegetationPolicy.ChooseKind(rule, site, 99u, 0));

            int passes = 0;
            int fails = 0;
            for (int slot = 0; slot < 200; slot++)
            {
                if (VegetationPolicy.PassesDensityRoll(rule, site, 99u, slot)) passes++;
                else fails++;
            }

            Assert.Greater(passes, 0);
            Assert.Greater(fails, 0);
        }

        [Test]
        public void VegetationSpacingAndClusterLimitsAreEnforced()
        {
            var rule = new VegetationRule
            {
                Id = "test.rule", Kinds = new[] { VegetationKind.Flower }, Density = 1f,
                SpacingVoxels = 4, ClusterSize = 2, ClusterRadiusVoxels = 10,
            };
            CollectionAssert.IsEmpty(rule.Validate());
            var site = new VegetationSiteSample("biome.grassland", 100, 100, 0f, 2, 0.5f, 0.5f, 2, false);
            var close = new VegetationSiteSample("biome.grassland", 102, 100, 0f, 2, 0.5f, 0.5f, 2, false);
            var far = new VegetationSiteSample("biome.grassland", 140, 140, 0f, 2, 0.5f, 0.5f, 2, false);

            var accepted = new List<VegetationSiteSample> { close };
            Assert.IsFalse(VegetationPolicy.RespectsSpacing(rule, site, accepted));
            Assert.IsTrue(VegetationPolicy.RespectsSpacing(rule, far, accepted));

            var cluster = new List<VegetationSiteSample>
            {
                new VegetationSiteSample("biome.grassland", 98, 100, 0f, 2, 0.5f, 0.5f, 2, false),
                new VegetationSiteSample("biome.grassland", 100, 98, 0f, 2, 0.5f, 0.5f, 2, false),
            };
            var inCluster = new VegetationSiteSample("biome.grassland", 105, 100, 0f, 2, 0.5f, 0.5f, 2, false);
            Assert.IsFalse(VegetationPolicy.RespectsSpacing(rule, inCluster, cluster),
                "two plants already stand inside the 10-voxel cluster radius (ClusterSize = 2)");
            Assert.IsTrue(VegetationPolicy.RespectsSpacing(rule, far, cluster),
                "the far site is outside spacing and cluster limits and must be allowed");
        }

        [Test]
        public void VegetationDecisionReportsBudgetAndRule()
        {
            var always = new VegetationRule
            {
                Id = "test.always", Kinds = new[] { VegetationKind.Flower }, Density = 1f, MaxSlope = 1f,
                MinHeightAboveWater = -1000, UnderwaterRequirement = -1,
            };
            var set = new VegetationRuleSet("vegetation.rules.always", new[] { always });
            var site = new VegetationSiteSample("biome.grassland", 1, 1, 0f, 2, 0.5f, 0.5f, 2, false);
            VegetationDecision budget = VegetationPolicy.Decide(set, site, 1u, 0, null, 5, 5);
            Assert.IsFalse(budget.Allowed);
            Assert.AreEqual(VegetationRejection.ZoneBudgetReached, budget.Rejection);

            VegetationDecision open = VegetationPolicy.Decide(set, site, 1u, 0, null, 0, 100);
            Assert.IsTrue(open.Allowed);
            Assert.AreEqual("test.always", open.RuleId);
            Assert.AreEqual(VegetationKind.Flower, open.Kind);
        }

        [Test]
        public void EveryVegetationKindHasATemporaryModelBinding()
        {
            CollectionAssert.IsEmpty(VegetationModelBinding.Validate());
            Assert.AreEqual(AlphaVegetationModel.Grass, VegetationModelBinding.ModelOf(VegetationKind.Grass));
            Assert.AreEqual(AlphaVegetationModel.Coral, VegetationModelBinding.ModelOf(VegetationKind.Coral));
            StringAssert.Contains("TEMPORARY_RUNTIME", VegetationModelBinding.Status);
        }

        // ---------------------------------------------------------------- structures

        private sealed class StubProbe : IStructureProbe
        {
            public string Biome = "biome.grassland";
            public int Surface = 30;
            public bool Free = true;
            public int Flatness;

            public StructureFootprint Probe(int x, int z, int width, int depth) =>
                new StructureFootprint(Free, Surface, Surface + Flatness, Biome);
        }

        [Test]
        public void StructurePipelineIsDeterministicWithStableIds()
        {
            StructureRuleSet rules = StructureRuleSet.Default();
            CollectionAssert.IsEmpty(rules.Validate(), string.Join("; ", rules.Validate()));
            BiomeCatalog biomes = BiomeCatalog.Default();
            var probe = new StubProbe();
            var first = StructurePlacementPipeline.PlanZone(rules, biomes, 777u, 32800, 32800, 2, probe,
                new List<StructureRejection>(), new List<string>());
            var second = StructurePlacementPipeline.PlanZone(rules, biomes, 777u, 32800, 32800, 2, probe,
                new List<StructureRejection>(), new List<string>());

            Assert.GreaterOrEqual(first.Count, 4, "grassland level 2 must accept the basic rules");
            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].PersistentId, second[i].PersistentId);
                Assert.AreEqual(first[i].X, second[i].X);
                Assert.AreEqual(first[i].Kind, second[i].Kind);
                Assert.AreEqual(StructureRuntimeState.Generated, first[i].State);
                StringAssert.StartsWith("structure:", first[i].PersistentId);
                Assert.IsTrue(first[i].X >= 32800 * 256 && first[i].X <= 32800 * 256 + 256);
            }

            Assert.IsTrue(first.Exists(p => p.Kind == StructureKind.Village));
            Assert.IsTrue(first.Exists(p => p.Kind == StructureKind.DungeonEntrance));
            var other = StructurePlacementPipeline.PlanZone(rules, biomes, 778u, 32800, 32800, 2, probe,
                new List<StructureRejection>(), new List<string>());
            Assert.AreNotEqual(first[0].PersistentId, other[0].PersistentId);
        }

        [Test]
        public void StructurePipelineDoesNotForceAPlacement()
        {
            StructureRuleSet rules = StructureRuleSet.Default();
            BiomeCatalog biomes = BiomeCatalog.Default();
            var blocked = new StubProbe { Free = false };
            var rejections = new List<StructureRejection>();
            var placements = StructurePlacementPipeline.PlanZone(rules, biomes, 777u, 32800, 32800, 2, blocked,
                rejections, new List<string>());
            Assert.IsFalse(placements.Exists(p => p.Kind == StructureKind.Village));
            Assert.IsFalse(placements.Exists(p => p.Kind == StructureKind.Camp));
            CollectionAssert.Contains(rejections, StructureRejection.FootprintNotEmpty);

            var sloped = new StubProbe { Flatness = 5 };
            var slopedRejections = new List<StructureRejection>();
            StructurePlacementPipeline.PlanZone(rules, biomes, 777u, 32800, 32800, 2, sloped, slopedRejections,
                new List<string>());
            CollectionAssert.Contains(slopedRejections, StructureRejection.NotFlat);

            var wrongBiome = new StubProbe { Biome = "biome.water" };
            var biomeRejections = new List<StructureRejection>();
            StructurePlacementPipeline.PlanZone(rules, biomes, 777u, 32800, 32800, 2, wrongBiome, biomeRejections,
                new List<string>());
            CollectionAssert.Contains(biomeRejections, StructureRejection.RuleBiome);

            var lowLevel = StructurePlacementPipeline.PlanZone(rules, biomes, 777u, 32800, 32800, 0,
                new StubProbe(), new List<StructureRejection>(), new List<string>());
            CollectionAssert.IsEmpty(lowLevel);
        }

        [Test]
        public void StructureRuntimeStateTransitionsAreClosed()
        {
            Assert.IsTrue(StructureRuntimeState.CanTransition(StructureRuntimeState.Candidate,
                StructureRuntimeState.Generated));
            Assert.IsTrue(StructureRuntimeState.CanTransition(StructureRuntimeState.Generated,
                StructureRuntimeState.Destroyed));
            Assert.IsTrue(StructureRuntimeState.CanTransition(StructureRuntimeState.Discovered,
                StructureRuntimeState.Cleared));
            Assert.IsFalse(StructureRuntimeState.CanTransition(StructureRuntimeState.Destroyed,
                StructureRuntimeState.Generated));
            Assert.IsFalse(StructureRuntimeState.CanTransition("unknown", StructureRuntimeState.Cleared));
        }

        [Test]
        public void StructureRemovalSurvivesThroughTheWorldDeltaStore()
        {
            StructureRuleSet rules = StructureRuleSet.Default();
            BiomeCatalog biomes = BiomeCatalog.Default();
            var placements = StructurePlacementPipeline.PlanZone(rules, biomes, 777u, 32800, 32800, 2,
                new StubProbe(), new List<StructureRejection>(), new List<string>());
            StructurePlacement target = placements.Find(p => p.Kind == StructureKind.Village);
            Assert.IsNotNull(target);

            Assert.IsTrue(StructurePersistentIds.TryParse(target.PersistentId, out uint seed, out int zoneX,
                out int zoneZ, out string ruleId, out int slot));
            Assert.AreEqual(777u, seed);
            Assert.AreEqual(32800, zoneX);
            Assert.AreEqual("structure.village", ruleId);
            Assert.IsTrue(StructurePersistentIds.TryGetDeltaKey(target.PersistentId, out ZoneKey zoneKey,
                out int channel, out int deltaSlot));
            Assert.AreEqual(StructurePersistentIds.Channel, channel);

            var deltas = new WorldDeltaStore();
            deltas.Record(new WorldDelta
            {
                Region = new RegionKey(zoneX >> 6, zoneZ >> 6),
                Kind = WorldDeltaKind.ObjectRemoved,
                X = zoneKey.X,
                Y = channel * 4096 + deltaSlot,
                Z = zoneKey.Z,
                Value = 1,
                TimeMs = 5,
            });
            Assert.IsTrue(deltas.IsObjectRemoved(zoneKey, channel, deltaSlot));

            // Every structure of one zone must own a distinct removal key, otherwise removing a village would
            // silently remove the ruins next to it.
            var keys = new HashSet<(int, int)>();
            foreach (StructurePlacement placement in placements)
            {
                Assert.IsTrue(StructurePersistentIds.TryGetDeltaKey(placement.PersistentId, out _, out int ch,
                    out int idx));
                Assert.IsTrue(keys.Add((ch, idx)), $"two structures share the removal key of {placement.PersistentId}");
            }

            List<StructurePlacement> alive = StructurePlacementPipeline.ApplyRemovals(placements, deltas);
            Assert.AreEqual(placements.Count - 1, alive.Count);
            Assert.IsFalse(alive.Exists(p => p.PersistentId == target.PersistentId));
        }

        // ---------------------------------------------------------------- seams

        [Test]
        public void SettlementSeamIsDeterministicAndCarriesAnchorsAndRoads()
        {
            BiomeCatalog biomes = BiomeCatalog.Default();
            StructureRuleSet rules = StructureRuleSet.Default();
            var probe = new WorldFoundationValidation.FixedSeamProbe(
                new ClimateAxes(0.5f, 0.6f, 0f, 0.1f, 2, 30, 0), biomes, 30);
            SettlementSite first = SettlementSeamPlanner.Plan(4242u, 512, 512, probe, rules);
            SettlementSite second = SettlementSeamPlanner.Plan(4242u, 512, 512, probe, rules);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(first.PersistentId, second.PersistentId);
            Assert.AreEqual(first.ZoneX, second.ZoneX);
            Assert.AreEqual(first.Anchors.Count, second.Anchors.Count);
            Assert.GreaterOrEqual(first.Anchors.Count, 8);
            Assert.AreEqual(4, first.Roads.Count);
            Assert.IsTrue(first.Anchors.Exists(a => a.Kind == WorldAnchorKind.Inn));
            Assert.IsTrue(first.Anchors.Exists(a => a.Kind == WorldAnchorKind.QuestGiver));
            foreach (NpcAnchorSpec anchor in first.Anchors) Assert.IsTrue(WorldSeamIds.IsAnchor(anchor.PersistentId));
            foreach (RoadConnectionPoint road in first.Roads) Assert.IsTrue(WorldSeamIds.IsRoad(road.PersistentId));
            StringAssert.StartsWith("seam.settlement:", first.PersistentId);

            SettlementSite otherSeed = SettlementSeamPlanner.Plan(4243u, 512, 512, probe, rules);
            Assert.AreNotEqual(first.PersistentId, otherSeed.PersistentId);

            var outOfLevel = new WorldFoundationValidation.FixedSeamProbe(
                new ClimateAxes(0.5f, 0.6f, 0f, 0.1f, 12, 30, 0), biomes, 30);
            Assert.IsNull(SettlementSeamPlanner.Plan(4242u, 512, 512, outOfLevel, rules));
        }

        [Test]
        public void DungeonSeamPublishesAStableInstance()
        {
            BiomeCatalog biomes = BiomeCatalog.Default();
            var probe = new WorldFoundationValidation.FixedSeamProbe(Grassland, biomes, 30);
            DungeonEntranceSite first = DungeonSeamPlanner.Plan(99u, 32800, 32800, probe);
            DungeonEntranceSite second = DungeonSeamPlanner.Plan(99u, 32800, 32800, probe);
            Assert.IsNotNull(first);
            Assert.AreEqual(first.PersistentId, second.PersistentId);
            Assert.AreEqual(first.DungeonInstanceId, second.DungeonInstanceId);
            StringAssert.StartsWith("dungeon.instance:", first.DungeonInstanceId);
            Assert.IsTrue(WorldSeamIds.IsDungeon(first.PersistentId));
            Assert.GreaterOrEqual(first.Level, 1);
            Assert.GreaterOrEqual(first.Stars, 1);
            Assert.AreEqual(30 + 1, first.Y);
        }

        // ---------------------------------------------------------------- G3 entity seam

        [Test]
        public void EntityDefinitionContractValidatesAndAdaptsSpawns()
        {
            var registry = new EntityDefinitionRegistry();
            var definition = new EntityDefinition
            {
                Id = "entity.wolf", Family = "beast", SpeciesKind = 0x1c, BehaviourKey = "creature_ai",
                Level = 4, Stars = 1, Disposition = 1,
                Visual = new EntityVisualRecipe
                {
                    Id = "recipe.wolf", Source = VisualSourceKind.ImportedAsset, ModelKey = "wolf_v1",
                    Rig = new RigAnimationPackage { Id = "rig.wolf", RigId = "quadruped", AnimationSetId = "wolf_base" },
                },
            };
            Assert.IsTrue(registry.Register(definition));
            Assert.IsTrue(registry.TryGet("entity.wolf", out EntityDefinition found));
            Assert.AreEqual("wolf_v1", found.Visual.ModelKey);
            CollectionAssert.IsEmpty(registry.Validate(), string.Join("; ", registry.Validate()));

            ResolvedEntitySpawn spawn = EntitySpawnContract.Resolve(definition, 1.5, 30, -2.5, 90f, 0u);
            Assert.AreEqual("wolf_v1", spawn.VisualKey);
            Assert.AreEqual("quadruped", spawn.RigKey);
            Assert.AreEqual(EntitySpawnContract.AppearanceSeedOf(123456789UL),
                EntitySpawnContract.AppearanceSeedOf(123456789UL));

            var native = new SpawnDefinition
            {
                Region = new RegionKey(1, 2), SpawnIndex = 3, Kind = new KindId(0x1c), Faction = 1, Level = 5,
                BehaviourKey = "creature_ai", Position = new WorldPosition(1, 2, 3),
            };
            EntityDefinition adapted = EntitySpawnContract.FromSpawn(native, "beast");
            Assert.AreEqual(VisualSourceKind.TemporaryAlpha, adapted.Visual.Source);
            Assert.AreEqual(1, adapted.Disposition);
            Assert.AreEqual("spawn.1:2:3", adapted.Id);
            CollectionAssert.IsEmpty(adapted.Validate(), string.Join("; ", adapted.Validate()));

            var invalid = new EntityDefinition { Id = string.Empty };
            Assert.IsNotEmpty(invalid.Validate());
        }

        // ---------------------------------------------------------------- G2-H / G2-I

        [Test]
        public void PerformanceRecorderProducesReproducibleNumbers()
        {
            var recorder = new WorldPerformanceRecorder();
            for (int i = 0; i < 100; i++)
            {
                recorder.RecordFrame(i * 0.01, 0.01, 1024);
            }

            recorder.RecordZoneGenerated(1.0, 250.0);
            recorder.RecordZoneGenerated(1.5, 150.0);
            recorder.RecordChunkBuilt(1.2, 4.0, 9000, 12);
            recorder.RecordChunkBuilt(1.3, 6.0, 9000, 8);
            recorder.RecordCounts(180000, 20, 4, 2, 30, 3, 7, 2);
            recorder.RecordMemory(123456789);

            WorldPerformanceSnapshot snapshot = recorder.Snapshot();
            Assert.AreEqual(100, snapshot.FrameSamples);
            Assert.AreEqual(10.0, snapshot.MeanFrameMs, 0.001);
            Assert.AreEqual(10.0, snapshot.P95FrameMs, 0.001);
            Assert.AreEqual(200.0, snapshot.MeanZoneGenerationMs, 0.001);
            Assert.AreEqual(5.0, snapshot.MeanChunkBuildMs, 0.001);
            Assert.AreEqual(6.0, snapshot.MaxChunkBuildMs, 0.001);
            Assert.AreEqual(180000, snapshot.Vertices);
            Assert.AreEqual(20, snapshot.ActiveChunks);
            Assert.AreEqual(2, snapshot.ChunksGenerated);
            Assert.AreEqual(30, snapshot.VegetationCount);
            Assert.Greater(snapshot.ChunksPerSecond, 0.0);
            StringAssert.Contains("\"activeChunks\":20", snapshot.ToJson());
            StringAssert.Contains("\"workingSetBytes\":123456789", snapshot.ToJson());
            Assert.AreEqual(snapshot.ToJson(), recorder.Snapshot().ToJson());
        }

        [Test]
        public void DebugSnapshotShowsWorldFacts()
        {
            BiomeCatalog biomes = BiomeCatalog.Default();
            BiomeDefinition biome = biomes.Resolve(Grassland);
            WorldStreamingBudget budget = WorldStreamingBudget.Default();
            var request = new WorldDebugRequest
            {
                Seed = 666666u,
                GeneratorId = AlphaTerrainGenerator.WorldRuntimeId,
                GenerationFingerprint = WorldGenerationIdentity.Fingerprint(
                    AlphaTerrainGenerator.WorldRuntimeId, 666666u),
                WorldX = WorldLayout.StartVoxel + 0.5,
                WorldY = 45,
                WorldZ = WorldLayout.StartVoxel + 0.5,
                Climate = Grassland,
                BiomeId = biome.Id,
                BiomeName = biome.DisplayName,
                TerrainHeight = 44,
                WaterLevel = 0,
                LoadedChunks = 17,
                PendingChunks = 3,
                GeneratedZones = 9,
                CachedZones = 9,
                Vertices = 153000,
                Plants = 240,
                ActiveNpc = 6,
                Structures = 0,
                Budget = budget,
                Measurement = new WorldBudgetMeasurement(17, 20, 153000, 240, 6, 240, 9, 9),
                Distance = WorldDistanceStrategy.Default(),
                Performance = new WorldPerformanceRecorder().Snapshot(),
                StructureCandidates = new List<StructurePlacement>(),
                NearbySpawns = new List<string>(),
                Problems = new List<string>(),
            };
            WorldDebugSnapshot snapshot = WorldDebugSnapshot.Build(request);
            Assert.AreEqual(666666u, snapshot.Seed);
            Assert.AreEqual(biome.Id, snapshot.BiomeId);
            Assert.AreEqual(44, snapshot.TerrainHeight);
            Assert.AreEqual(17, snapshot.LoadedChunks);
            Assert.GreaterOrEqual(snapshot.Lines.Count, 7);
            StringAssert.Contains("seed 666666", snapshot.Text);
            StringAssert.Contains("chunk", snapshot.Text);
            StringAssert.Contains("zone", snapshot.Text);
            StringAssert.Contains("biome", snapshot.Text);
            Assert.AreEqual(WorldBudgetStatus.WithinBudget, snapshot.BudgetStatus);
            Assert.Greater(snapshot.Summary().Length, 20);

            WorldDebugSnapshot unavailable = WorldDebugSnapshot.Unavailable("NO_WORLD");
            StringAssert.Contains("no active world", unavailable.Text);
        }

        [Test]
        public void FoundationValidationIsClean()
        {
            List<string> problems = WorldFoundationValidation.Check();
            CollectionAssert.IsEmpty(problems, string.Join("; ", problems));
            StringAssert.Contains("biomes=", WorldFoundationValidation.Describe());
        }
    }
}
