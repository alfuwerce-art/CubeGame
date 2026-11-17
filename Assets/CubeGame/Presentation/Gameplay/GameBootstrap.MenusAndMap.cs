using System;
using System.Collections.Generic;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Presentation.Visual;
using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Local world map (Cube.exe toggles the map flag with M, 0x47E1B0 case 0x4D; the map renderer is a separate
    /// presentation system owned by M0 P6). Only the input/state routing is part of P0.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private const int LocalMapZoneSpan = 3;
        private Texture2D _localMap;
        private Vector2Int _localMapOrigin;
        private ZoneKey _mapCenter;
        private int _mapDiscoveryCount;
        // Mission markers (0x5FEEA2): label and the voxel point it marks.
        private readonly List<(Label Marker, double X, double Z)> _mapHunts = new List<(Label, double, double)>();

        private ZoneKey CurrentMapZone()
        {
            WorldPosition position = _session.Hero.Position;
            return WorldLayout.ZoneOf((int)Math.Floor(position.X), (int)Math.Floor(position.Z));
        }

        private void BuildMap(VisualElement panel)
        {
            ReleaseMap();
            _mapCenter = CurrentMapZone();
            _mapDiscoveryCount = _session.WorldDeltas.Discovered.Count;
            int halfSpan = LocalMapZoneSpan / 2;
            _localMapOrigin = new Vector2Int((_mapCenter.X - halfSpan) * WorldLayout.ZoneSize, (_mapCenter.Z - halfSpan) * WorldLayout.ZoneSize);
            int size = LocalMapZoneSpan * MapTileBuilder.DefaultTileSize;
            int step = WorldLayout.ZoneSize / MapTileBuilder.DefaultTileSize;
            byte[] pixels = MapTileBuilder.Render(size, (px, pz) =>
            {
                int x = _localMapOrigin.x + px * step + step / 2;
                int z = _localMapOrigin.y + pz * step + step / 2;
                ZoneKey zone = WorldLayout.ZoneOf(x, z);
                if (!zone.Equals(_mapCenter) && !_session.WorldDeltas.IsDiscovered(zone))
                {
                    return default;
                }

                VoxelColumn column = _worldState.EffectiveColumn(x, z);
                return column != null ? MapTileBuilder.FromColumn(column) : default;
            });
            _localMap = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Local world map", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            _localMap.LoadRawTextureData(pixels);
            _localMap.Apply(false, true);

            var image = new Image { name = "map-image", image = _localMap, scaleMode = ScaleMode.StretchToFill };
            image.AddToClassList("map-image");
            panel.Add(image);
            Text(panel, "● You   T Town   D Dungeon   ! Active hunt", "map-legend", "muted");

            // CUBEGAME_CONTRACT: these markers describe existing world records and streamed targets.
            for (int dz = 0; dz < LocalMapZoneSpan; dz++)
            {
                for (int dx = 0; dx < LocalMapZoneSpan; dx++)
                {
                    var zone = new ZoneKey(_mapCenter.X - halfSpan + dx, _mapCenter.Z - halfSpan + dz);
                    if (!zone.Equals(_mapCenter) && !_session.WorldDeltas.IsDiscovered(zone)) continue;
                    int type = ((AlphaTerrainGenerator)_worldState.Generator).RegionRecord(zone.X >> 6, zone.Z >> 6)
                        .Zone(zone.X & 63, zone.Z & 63).Type;
                    string glyph = type == AlphaZoneRecord.Town ? "T"
                        : type == AlphaZoneRecord.Type3 || type == AlphaZoneRecord.Type5 ? "D" : null;
                    if (glyph == null) continue;
                    Label marker = Text(image, glyph, $"map-zone-{zone.X}-{zone.Z}", "map-marker");
                    marker.AddToClassList("map-landmark");
                    PositionMarker(marker, zone.X * (double)WorldLayout.ZoneSize + WorldLayout.ZoneSize / 2,
                        zone.Z * (double)WorldLayout.ZoneSize + WorldLayout.ZoneSize / 2);
                }
            }
            // Alpha 0x5FEEA2: a mission that is not complete is drawn at its target zone (type 1) or at the
            // landmark centre of its column (other types).
            var terrain = (AlphaTerrainGenerator)_worldState.Generator;
            foreach (AlphaMission mission in _session.Hero.Missions.Missions)
            {
                if (mission.State == AlphaMissionState.Complete) continue;
                double x;
                double z;
                if (mission.Type == AlphaMissionType.Monster)
                {
                    x = mission.TargetZoneX * (double)WorldLayout.ZoneSize + 0x80;
                    z = mission.TargetZoneZ * (double)WorldLayout.ZoneSize + 0x80;
                }
                else
                {
                    LandmarkColumn column = terrain.RegionRecord(mission.RegionX, mission.RegionZ)
                        ?.Column(mission.ColumnX & 7, mission.ColumnZ & 7);
                    if (column == null) continue;
                    x = column.X;
                    z = column.Z;
                }
                Label label = Text(image, "!", $"map-hunt-{mission.ColumnX}-{mission.ColumnZ}", "map-marker");
                _mapHunts.Add((label, x, z));
            }
            _mapMarker = Text(image, "●", "map-hero", "map-marker");
            _mapMarker.AddToClassList("map-player");
        }

        private bool OnMap(WorldPosition position)
        {
            int edge = LocalMapZoneSpan * WorldLayout.ZoneSize;
            return position.X >= _localMapOrigin.x && position.X < (double)_localMapOrigin.x + edge
                && position.Z >= _localMapOrigin.y && position.Z < (double)_localMapOrigin.y + edge;
        }

        private void RefreshMapMarker()
        {
            if (!_mapCenter.Equals(CurrentMapZone()) || _mapDiscoveryCount != _session.WorldDeltas.Discovered.Count)
            {
                InvalidatePanel();
            }
            WorldPosition p = _session.Hero.Position;
            _mapMarker.style.display = OnMap(p) ? DisplayStyle.Flex : DisplayStyle.None;
            PositionMarker(_mapMarker, p.X, p.Z);
            foreach ((Label marker, double x, double z) in _mapHunts)
            {
                var position = new WorldPosition(x, 0, z);
                if (OnMap(position))
                {
                    ZoneKey zone = WorldLayout.ZoneOf((int)Math.Floor(x), (int)Math.Floor(z));
                    if (zone.Equals(_mapCenter) || _session.WorldDeltas.IsDiscovered(zone))
                    {
                        marker.style.display = DisplayStyle.Flex;
                        PositionMarker(marker, x, z);
                        continue;
                    }
                }
                marker.style.display = DisplayStyle.None;
            }
        }

        private void PositionMarker(VisualElement marker, double x, double z)
        {
            double edge = LocalMapZoneSpan * WorldLayout.ZoneSize;
            marker.style.left = Length.Percent((float)(100 * (x - _localMapOrigin.x) / edge));
            marker.style.top = Length.Percent((float)(100 * (1 - (z - _localMapOrigin.y) / edge)));
        }

        private void ReleaseMap()
        {
            if (_localMap != null) Destroy(_localMap);
            _localMap = null;
            _mapMarker = null;
            _mapHunts.Clear();
        }
    }
}
