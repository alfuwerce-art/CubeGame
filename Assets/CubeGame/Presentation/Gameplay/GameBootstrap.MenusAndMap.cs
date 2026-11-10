using System;
using System.Collections.Generic;
using System.Globalization;
using CubeGame.Core.Characters;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Presentation.Visual;
using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    public sealed partial class GameBootstrap
    {
        private const int LocalMapZoneSpan = 3;
        private Texture2D _localMap;
        private Vector2Int _localMapOrigin;
        private ZoneKey _mapCenter;
        private int _mapDiscoveryCount;
        private readonly Dictionary<StableEntityId, Label> _mapHunts = new Dictionary<StableEntityId, Label>();

        private void BuildMainMenu()
        {
            var panel = Surface("main-menu", "main-menu");
            _interface.Add(panel);
            Text(panel, "CubeGame", null, "heading");
            _statusLabel = Text(panel, "", "menu-status", "muted");
            var body = new ScrollView();
            panel.Add(body);
            Text(body, "New world", null, "section");
            AddField(body, "World name", "world-name", _formWorldName, value => _formWorldName = value);
            AddField(body, "Seed", "world-seed", _formSeed, value => _formSeed = value);
            AddChoice(body, "Slot", "world-slot", SlotKeys, _formSlot, index => _formSlot = index);

            Text(body, "Create hero", null, "section");
            var name = AddField(body, "Hero name", "hero-name", _formHeroName, value => _formHeroName = value);
            name.maxLength = AlphaHeroCreation.MaxNameLength;
            AddChoice(body, "Class", "hero-class", Enum.GetNames(typeof(CharacterClass)), _formClass, index => _formClass = index);
            var look = new VisualElement();
            body.Add(look);
            BuildAppearance(look);
            ButtonOf(body, "Create world", "create-world", () =>
            {
                uint seed = uint.TryParse(_formSeed, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed)
                    ? parsed : DefaultSeed;
                StartNewWorld(SlotKeys[_formSlot], _formWorldName, seed, _formHeroName, CharacterClasses.FromId(_formClass), _formLook);
            });
            Text(body, "Saved worlds", null, "section");
            var loads = Row(body);
            foreach (string slot in SlotKeys)
            {
                bool exists = _slots.Exists(slot);
                ButtonOf(loads, exists ? $"Load {slot}" : $"{slot} empty", $"load-{slot}", () => LoadWorld(slot)).SetEnabled(exists);
            }
            ButtonOf(body, "Quit", "quit", QuitGame);
        }

        private void BuildAppearance(VisualElement body)
        {
            HeroAppearance a = _formLook;
            KindId kind = AlphaHeroCreation.KindOf(a.Race, a.Gender);
            var appearance = Row(body, "form-row");
            DropdownField face = null;
            DropdownField hair = null;
            AddChoice(appearance, "Race", "hero-race", AlphaHeroCreation.Races, a.Race,
                index => SetAppearance(index, _formLook.Gender, _formLook.Face, _formLook.Haircut));
            AddChoice(appearance, "Sex", "hero-sex", new[] { "Male", "Female" }, a.Gender,
                index => SetAppearance(_formLook.Race, index, _formLook.Face, _formLook.Haircut));
            var style = Row(body, "form-row");
            face = AddChoice(style, "Face", "hero-face", NumberedChoices(AlphaHeroCreation.FaceCount(kind)), a.Face,
                index => SetAppearance(_formLook.Race, _formLook.Gender, index, _formLook.Haircut));
            hair = AddChoice(style, "Hair", "hero-hair", NumberedChoices(AlphaHeroCreation.HaircutCount(kind)), a.Haircut,
                index => SetAppearance(_formLook.Race, _formLook.Gender, _formLook.Face, index));

            var swatches = new List<Button>();
            for (int row = 0; row < AlphaHeroCreation.PaletteRows; row++)
            {
                var palette = Row(body, "palette");
                for (int col = 0; col < AlphaHeroCreation.PaletteColumns; col++)
                {
                    var color = AlphaHeroCreation.PaletteColor(col, row);
                    var swatch = ButtonOf(palette, "", $"hair-color-{col}-{row}", () =>
                    {
                        HeroAppearance current = _formLook;
                        _formLook = new HeroAppearance(current.Race, current.Gender, current.Face, current.Haircut, color.R, color.G, color.B);
                        RefreshPalette();
                        _audio.Play("menu-select.wav");
                    });
                    swatch.tooltip = $"RGB {color.R}, {color.G}, {color.B}";
                    swatch.style.backgroundColor = (Color)new Color32(color.R, color.G, color.B, 255);
                    swatch.AddToClassList("swatch");
                    swatches.Add(swatch);
                }
            }
            RefreshPalette();

            void SetAppearance(int raceIndex, int sex, int faceIndex, int haircut)
            {
                HeroAppearance current = _formLook;
                _formLook = AlphaHeroCreation.Normalize(new HeroAppearance(raceIndex, sex, faceIndex, haircut,
                    current.HairR, current.HairG, current.HairB));
                KindId newKind = AlphaHeroCreation.KindOf(_formLook.Race, _formLook.Gender);
                face.choices = NumberedChoices(AlphaHeroCreation.FaceCount(newKind));
                face.SetValueWithoutNotify(face.choices[_formLook.Face]);
                hair.choices = NumberedChoices(AlphaHeroCreation.HaircutCount(newKind));
                hair.SetValueWithoutNotify(hair.choices[_formLook.Haircut]);
            }

            void RefreshPalette()
            {
                for (int i = 0; i < swatches.Count; i++)
                {
                    var color = AlphaHeroCreation.PaletteColor(i % AlphaHeroCreation.PaletteColumns, i / AlphaHeroCreation.PaletteColumns);
                    swatches[i].EnableInClassList("selected-swatch",
                        color.R == _formLook.HairR && color.G == _formLook.HairG && color.B == _formLook.HairB);
                }
            }
        }

        private static List<string> NumberedChoices(int count)
        {
            var choices = new List<string>(count);
            for (int i = 0; i < count; i++) choices.Add((i + 1).ToString(CultureInfo.InvariantCulture));
            return choices;
        }

        private DropdownField AddChoice(VisualElement parent, string label, string name, IEnumerable<string> values,
            int index, Action<int> changed)
        {
            var field = new DropdownField(label, new List<string>(values), index) { name = name };
            field.RegisterValueChangedCallback(_ =>
            {
                changed(field.index);
                _audio.Play("menu-select.wav");
            });
            parent.Add(field);
            return field;
        }

        private static TextField AddField(VisualElement parent, string label, string name, string value, Action<string> changed)
        {
            var field = new TextField(label) { name = name, value = value };
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            parent.Add(field);
            return field;
        }

        private void BuildPauseMenu()
        {
            var panel = Surface("pause-menu", "pause-menu");
            _interface.Add(panel);
            Text(panel, $"Paused  ·  {_session.World.Name}", null, "heading");
            ButtonOf(panel, "Resume", "resume", TogglePause);
            ButtonOf(panel, "Save", "save", () => SaveCheckpoint());
            ButtonOf(panel, "Save and exit to menu", "save-exit", () => ExitToMenu(true));
            ButtonOf(panel, "Exit to menu", "exit", () => ExitToMenu(false));
            ButtonOf(panel, "Quit", "quit", QuitGame);
            _statusLabel = Text(panel, "", "pause-status", "muted");
        }

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
            foreach (HuntMission hunt in _session.Hero.Missions.Hunts)
            {
                if (!hunt.Complete)
                {
                    _mapHunts.Add(hunt.Target, Text(image, "!", $"map-hunt-{hunt.Target.Value}", "map-marker"));
                }
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
            foreach (var pair in _mapHunts)
            {
                if (_session.Combat.TryGet(pair.Key, out Combatant target) && OnMap(target.Position))
                {
                    WorldPosition position = target.Position;
                    ZoneKey zone = WorldLayout.ZoneOf((int)Math.Floor(position.X), (int)Math.Floor(position.Z));
                    if (zone.Equals(_mapCenter) || _session.WorldDeltas.IsDiscovered(zone))
                    {
                        pair.Value.style.display = DisplayStyle.Flex;
                        PositionMarker(pair.Value, position.X, position.Z);
                        continue;
                    }
                }
                pair.Value.style.display = DisplayStyle.None;
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
