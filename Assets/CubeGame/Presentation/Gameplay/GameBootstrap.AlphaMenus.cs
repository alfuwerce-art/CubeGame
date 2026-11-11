using System;
using System.Collections.Generic;
using System.Globalization;
using CubeGame.Core.Characters;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Alpha start-screen state machine. Alpha has no "lobby": it shows one start menu and switches screen nodes by
    /// visibility flags, keeping the selected character and world as separate indices:
    ///
    ///   Start (start.plx, StartMenuWidget 0x583320)             Start Game / Options / Exit
    ///     → Characters (0x800888, GameController_buildCharacterList 0x49D650)
    ///         → Editor (0x800880, CharCreation 0x42BD90 / 0x42C080, confirm 0x4821A0)
    ///         → Worlds (0x80088C, GameController_buildWorldList 0x4A23D0)
    ///             → CreateWorld (0x800890, confirm 0x482530)
    ///             → world load request (0x484170 → 0x46F620 → world worker 0x46A8A0)
    ///   Back (0x4814F0): CreateWorld→Worlds, Worlds→Characters, Editor→Characters, Characters→Start.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private enum StartupScreen
        {
            Start = 0,
            Options = 1,
            Characters = 2,
            Editor = 3,
            Worlds = 4,
            CreateWorld = 5,
        }

        /// <summary>In-game overlay menus of Alpha: the Esc system menu (SystemWidget 0x587710) and Options (0x800A00).</summary>
        private enum GameOverlay
        {
            None = 0,
            System = 1,
            Options = 2,
        }

        private AlphaProfileStore _profiles;
        private StartupScreen _startupScreen = StartupScreen.Start;
        private GameOverlay _overlay;
        private GameOverlay _drawnOverlay;
        private AlphaCharacterRecord _selectedCharacter;
        private AlphaWorldRecord _selectedWorld;
        private HeroState _pendingHero;
        private SessionSaveData _pendingSession;
        private SessionSaveData _runningSession;
        private bool _profileFlow;
        private bool _redraw;
        private bool _enteringWorld;
        private PendingWorldEntry _pendingEntry;

        private sealed class PendingWorldEntry
        {
            public AlphaWorldRecord World;
            public WorldInfo WorldInfo;
            public HeroState Hero;
            public AlphaWorldLoad Load;
            public string Key;
            public bool AtSavedPosition;
        }

        // Editor draft (Alpha CharCreation_reset_defaults 0x42BD90: race 0, sex 0, class 0, face 0, haircut 0, FF C8 32).
        private string _formHeroName = string.Empty;
        private int _formClass;
        private HeroAppearance _formLook = AlphaHeroCreation.Default;
        private string _formWorldName = string.Empty;
        private string _formSeed = string.Empty;

        public AlphaProfileStore Profiles => _profiles;

        public bool ProfileFlow => _profileFlow;

        private AlphaProfileStore CreateProfiles(string directory) =>
            new AlphaProfileStore(new FileSaveStore(directory), new JsonSaveSerializer(), new JsonSessionSerializer(),
                new JsonWorldSerializer());

        private void ReloadProfiles()
        {
            if (_profiles == null) return;
            bool ok = _profiles.Reload();
            Debug.Log($"[Game] alpha profile store {(ok ? "loaded" : "FAILED")} {_profiles.LastReport} " +
                      $"characters={_profiles.Characters.Count} worlds={_profiles.Worlds.Count}");
        }

        private void ShowStartupScreen(StartupScreen screen)
        {
            _startupScreen = screen;
            _status = string.Empty;
            RequestRedraw();
        }

        /// <summary>Rebuilds the visible screen on the next LateUpdate without touching the state machine.</summary>
        private void RequestRedraw() => _redraw = true;

        // ----- Start menu (StartMenuWidget 0x583320) -----

        private void BuildStartupScreen()
        {
            if (_enteringWorld)
            {
                // Alpha shows the start.plx "Please wait..." node while the world is opened.
                var wait = Surface("wait-screen", "main-menu");
                Text(wait, "Please wait...", "wait-label", "heading");
                _statusLabel = Text(wait, _status, "wait-status", "muted");
                _interface.Add(wait);
                return;
            }

            var backdrop = new VisualElement { name = "menu-backdrop", pickingMode = PickingMode.Ignore };
            backdrop.AddToClassList("menu-backdrop");
            _interface.Add(backdrop);
            var panel = Surface("main-menu", "main-menu");
            if (_startupScreen == StartupScreen.Start) panel.AddToClassList("start-screen");
            var body = new ScrollView { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            body.AddToClassList("menu-body");
            panel.Add(body);
            VisualElement footer = null;
            switch (_startupScreen)
            {
                case StartupScreen.Options:
                    panel.name = "options-screen";
                    footer = Row(panel, "menu-footer");
                    BuildOptionsScreen(body, footer, false);
                    break;
                case StartupScreen.Characters:
                    footer = Row(panel, "menu-footer");
                    BuildCharacterList(body, footer);
                    break;
                case StartupScreen.Editor:
                    footer = Row(panel, "menu-footer");
                    BuildCharacterEditor(body, footer);
                    break;
                case StartupScreen.Worlds:
                    footer = Row(panel, "menu-footer");
                    BuildWorldList(body, footer);
                    break;
                case StartupScreen.CreateWorld:
                    footer = Row(panel, "menu-footer");
                    BuildWorldCreation(body, footer);
                    break;
                default:
                    BuildStartMenu(body);
                    break;
            }

            if (footer != null && footer.childCount > 0) panel.Add(footer);
            _interface.Add(panel);
        }

        private void BuildStartMenu(VisualElement panel)
        {
            Text(panel, "CubeGame", "start-title", "heading");
            _statusLabel = Text(panel, "", "menu-status", "muted");
            // StartMenuWidget 0x583320 lists exactly three entries; item 0 opens the character list (0x47B600).
            ButtonOf(panel, "Start Game", "start-game", () => ShowStartupScreen(StartupScreen.Characters));
            ButtonOf(panel, "Options", "menu-options", () => ShowStartupScreen(StartupScreen.Options));
            ButtonOf(panel, "Exit", "menu-exit", QuitGame);
        }

        // ----- Character list (0x49D650, rows 230 px from y=50 + the extra "Create Character" row) -----

        private void BuildCharacterList(VisualElement panel, VisualElement footer)
        {
            Text(panel, "Characters", "screen-title", "heading");
            _statusLabel = Text(panel, "", "menu-status", "muted");
            foreach (AlphaCharacterRecord record in _profiles.Characters)
            {
                AlphaCharacterRecord selected = record;
                Button row = ButtonOf(panel, $"{record.Name}\n{record.Class}  ·  Level {record.Level}",
                    $"character-{record.Id}", () => SelectCharacter(selected.Id));
                row.AddToClassList("entry-row");
            }

            if (_profiles.Characters.Count == 0)
            {
                Text(panel, "No characters", "no-characters", "muted");
            }

            ButtonOf(footer, "Create Character", "new-character", BeginCharacterCreation);
            ButtonOf(footer, "Back", "menu-back", () => ShowStartupScreen(StartupScreen.Start));
        }

        /// <summary>Character row select: load the record and open the world list (0x483E70).</summary>
        public bool SelectCharacter(int characterId)
        {
            AlphaCharacterLoad load = _profiles.LoadCharacter(characterId);
            if (!load.IsSuccess)
            {
                return Report(false, $"character {characterId}: {load.Message}");
            }

            SelectCharacter(load);
            return true;
        }

        private void SelectCharacter(AlphaCharacterLoad load)
        {
            _selectedCharacter = load.Record;
            _pendingHero = load.Hero;
            _pendingSession = load.Session;
            Debug.Log($"[Game] character selected id={load.Record.Id} '{load.Record.Name}' class={load.Record.Class} " +
                      $"level={load.Record.Level} visits={load.Record.Visits.Count}");
            ShowStartupScreen(StartupScreen.Worlds);
        }

        private void BeginCharacterCreation()
        {
            _formHeroName = string.Empty;
            _formClass = 0;
            _formLook = AlphaHeroCreation.Default;
            ShowStartupScreen(StartupScreen.Editor);
        }

        // ----- Character editor (0x42BD90 defaults, 0x42C080 model refresh, 0x4821A0 confirm) -----

        private void BuildCharacterEditor(VisualElement panel, VisualElement footer)
        {
            Text(panel, "Create Character", "screen-title", "heading");
            _statusLabel = Text(panel, "", "menu-status", "muted");

            var name = AddField(panel, "Name", "hero-name", _formHeroName, value => _formHeroName = value);
            name.maxLength = AlphaHeroCreation.MaxNameLength;

            // Alpha previews the creature model while the editor is open (CharacterPreviewWidget 0x424E80).
            VisualElement preview = PreviewPanel(panel);
            CreatePreviewStage(preview, _formLook);

            AddCarousel(panel, "Class", "hero-class", Enum.GetNames(typeof(CharacterClass)), _formClass, index =>
            {
                _formClass = index;
                RefreshEditorPreview();
            });
            AddCarousel(panel, "Race", "hero-race", AlphaHeroCreation.Races, _formLook.Race,
                index => SetEditorAppearance(index, _formLook.Gender, _formLook.Face, _formLook.Haircut));
            AddCarousel(panel, "Sex", "hero-sex", new[] { "Male", "Female" }, _formLook.Gender,
                index => SetEditorAppearance(_formLook.Race, index, _formLook.Face, _formLook.Haircut));
            AddCarousel(panel, "Face", "hero-face", NumberedChoices(AlphaHeroCreation.FaceCount(AlphaHeroCreation.KindOf(_formLook))),
                _formLook.Face, index => SetEditorAppearance(_formLook.Race, _formLook.Gender, index, _formLook.Haircut));
            AddCarousel(panel, "Hair", "hero-hair", NumberedChoices(AlphaHeroCreation.HaircutCount(AlphaHeroCreation.KindOf(_formLook))),
                _formLook.Haircut, index => SetEditorAppearance(_formLook.Race, _formLook.Gender, _formLook.Face, index));

            // Alpha palette: 16 hues × 7 rows of shadings (drawCharacterAppearancePanel 0x428E40).
            var swatches = new List<Button>();
            for (int rowIndex = 0; rowIndex < AlphaHeroCreation.PaletteRows; rowIndex++)
            {
                var palette = Row(panel, "palette");
                for (int column = 0; column < AlphaHeroCreation.PaletteColumns; column++)
                {
                    (byte R, byte G, byte B) color = AlphaHeroCreation.PaletteColor(column, rowIndex);
                    Button swatch = ButtonOf(palette, string.Empty, $"hair-color-{column}-{rowIndex}", () =>
                    {
                        HeroAppearance current = _formLook;
                        _formLook = new HeroAppearance(current.Race, current.Gender, current.Face, current.Haircut,
                            color.R, color.G, color.B);
                        RefreshPalette();
                        RefreshEditorPreview();
                        _audio.Play("menu-select.wav");
                    });
                    swatch.tooltip = $"RGB {color.R}, {color.G}, {color.B}";
                    swatch.style.backgroundColor = (Color)new Color32(color.R, color.G, color.B, 255);
                    swatch.AddToClassList("swatch");
                    swatches.Add(swatch);
                }
            }

            RefreshPalette();

            ButtonOf(footer, "Create Character", "create-character", () => ConfirmCharacterEditor());
            ButtonOf(footer, "Back", "menu-back", () => ShowStartupScreen(StartupScreen.Characters));

            void RefreshPalette()
            {
                for (int i = 0; i < swatches.Count; i++)
                {
                    (byte R, byte G, byte B) color = AlphaHeroCreation.PaletteColor(i % AlphaHeroCreation.PaletteColumns,
                        i / AlphaHeroCreation.PaletteColumns);
                    swatches[i].EnableInClassList("selected-swatch",
                        color.R == _formLook.HairR && color.G == _formLook.HairG && color.B == _formLook.HairB);
                }
            }
        }

        /// <summary>Carousel step of the editor class field (CharCreation_refresh_model 0x42C080 cycling).</summary>
        public void CycleEditorClass(int delta)
        {
            int count = Enum.GetValues(typeof(CharacterClass)).Length;
            _formClass = Cycle(_formClass + delta, count);
            RefreshEditorPreview();
            RequestRedraw();
        }

        /// <summary>Carousel step of the editor race field; face/hair ranges follow the species.</summary>
        public void CycleEditorRace(int delta)
        {
            SetEditorAppearance(Cycle(_formLook.Race + delta, AlphaHeroCreation.RaceCount), _formLook.Gender,
                _formLook.Face, _formLook.Haircut);
        }

        /// <summary>Applies a race/sex/face/hair draft change and rebuilds the row ranges and the preview.</summary>
        public void SetEditorAppearance(int race, int sex, int face, int haircut)
        {
            HeroAppearance current = _formLook;
            _formLook = AlphaHeroCreation.Normalize(new HeroAppearance(race, sex, face, haircut,
                current.HairR, current.HairG, current.HairB));
            _formClass = Mathf.Clamp(_formClass, 0, Math.Max(0, Enum.GetValues(typeof(CharacterClass)).Length - 1));
            RequestRedraw();
            RefreshEditorPreview();
        }

        /// <summary>Editor confirm: create the hero (0x4772B0 starter kit) and save the record (0x4821A0).</summary>
        public bool ConfirmCharacterEditor()
        {
            string name = AlphaHeroCreation.NormalizeName(_formHeroName);
            var request = new NewWorldRequest
            {
                WorldName = string.Empty,
                Seed = 0,
                GeneratorId = GeneratorId,
                HeroName = name,
                Class = CharacterClasses.FromId(_formClass),
                Specialization = CharacterSpecialization.Primary,
                Appearance = AlphaHeroCreation.Normalize(_formLook),
            };

            HeroState hero = GameSession.CreateHero(request);
            long now = DateTime.UtcNow.Ticks;
            var record = new AlphaCharacterRecord
            {
                Name = hero.Name,
                Class = hero.Sheet.Class,
                Level = hero.Sheet.Progression.Level,
                Appearance = hero.Appearance,
            };
            var session = new SessionSaveData();
            SaveResult saved = _profiles.SaveCharacter(record, hero, session, now);
            if (!saved.IsSuccess)
            {
                return Report(false, $"character save failed: {saved}");
            }

            _selectedCharacter = record;
            _pendingHero = hero;
            _pendingSession = session;
            Debug.Log($"[Game] character created id={record.Id} '{record.Name}' class={record.Class}");
            ShowStartupScreen(StartupScreen.Worlds);
            return Report(true, $"character '{record.Name}' saved");
        }

        // ----- World list (0x4A23D0) and creation (0x482530) -----

        private void BuildWorldList(VisualElement panel, VisualElement footer)
        {
            Text(panel, "Select World", "screen-title", "heading");
            _statusLabel = Text(panel, "", "menu-status", "muted");
            if (_selectedCharacter != null)
            {
                Text(panel, _selectedCharacter.Name, "worlds-character", "muted");
            }

            foreach (AlphaWorldRecord world in _profiles.Worlds)
            {
                AlphaWorldRecord selected = world;
                Button row = ButtonOf(panel,
                    $"{(string.IsNullOrEmpty(world.Name) ? " " : world.Name)}\nSeed {world.Seed}",
                    $"world-{world.Id}", () => EnterWorld(selected));
                row.AddToClassList("entry-row");
            }

            if (_profiles.Worlds.Count == 0)
            {
                Text(panel, "No worlds", "no-worlds", "muted");
            }

            ButtonOf(footer, "Create World", "new-world", () =>
            {
                _formWorldName = string.Empty;
                _formSeed = string.Empty;
                ShowStartupScreen(StartupScreen.CreateWorld);
            });
            ButtonOf(footer, "Back", "menu-back", () => ShowStartupScreen(StartupScreen.Characters));
        }

        private void BuildWorldCreation(VisualElement panel, VisualElement footer)
        {
            Text(panel, "Create World", "screen-title", "heading");
            _statusLabel = Text(panel, "", "menu-status", "muted");
            AddField(panel, "Name", "world-name", _formWorldName, value => _formWorldName = value);
            AddField(panel, "Seed (must be a number)", "world-seed", _formSeed, value => _formSeed = value);
            ButtonOf(footer, "Create World", "create-world", () => ConfirmWorldCreation());
            ButtonOf(footer, "Back", "menu-back", () => ShowStartupScreen(StartupScreen.Worlds));
        }

        /// <summary>
        /// Alpha 0x482530: stores the new WorldInfo (name + parsed seed) and loads it at once.
        /// The seed field is parsed like the original <c>wistream &gt;&gt; int</c> without range validation;
        /// text that is not a number yields 0 (the value WorldInfo::ctor_1 leaves in +0x20).
        /// </summary>
        public bool ConfirmWorldCreation()
        {
            if (_selectedCharacter == null || _pendingHero == null)
            {
                return Report(false, "select a character first");
            }

            uint seed = ParseAlphaSeed(_formSeed);
            string name = _formWorldName ?? string.Empty;
            long now = DateTime.UtcNow.Ticks;
            AlphaWorldRecord world = _profiles.FindWorld(seed, name);
            if (world == null)
            {
                world = new AlphaWorldRecord
                {
                    Name = name,
                    Seed = seed,
                    GeneratorId = GeneratorId,
                    CreatedUtcTicks = now,
                    SavedUtcTicks = now,
                };
                SaveResult saved = _profiles.AddWorld(world, now);
                if (!saved.IsSuccess)
                {
                    return Report(false, $"world save failed: {saved}");
                }

                Debug.Log($"[Game] world created id={world.Id} name='{world.Name}' seed={world.Seed}");
            }

            return EnterWorld(world);
        }

        /// <summary>Alpha seed field: an integer as typed; anything else is 0.</summary>
        public static uint ParseAlphaSeed(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0u;
            if (!long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                return 0u;
            }

            return value >= int.MinValue && value <= int.MaxValue ? unchecked((uint)(int)value) : 0u;
        }

        /// <summary>
        /// World select: load the world state and enter it with the character's saved position for that world.
        /// Alpha 0x46A8A0 uses the character's per-world map entry when it exists, and the world start position
        /// when the character has never entered that world.
        /// </summary>
        public bool EnterWorld(AlphaWorldRecord world)
        {
            if (world == null) return Report(false, "world is missing");
            if (_pendingHero == null) return Report(false, "select a character first");

            AlphaWorldLoad load = _profiles.LoadWorld(world.Id, GeneratorId);
            if (!load.IsSuccess)
            {
                return Report(false, $"world {world.Id}: {load.Message}");
            }

            HeroState hero = _pendingHero;
            AlphaWorldVisit visit = _selectedCharacter?.FindVisit(world);
            if (visit != null)
            {
                hero.Position = new WorldPosition(visit.Position.X, visit.Position.Y, visit.Position.Z);
                hero.Yaw = visit.Position.Yaw;
                hero.NeedsGroundPlacement = false;
            }
            else
            {
                // No map entry: the world start position (WorldInfo::ctor_0 0x46A8A0 copy of world+0x8003D4).
                hero.NeedsGroundPlacement = true;
            }

            WorldInfo info = load.Record.ToWorldInfo();
            string key = AlphaProfileStore.CharacterKey(_selectedCharacter != null ? _selectedCharacter.Id : 0);
            _pendingEntry = new PendingWorldEntry
            {
                World = load.Record,
                WorldInfo = info,
                Hero = hero,
                Load = load,
                Key = key,
                AtSavedPosition = visit != null,
            };
            _enteringWorld = true;
            _status = $"Opening '{load.Record.Name}' for {hero.Name}";
            RequestRedraw();
            StartCoroutine(EnterWorldRoutine());
            return true;
        }

        /// <summary>
        /// The world build blocks the main thread for several seconds, so the Alpha wait screen ("Please wait...",
        /// `start.plx` node `wait`, string 0x2FEE74) is painted for one frame first and stays on screen while the
        /// build runs.
        /// </summary>
        private System.Collections.IEnumerator EnterWorldRoutine()
        {
            yield return null;
            PendingWorldEntry entry = _pendingEntry;
            _pendingEntry = null;
            _enteringWorld = false;
            if (entry == null) yield break;

            if (!_session.BeginLoadedWorld(entry.Key, entry.WorldInfo, entry.Hero, entry.Load.ClockTick,
                    entry.Load.Deltas, entry.Load.WorldTimeOffsetMs))
            {
                Report(false, $"world entry refused: {_session.LastRejection}");
                ShowStartupScreen(StartupScreen.Worlds);
                yield break;
            }

            _selectedWorld = entry.World;
            _pendingHero = null;
            _pendingSession = null;
            _runningSession = new SessionSaveData();
            _profileFlow = true;
            _legacySlots = false;
            _overlay = GameOverlay.None;
            EnterLoadedWorld(entry.AtSavedPosition
                ? $"world '{entry.WorldInfo.Name}' seed={entry.WorldInfo.Seed} character '{entry.Hero.Name}' at saved position"
                : $"world '{entry.WorldInfo.Name}' seed={entry.WorldInfo.Seed} character '{entry.Hero.Name}' at world start");
        }

        // ----- In-game overlays: Esc system menu (SystemWidget 0x587710) + Options (0x800A00) -----

        private void ToggleSystemMenu()
        {
            if (_session.State != SessionState.InGame) return;
            _overlay = _overlay == GameOverlay.None ? GameOverlay.System : GameOverlay.None;
            RequestRedraw();
            SetGameplayInput(true);
        }

        public void CloseOverlay()
        {
            if (_overlay == GameOverlay.None) return;
            _overlay = GameOverlay.None;
            RequestRedraw();
            SetGameplayInput(true);
        }

        private VisualElement BuildOverlayMenu()
        {
            var panel = Surface("system-menu", "system-menu");
            switch (_overlay)
            {
                case GameOverlay.Options:
                    panel.name = "options-screen";
                    BuildOptionsScreen(panel, null, true);
                    break;
                default:
                    // SystemWidget 0x587710: Options / Start Menu / Exit Game.
                    ButtonOf(panel, "Options", "system-options", () => OpenOverlayOptions());
                    ButtonOf(panel, "Start Menu", "system-start-menu", () => ExitToStartMenu());
                    ButtonOf(panel, "Exit Game", "system-exit", QuitGame);
                    break;
            }

            return panel;
        }

        private void OpenOverlayOptions()
        {
            _overlay = GameOverlay.Options;
            RequestRedraw();
        }

        /// <summary>
        /// System menu "Start Menu": Alpha 0x47E0BC saves the character (saveEntityBlob) and the world
        /// (saveStructureBlob), then shows the start screen and resets the pending world request.
        /// </summary>
        public bool ExitToStartMenu()
        {
            if (_session.State == SessionState.InGame && !SaveCheckpoint())
            {
                return false;
            }

            _overlay = GameOverlay.None;
            if (!ExitToMenuInternal())
            {
                return false;
            }

            ShowStartupScreen(StartupScreen.Start);
            _selectedCharacter = null;
            _selectedWorld = null;
            _pendingHero = null;
            _pendingSession = null;
            _runningSession = null;
            _profileFlow = false;
            ReloadProfiles();
            return true;
        }

        // ----- Shared UI helpers of the start screens -----

        private TextField AddField(VisualElement parent, string label, string name, string value,
            Action<string> changed)
        {
            var field = new TextField(label) { name = name, value = value };
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            parent.Add(field);
            return field;
        }

        /// <summary>
        /// One carousel row of the editor: label, value and the two arrows Alpha cycles the index with
        /// (negative → last, ≥ count → 0, CharCreation_refresh_model 0x42C080).
        /// </summary>
        private void AddCarousel(VisualElement parent, string label, string name, IReadOnlyList<string> values, int index,
            Action<int> changed)
        {
            var row = Row(parent, "carousel");
            ButtonOf(row, "<", name + "-prev", () =>
            {
                changed(Cycle(index - 1, values.Count));
                _audio.Play("menu-select.wav");
            });
            Text(row, label, name + "-label", "carousel-label");
            Text(row, values[Mathf.Clamp(index, 0, values.Count - 1)], name, "carousel-value");
            ButtonOf(row, ">", name + "-next", () =>
            {
                changed(Cycle(index + 1, values.Count));
                _audio.Play("menu-select.wav");
            });
        }

        private static int Cycle(int index, int count) => count <= 0 ? 0 : ((index % count) + count) % count;

        private static List<string> NumberedChoices(int count)
        {
            var choices = new List<string>(count);
            for (int i = 0; i < count; i++) choices.Add((i + 1).ToString(CultureInfo.InvariantCulture));
            return choices;
        }
    }
}
