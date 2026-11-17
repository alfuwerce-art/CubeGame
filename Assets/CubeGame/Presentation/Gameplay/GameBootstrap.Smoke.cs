// CubeGame.Gameplay — G2 smoke automation.
//
// Two dev-only automation modes, both used by the Player smoke of the stages:
//   -cubeSmokeSeconds <n>            run the world for n seconds, write metrics, save and quit (G2-I);
//   -cubeSmokeContent <fresh|verify> exercise the CubeGame content pipeline (G2-1): vegetation, settlement anchors,
//                                    dungeon enter/exit, destruction of a structure and a plant, save; the verify
//                                    run checks that the removals survived the save/load.
//
// Neither mode changes gameplay rules; they only drive the same runtime methods a player action would.
using System;
using System.Collections;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Features.World.Foundation;
using CubeGame.Gameplay.Streaming;
using UnityEngine;

namespace CubeGame.Gameplay
{
    public sealed partial class GameBootstrap
    {
        private float _smokeSeconds;
        private float _smokeElapsed;
        private bool _smokeDone;

        private string _contentSmokeMode = string.Empty;
        private bool _contentSmokeStarted;
        private bool _contentSmokeFinished;
        private WorldDebugSnapshot _contentSmokeSnapshot;

        /// <summary>Seconds to play before saving and quitting; 0 = disabled (normal interactive run).</summary>
        public float SmokeSeconds => _smokeSeconds;

        /// <summary>Snapshot of the last content smoke step (tests/dev only).</summary>
        public WorldDebugSnapshot ContentSmokeSnapshot => _contentSmokeSnapshot;

        private void UpdateSmoke(float deltaSeconds)
        {
            MaybeStartContentSmoke();
            if (_smokeSeconds <= 0f || _smokeDone) return;
            if (_session == null || _session.State != SessionState.InGame || _worldView == null) return;

            _smokeElapsed += Mathf.Max(0f, deltaSeconds);
            if (_smokeElapsed < _smokeSeconds) return;

            _smokeDone = true;
            WorldDebugSnapshot snapshot = WorldFoundationSnapshot();
            WorldPerformanceSnapshot performance = _worldMetrics?.Snapshot();
            Debug.Log($"[Smoke] SMOKE_SECONDS={_smokeSeconds:0.#} elapsed={_smokeElapsed:0.#} state={_session.State} " +
                      $"world={snapshot.Summary()}");
            if (performance != null) Debug.Log($"[Smoke] metrics {performance.Describe()}");
            if (_worldMetricsProbe != null)
            {
                _worldMetricsProbe.Write();
                Debug.Log($"[Smoke] metrics file {_worldMetricsProbe.OutputPath}");
            }

            _saveCheckpoint = true;   // the checkpoint is written by QuitGame/Quit
            Debug.Log("[Smoke] quitting");
            QuitGame();
        }

        private void MaybeStartContentSmoke()
        {
            if (string.IsNullOrEmpty(_contentSmokeMode) || _contentSmokeStarted || _contentSmokeFinished) return;
            if (_session == null || _session.State != SessionState.InGame || _worldView == null) return;
            _contentSmokeStarted = true;
            StartCoroutine(RunContentSmoke(_contentSmokeMode));
        }

