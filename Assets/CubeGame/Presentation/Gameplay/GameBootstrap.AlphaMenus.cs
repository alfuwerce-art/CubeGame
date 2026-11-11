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
    ///
    /// Presentation follows the Alpha widgets: centred small-caps entries for the start menu (0x583320), 230 px rows
    /// from y = 50 with the Alpha colours for the two lists (0x49D650 / 0x4A23D0), and flat 198x66 / 298x66 framed
    /// buttons for Back / Select / Create (complete list of measured values in <see cref="AlphaStartupPresentation"/>).
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
            Debug.Log($"[UI] screen {screen}");
        }

        /// <summary>Rebuilds the visible screen on the next LateUpdate without touching the state machine.</summary>
        private void RequestRedraw() => _redraw = true;

        // ----- Screen frame -----

        private void BuildStartupScreen()
        {
            ReleaseRowPreviews();
            SetPixelScaleUi(true);
            var screen = new VisualElement { name = "main-menu" };
            screen.AddToClassList("alpha-screen");

            if (_enteringWorld)
            {
                // Alpha shows the start.plx "Please wait..." node while the world is opened.
                screen.AddToClassList("alpha-wait");
                var wait = new VisualElement { name = "wait-screen" };
                wait.AddToClassList("alpha-center");
                AlphaStartupPresentation.Line(wait, "Please wait...", "wait-label",
                    AlphaStartupPresentation.TitleColor, AlphaStartupPresentation.TextSize);
                AlphaStartupPresentation.Line(wait, _status, "wait-status", AlphaStartupPresentation.DetailColor,
                    AlphaStartupPresentation.DetailSize);
                screen.Add(wait);
                _interface.Add(screen);
                return;
            }

            switch (_startupScreen)
            {
                case StartupScreen.Options:
                    var optionsPage = new VisualElement { name = "options-page" };
                    optionsPage.AddToClassList("alpha-center");
                    optionsPage.AddToClassList("alpha-options");
                    screen.Add(optionsPage);
                    BuildOptionsScreen(optionsPage, null, false);
                    break;
                case StartupScreen.Characters:
                    BuildCharacterList(screen);
                    break;
                case StartupScreen.Editor:
                    BuildCharacterEditor(screen);
                    break;
                case StartupScreen.Worlds:
                    BuildWorldList(screen);
                    break;
                case StartupScreen.CreateWorld:
                    BuildWorldCreation(screen);
                    break;
                default:
                    BuildStartMenu(screen);
                    break;
            }

            _statusLabel = AlphaStartupPresentation.Line(screen, _status, "menu-status",
                AlphaStartupPresentation.DetailColor, AlphaStartupPresentation.DetailSize);
            _statusLabel.AddToClassList("alpha-status");
            _interface.Add(screen);
        }

        /// <summary>
        /// Start menu of 0x583320: exactly three centred entries (Start Game / Options / Exit), text size 18 with the
        /// 4 px outline pass, cyan on hover. Alpha has no title, no logo box and no frame here — the 3D scene and the
        /// logo texture of start.plx carry the identity.
        /// </summary>
        private void BuildStartMenu(VisualElement screen)
        {
            var menu = new VisualElement { name = "start-menu" };
            menu.AddToClassList("alpha-menu");
            screen.Add(menu);
            AlphaStartupPresentation.MenuItem(menu, "Start Game", "start-game",
                () => ShowStartupScreen(StartupScreen.Characters));
            AlphaStartupPresentation.MenuItem(menu, "Options", "menu-options",
                () => ShowStartupScreen(StartupScreen.Options));
            AlphaStartupPresentation.MenuItem(menu, "Exit", "menu-exit", QuitGame);
        }

        // ----- Character list (0x49D650: rows 230 px from y = 50 + the extra "Create Character" row) -----

        private void BuildCharacterList(VisualElement screen)
        {
            AddBackButton(screen, () => ShowStartupScreen(StartupScreen.Start));
            var rows = RowsHost(screen);
            foreach (AlphaCharacterRecord record in _profiles.Characters)
            {
                AlphaCharacterRecord selected = record;
                Button row = AlphaRow(rows, $"character-{record.Id}", () => SelectCharacter(selected.Id));
                AddCharacterPreviewSlot(row, record.Appearance);
                VisualElement info = RowInfo(row);
                string race = AlphaHeroCreation.Races[
                    Mathf.Clamp(record.Appearance.Race, 0, AlphaHeroCreation.Races.Length - 1)];
                // Text calls of Interface::drawCharacterCreation 0x425450: name at (200,30) size 14,
                // "LVL <n> <race> <class>" at (200,55) size 12, "Specialization: <x>" at (200,75) size 10.
                AlphaStartupPresentation.Line(info, record.Name, null, AlphaStartupPresentation.NameColor,
                    AlphaStartupPresentation.NameSize);
                AlphaStartupPresentation.Line(info, $"Lvl {record.Level} {race} {record.Class}",
                    name: null, color: AlphaStartupPresentation.TitleColor, size: AlphaStartupPresentation.TitleSize);
                // The specialization line of Alpha needs the creature specialization, which the character record of this
                // port does not store yet (report: CHARACTER_LIST_VISUAL=PARTIAL).
                AddRowAction(row, "Select");
            }

            Button newRow = AlphaRow(rows, "new-character", BeginCharacterCreation);
            newRow.AddToClassList("alpha-row-empty");
            AlphaStartupPresentation.Line(newRow, "New character", null, AlphaStartupPresentation.TitleColor,
                AlphaStartupPresentation.NameSize);
            AddRowAction(newRow, "Create");
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

        private void BuildCharacterEditor(VisualElement screen)
        {
            AddBackButton(screen, () => ShowStartupScreen(StartupScreen.Characters));
            var page = new VisualElement { name = "character-editor" };
            page.AddToClassList("alpha-center");
            page.AddToClassList("alpha-editor");
            screen.Add(page);

            var body = new VisualElement();
            body.AddToClassList("alpha-editor-body");
            page.Add(body);

            var left = new VisualElement();
            left.AddToClassList("alpha-editor-preview");
            // Alpha previews the creature model while the editor is open (CharacterPreviewWidget 0x424E80).
            VisualElement preview = PreviewPanel(left);
            body.Add(left);
            CreatePreviewStage(preview, _formLook);

            var fields = new VisualElement();
            fields.AddToClassList("alpha-editor-fields");
            body.Add(fields);

            TextField name = AddField(fields, "Name", "hero-name", _formHeroName, value => _formHeroName = value);
            name.maxLength = AlphaHeroCreation.MaxNameLength;

            AddCarousel(fields, "Class", "hero-class", Enum.GetNames(typeof(CharacterClass)), _formClass, index =>
            {
                _formClass = index;
                RefreshEditorPreview();
            });
            AddCarousel(fields, "Race", "hero-race", AlphaHeroCreation.Races, _formLook.Race,
                index => SetEditorAppearance(index, _formLook.Gender, _formLook.Face, _formLook.Haircut));
            AddCarousel(fields, "Sex", "hero-sex", new[] { "Male", "Female" }, _formLook.Gender,
                index => SetEditorAppearance(_formLook.Race, index, _formLook.Face, _formLook.Haircut));
            AddCarousel(fields, "Face", "hero-face",
                NumberedChoices(AlphaHeroCreation.FaceCount(AlphaHeroCreation.KindOf(_formLook))),
                _formLook.Face, index => SetEditorAppearance(_formLook.Race, _formLook.Gender, index, _formLook.Haircut));
            AddCarousel(fields, "Hair", "hero-hair",
                NumberedChoices(AlphaHeroCreation.HaircutCount(AlphaHeroCreation.KindOf(_formLook))),
                _formLook.Haircut, index => SetEditorAppearance(_formLook.Race, _formLook.Gender, _formLook.Face, index));

            // Alpha palette: 16 hues × 7 rows of shadings (drawCharacterAppearancePanel 0x428E40).
            var swatches = new List<Button>();
            for (int rowIndex = 0; rowIndex < AlphaHeroCreation.PaletteRows; rowIndex++)
            {
                var palette = new VisualElement();
                palette.AddToClassList("row");
                palette.AddToClassList("palette");
                fields.Add(palette);
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

            var actions = new VisualElement();
            actions.AddToClassList("row");
            actions.AddToClassList("alpha-editor-actions");
            page.Add(actions);
            AlphaStartupPresentation.Button(actions, "Create Character", "create-character", true,
                () => ConfirmCharacterEditor());
            AlphaStartupPresentation.Button(actions, "Back", "menu-back", false,
                () => ShowStartupScreen(StartupScreen.Characters));

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

        private void BuildWorldList(VisualElement screen)
        {
            AddBackButton(screen, () => ShowStartupScreen(StartupScreen.Characters));
            // 0x4A23D0 keeps a separate singleplayer / multiplayer mode; the single player M0 renders no switcher.
            AlphaStartupPresentation.Button(screen, "Multiplayer Worlds...", "worlds-mode", true, () =>
                Report(false, "multiplayer is outside the single player M0")).AddToClassList("alpha-top-right");

            var rows = RowsHost(screen);
            foreach (AlphaWorldRecord world in _profiles.Worlds)
            {
                AlphaWorldRecord selected = world;
                Button row = AlphaRow(rows, $"world-{world.Id}", () => EnterWorld(selected));
                AddWorldPreviewSlot(row);
                VisualElement info = RowInfo(row);
                // Text calls of GameController::drawWorldSelectEntry 0x605AE0: name at (200,30) size 14,
                // "Explored: <WorldInfo+0x24>" at (200,55) size 10, "Seed: <WorldInfo+0x20>" at (200,80) size 10.
                AlphaStartupPresentation.Line(info, string.IsNullOrEmpty(world.Name) ? " " : world.Name, null,
                    AlphaStartupPresentation.NameColor, AlphaStartupPresentation.NameSize);
                AlphaStartupPresentation.Line(info, $"Explored: {world.LegacyField24}", null,
                    AlphaStartupPresentation.TitleColor, AlphaStartupPresentation.DetailSize);
                AlphaStartupPresentation.Line(info, $"Seed: {world.Seed}", null,
                    AlphaStartupPresentation.DetailColor, AlphaStartupPresentation.DetailSize);
                AddRowAction(row, "Select");
            }

            Button newRow = AlphaRow(rows, "new-world", () =>
            {
                _formWorldName = string.Empty;
                _formSeed = string.Empty;
                ShowStartupScreen(StartupScreen.CreateWorld);
            });
            newRow.AddToClassList("alpha-row-empty");
            AlphaStartupPresentation.Line(newRow, "New world", null, AlphaStartupPresentation.TitleColor,
                AlphaStartupPresentation.NameSize);
            AddRowAction(newRow, "Create");

            AlphaStartupPresentation.Button(screen, "Connect to Server", "worlds-connect", true, () =>
                Report(false, "multiplayer is outside the single player M0")).AddToClassList("alpha-bottom-right");
        }

        private void BuildWorldCreation(VisualElement screen)
        {
            AddBackButton(screen, () => ShowStartupScreen(StartupScreen.Worlds));
            var page = new VisualElement { name = "world-creation" };
            page.AddToClassList("alpha-center");
            page.AddToClassList("alpha-creation");
            screen.Add(page);

            AlphaStartupPresentation.Line(page, "Create World", null, AlphaStartupPresentation.TitleColor,
                AlphaStartupPresentation.TextSize);
            AddField(page, "Name", "world-name", _formWorldName, value => _formWorldName = value);
            AddField(page, "Seed (must be a number)", "world-seed", _formSeed, value => _formSeed = value);

            var actions = new VisualElement();
            actions.AddToClassList("row");
            actions.AddToClassList("alpha-editor-actions");
            page.Add(actions);
            AlphaStartupPresentation.Button(actions, "Create World", "create-world", true,
                () => ConfirmWorldCreation());
            AlphaStartupPresentation.Button(actions, "Back", "menu-back", false,
                () => ShowStartupScreen(StartupScreen.Worlds));
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

        /// <summary>
        /// Alpha draws its interface 1:1 in pixels (the start menu is 18 px text in a 1936 px window and in a 1280 px
        /// window alike), so the startup screens switch the panel to constant pixel size. The in-game HUD keeps the
        /// scalable panel it was reviewed with.
        /// </summary>
        private void SetPixelScaleUi(bool startup)
        {
            if (_panelSettings == null) return;
            PanelScaleMode mode = startup ? PanelScaleMode.ConstantPixelSize : PanelScaleMode.ScaleWithScreenSize;
            if (_panelSettings.scaleMode != mode) _panelSettings.scaleMode = mode;
        }

        private VisualElement BuildOverlayMenu()
        {
            SetPixelScaleUi(false);
            var panel = new VisualElement { name = "system-menu" };
            panel.AddToClassList("alpha-screen");
            panel.AddToClassList("alpha-overlay");
            switch (_overlay)
            {
                case GameOverlay.Options:
                    panel.name = "options-screen";
                    var optionsPage = new VisualElement { name = "options-page" };
                    optionsPage.AddToClassList("alpha-center");
                    optionsPage.AddToClassList("alpha-options");
                    panel.Add(optionsPage);
                    BuildOptionsScreen(optionsPage, null, true);
                    break;
                default:
                    // SystemWidget 0x587710: Options / Start Menu / Exit Game — same three-entry layout as 0x583320 but
                    // 14 px text on a 30 px pitch (y = 30 / 60 / 90 inside the 350 px wide widget).
                    var menu = new VisualElement { name = "system-menu-items" };
                    menu.AddToClassList("alpha-menu");
                    menu.AddToClassList("alpha-menu-small");
                    panel.Add(menu);
                    AlphaStartupPresentation.MenuItem(menu, "Options", "system-options", OpenOverlayOptions);
                    AlphaStartupPresentation.MenuItem(menu, "Start Menu", "system-start-menu",
                        () => ExitToStartMenu());
                    AlphaStartupPresentation.MenuItem(menu, "Exit Game", "system-exit", QuitGame);
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

        // ----- Alpha row / button helpers (shared by the start screens) -----

        /// <summary>Back button of 0x4814F0 in the Alpha position: top-left, 198x66, flat frame.</summary>
        private void AddBackButton(VisualElement screen, Action action)
        {
            AlphaStartupPresentation.Button(screen, "Back", "menu-back", false, action)
                .AddToClassList("alpha-back-slot");
        }

        /// <summary>Vertical, centred host of the 230 px rows (0x49D650 / 0x4A23D0).</summary>
        private static VisualElement RowsHost(VisualElement screen)
        {
            var host = new VisualElement { name = "alpha-rows" };
            host.AddToClassList("alpha-rows");
            screen.Add(host);
            return host;
        }

        private static Button AlphaRow(VisualElement parent, string name, Action action)
        {
            var row = new Button(action) { name = name, text = string.Empty };
            row.AddToClassList("alpha-row");
            parent.Add(row);
            return row;
        }

        private static VisualElement RowInfo(VisualElement row)
        {
            var info = new VisualElement();
            info.AddToClassList("alpha-row-info");
            row.Add(info);
            return info;
        }

        /// <summary>
        /// Alpha shows the row action (Select / Create) as a 198x66 framed button only while the row is hovered;
        /// the button is part of the row subtree, so the visibility rule lives in USS and the click still reaches
        /// the row handler.
        /// </summary>
        private static void AddRowAction(VisualElement row, string label)
        {
            var action = new Label(label) { pickingMode = PickingMode.Ignore };
            action.AddToClassList("alpha-row-action");
            row.Add(action);
        }

        // ----- Shared UI helpers of the start screens -----

        private TextField AddField(VisualElement parent, string label, string name, string value,
            Action<string> changed)
        {
            var field = new TextField(label) { name = name, value = value };
            field.AddToClassList("alpha-field");
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
            var row = new VisualElement();
            row.AddToClassList("row");
            row.AddToClassList("carousel");
            parent.Add(row);
            var previous = new Button(() =>
            {
                changed(Cycle(index - 1, values.Count));
                _audio.Play("menu-select.wav");
            })
            {
                name = name + "-prev",
                text = "<",
            };
            previous.AddToClassList("alpha-arrow");
            row.Add(previous);

            Label caption = AlphaStartupPresentation.Line(row, label, name + "-label",
                AlphaStartupPresentation.DetailColor, AlphaStartupPresentation.TextSize);
            caption.AddToClassList("alpha-carousel-label");
            Label valueLabel = AlphaStartupPresentation.Line(row,
                values[Mathf.Clamp(index, 0, values.Count - 1)], name, AlphaStartupPresentation.TitleColor,
                AlphaStartupPresentation.TextSize);
            valueLabel.AddToClassList("alpha-carousel-value");

            var next = new Button(() =>
            {
                changed(Cycle(index + 1, values.Count));
                _audio.Play("menu-select.wav");
            })
            {
                name = name + "-next",
                text = ">",
            };
            next.AddToClassList("alpha-arrow");
            row.Add(next);
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