        private IEnumerator RunContentSmoke(string mode)
        {
            // Give the world a few frames: the hero must be on the ground, the population must have its zones.
            int settle = 0;
            while (settle++ < 120 && (_player == null || _session.State != SessionState.InGame))
            {
                yield return null;
            }

            if (_contentRuntime == null || !_contentRuntime.IsCubeGameWorld)
            {
                Debug.Log("[ContentSmoke] FAIL content runtime missing or the world is not a CubeGame content world");
                _contentSmokeFinished = true;
                yield return QuitAfterSmoke();
                yield break;
            }

            if (string.Equals(mode, "verify", System.StringComparison.Ordinal))
            {
                yield return RunContentSmokeVerify();
                yield break;
            }

            TeleportHeroToStart();
            yield return null;
            _worldView.LoadAround(_player.transform.position, 2);
            yield return null;

            Vector3 scene = _player.transform.position;
            _worldView.ToWorld(scene, out double worldX, out _, out double worldZ);
            ZoneKey zone = WorldLayout.ZoneOf((int)Math.Floor(worldX), (int)Math.Floor(worldZ));
            AlphaTerrainGenerator terrain = _worldState.Generator as AlphaTerrainGenerator;

            // 1. Vegetation is CubeGame content with stable identity.
            AlphaZone content = terrain != null ? terrain.Zone(zone.X, zone.Z) : null;
            int plants = content?.Vegetation.Count ?? 0;
            string firstPlant = content != null && content.Vegetation.Count > 0
                ? content.Vegetation[0].PersistentId
                : "(none)";
            Debug.Log($"[ContentSmoke] VEGETATION={plants} zone=({zone.X},{zone.Z}) " +
                      $"biome={content?.BiomeId} rules={content?.VegetationRuleSetId} first={firstPlant} " +
                      $"candidates={content?.VegetationCandidates}");

            // 2. Settlement prototype + its anchors (spawned through the entity registry / placeholder binding).
            SettlementBlueprint settlement = null;
            ZoneKey settlementZone = default;
            bool settlementFound = _contentRuntime.TryFindSettlement(worldX, worldZ, 2, out settlement,
                out settlementZone);
            int anchors = 0;
            if (settlementFound)
            {
                TeleportHero(settlement.CenterX, settlement.CenterZ);
                _worldView.LoadAround(_player.transform.position, 2);
                for (int i = 0; i < 20; i++) yield return null;
                anchors = CountAnchors();
                Debug.Log($"[ContentSmoke] SETTLEMENT={settlement.PersistentId} slots={settlement.Slots.Count} " +
                          $"anchors={anchors} biome={settlement.BiomeId} level={settlement.Level}");
            }
            else
            {
                Debug.Log("[ContentSmoke] SETTLEMENT=(none within 2 regions)");
            }

            // 3. Dungeon prototype: entrance -> room -> entrance.
            bool entered = false;
            bool exited = false;
            string dungeonId = string.Empty;
            if (_contentRuntime.TryFindDungeon(worldX, worldZ, 2, out DungeonPrototype dungeon, out ZoneKey dungeonZone))
            {
                dungeonId = dungeon.InstanceId;
                if (_contentRuntime.EnterDungeon(dungeon, out double ex, out double ey, out double ez))
                {
                    TeleportHeroExact(ex, ey, ez);
                    for (int i = 0; i < 5; i++) yield return null;
                    entered = dungeon.Contains(_session.Hero.Position.X, _session.Hero.Position.Y,
                        _session.Hero.Position.Z);
                }

                if (_contentRuntime.ExitDungeon(dungeon, out double rx, out double ry, out double rz))
                {
                    TeleportHeroExact(rx, ry, rz);
                    for (int i = 0; i < 5; i++) yield return null;
                    double dx = _session.Hero.Position.X - (dungeon.ReturnX + 0.5);
                    double dz = _session.Hero.Position.Z - (dungeon.ReturnZ + 0.5);
                    exited = dx * dx + dz * dz < 16.0;
                }

                Debug.Log($"[ContentSmoke] DUNGEON={dungeonId} zone=({dungeonZone.X},{dungeonZone.Z}) " +
                          $"ENTER={(entered ? "PASS" : "FAIL")} EXIT={(exited ? "PASS" : "FAIL")} " +
                          $"room=({dungeon.MinX}…{dungeon.MaxX},{dungeon.FloorY})");
            }
            else
            {
                Debug.Log("[ContentSmoke] DUNGEON=(none within 2 zones)");
            }

            // 4. Destroy one structure and one plant; both must be gone after reload.
            string destroyedStructure = "(none)";
            int structureZoneX = 0;
            int structureZoneZ = 0;
            if (_contentRuntime.TryNearestStructure(_session.Hero.Position.X, _session.Hero.Position.Z, 2,
                    out ContentStructureState structure))
            {
                destroyedStructure = structure.PersistentId;
                structureZoneX = structure.ZoneX;
                structureZoneZ = structure.ZoneZ;
                bool destroyed = _contentRuntime.DestroyStructure(structure, _session.Clock.TimeMs);
                Debug.Log($"[ContentSmoke] DESTROY={(destroyed ? "PASS" : "FAIL")} {structure.Kind} " +
                          $"{structure.PersistentId} at ({structure.X},{structure.Z})");
            }
            else
            {
                Debug.Log("[ContentSmoke] DESTROY=(no structure within 2 zones)");
            }

            string removedPlant = "(none)";
            ZoneKey removalZone = zone;
            if (_contentRuntime.RemoveVegetationNear(_session.Hero.Position.X, _session.Hero.Position.Z, 64,
                    _session.Clock.TimeMs, out VegetationPlacement plant))
            {
                removedPlant = plant.PersistentId;
                removalZone = WorldLayout.ZoneOf(plant.X, plant.Z);
                Debug.Log($"[ContentSmoke] VEG_REMOVE=PASS {plant.Kind} {plant.PersistentId}");
            }
            else
            {
                // Fallback for a sparse biome: remove the first plant of the zone the hero stands in, so the verify
                // run (which scans around the loaded position) can find the removal.
                ZoneKey heroZone = WorldLayout.ZoneOf((int)Math.Floor(_session.Hero.Position.X),
                    (int)Math.Floor(_session.Hero.Position.Z));
                AlphaZone heroContent = terrain?.Zone(heroZone.X, heroZone.Z);
                if (heroContent != null && heroContent.Vegetation.Count > 0)
                {
                    plant = heroContent.Vegetation[0];
                    if (_contentRuntime.RemoveVegetation(heroZone, plant, _session.Clock.TimeMs))
                    {
                        removedPlant = plant.PersistentId;
                        removalZone = heroZone;
                        Debug.Log($"[ContentSmoke] VEG_REMOVE=PASS (zone plant) {plant.Kind} {plant.PersistentId}");
                    }
                }
            }

            if (string.Equals(removedPlant, "(none)", System.StringComparison.Ordinal))
            {
                Debug.Log("[ContentSmoke] VEG_REMOVE=(no plant within 64 voxels)");
            }

            Debug.Log($"[ContentSmoke] REMOVED_AT zone=({removalZone.X},{removalZone.Z}) " +
                      $"structure_zone=({structureZoneX},{structureZoneZ})");

            _contentSmokeSnapshot = WorldFoundationSnapshot();
            bool saved = SaveCheckpoint();
            Debug.Log($"[ContentSmoke] SUMMARY settlement={settlementFound} anchors={anchors} plants={plants} " +
                      $"dungeon_enter={entered} dungeon_exit={exited} destroyed={destroyedStructure} " +
                      $"plant_removed={removedPlant} save={(saved ? "PASS" : "FAIL " + Status)}");
            _contentSmokeFinished = true;
            yield return QuitAfterSmoke();
        }

        private IEnumerator RunContentSmokeVerify()
        {
            yield return null;
            Vector3 scene = _player.transform.position;
            _worldView.ToWorld(scene, out double worldX, out _, out double worldZ);
            ZoneKey centre = WorldLayout.ZoneOf((int)Math.Floor(worldX), (int)Math.Floor(worldZ));

            int removedStructures = 0;
            int removedPlants = 0;
            int structures = 0;
            int plants = 0;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    var zone = new ZoneKey(centre.X + dx, centre.Z + dz);
                    if (!WorldLayout.IsInside(zone)) continue;
                    removedStructures += _contentRuntime.RemovedStructuresIn(zone);
                    removedPlants += _contentRuntime.RemovedVegetationIn(zone);
                    List<ContentStructureState> states = _contentRuntime.StructuresOfZone(zone, true);
                    structures += states.Count;
                    if (_worldState.Generator is AlphaTerrainGenerator terrain &&
                        terrain.TryGetGeneratedZone(zone.X, zone.Z, out AlphaZone content))
                    {
                        plants += content.Vegetation.Count;
                    }
                }
            }

            Debug.Log($"[ContentSmoke] VERIFY removed_structures={removedStructures} removed_plants={removedPlants} " +
                      $"structures={structures} plants={plants} " +
                      $"result={(removedStructures > 0 && removedPlants > 0 ? "PASS" : "FAIL")}");
            _contentSmokeSnapshot = WorldFoundationSnapshot();
            _contentSmokeFinished = true;
            yield return QuitAfterSmoke();
        }

        private IEnumerator QuitAfterSmoke()
        {
            yield return null;
            Debug.Log("[ContentSmoke] quitting");
            QuitGame();
        }

        private int CountAnchors()
        {
            int anchors = 0;
            if (_population == null) return anchors;
            foreach (LiveSpawn live in _population.Live)
            {
                if (CubeGameContentIds.IsAnchor(live.Definition.PersistentObjectId)) anchors++;
            }

            return anchors;
        }

        private void TeleportHeroToStart()
        {
            WorldPosition start = WorldState.StartPosition(_worldState.Generator, _session.WorldDeltas);
            TeleportHero((int)start.X, (int)start.Z);
        }

        /// <summary>Places the hero at an exact world position (dungeon room / entrance), with a fresh origin.</summary>
        public void TeleportHeroToPosition(double x, double y, double z) => TeleportHeroExact(x, y, z);

        /// <summary>Places the hero at an exact world position (dungeon room / entrance), with a fresh origin.</summary>
        private void TeleportHeroExact(double x, double y, double z)
        {
            if (_worldView == null || _player == null) return;
            Vector2Int origin = StreamingWorldView.OriginFor(x, z);
            Vector3 shift = _worldView.Rebase(origin);
            if (_camera != null) _camera.transform.position += shift;
            _player.transform.position = _worldView.ToScene(x, y, z);
            Physics.SyncTransforms();
            _lastHeroScenePosition = _player.transform.position;
            _worldView.LoadAround(_player.transform.position, 1);
            SyncHeroFromView();
        }
    }
}
