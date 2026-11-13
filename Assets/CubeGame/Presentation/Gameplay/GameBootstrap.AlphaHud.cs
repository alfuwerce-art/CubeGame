using System;
using System.IO;
using System.Linq;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Core.Items;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Presentation.Visual;
using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// In-game screen of Alpha 2013. Alpha has no permanent buttons on the game screen: it shows the HUD widgets of
    /// <c>gui.plx</c> placed by code, and the six menu buttons only while Esc has switched the GUI flag on.
    ///
    /// Placement (ALPHA_CODE_STATIC, Cube.exe bytes; W/H = window client size, y grows downwards):
    ///   hpbar          pivot (W/2 − 163, H − 90)                 GameController::vfunc_10 0x492CC6..0x492CEA
    ///   mpbar          pivot (W/2 + 8,   H − 90)                 0x492A71
    ///   staminabar     pivot (W/2, H − 115), castbar (W/2, H − 150) 0x492E3E / 0x49303C (no stamina/cast model: not shown)
    ///   experiencebar  pivot (100, 75)                           0x490BC8 (0x42C80000, 0x42960000)
    ///   crosshair      (W/2, H/2)                                0x4927DA
    ///   hotbar         n clones, pivot x = W/2 + 24·(2i − n) + 6, y = H − 50 (pitch 48)   vfunc_4 0x48331F
    ///   menu buttons   n = 6, centre x = W/2 + 30·(2i − n + 1), y = H − 140 (pitch 60)     vfunc_4 0x483422
    /// Template geometry and colours (ALPHA_DATA, gui.plx decoded by tools/alpha_research/plx_dump.py): bar frame
    /// 158×27 with the pivot at its top-left, 2 px white stroke over an 8 px black one, fill inset 4 px, a white 12 %
    /// gloss on the upper half, "HP"/"MP"/"XP" caption 10 px, value text 9 px; ability button 42×42 (2 px white frame,
    /// 4 px black stroke), key caption 10 px; menu button 48×48 sprite quad with the key caption under it, hover scale
    /// 1.5 (0x48EC02). Slot fills M1/M2/empty/Q and the HP red are ALPHA_CAPTURE values of the running original.
    /// Hotbar captions M1, M2, 1…4, Q (0x70088C, 0x700894, decimal i − 1, 0x70089C); menu captions F1, X, C, B, M, O
    /// for help / skills / crafting / inventory / world map / system (0x7008C4…0x700924, handlers 0x47B7C6).
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private sealed class AlphaBar
        {
            public VisualElement Root;
            public VisualElement Fill;
            public Label Value;
            public float Ratio;
        }

        private static readonly string[] HotbarCaptions = { "M1", "M2", "1", "2", "3", "4" };

        // ALPHA_CAPTURE (shots of the running original, P1): slot fills of the primary / secondary attack, an empty slot
        // and the quick-item slot. The background colour rule of ability slots 1…4 is UNKNOWN.
        private static readonly Color PrimarySlotFill = new Color32(246, 78, 19, 255);
        private static readonly Color SecondarySlotFill = new Color32(117, 13, 255, 255);
        private static readonly Color EmptySlotFill = new Color32(143, 145, 145, 255);

        private AlphaGuiIcons _guiIcons;
        private bool _menuMode;
        private AlphaBar _hpBar, _mpBar, _hpSmallBar, _xpBar, _targetBar;
        private Label _nameLabel, _timeLabel;
        private VisualElement _menuRow, _targetPanel;
        private readonly Button[] _hotbarSlots = new Button[HotbarCaptions.Length];
        private readonly Image[] _hotbarIcons = new Image[HotbarCaptions.Length];
        private readonly int[] _hotbarShown = new int[HotbarCaptions.Length];
        private Label _potionCount;

        /// <summary>Alpha GUI flag <c>+0x8008F2</c>: Esc shows the six menu buttons and frees the mouse from the game.</summary>
        public bool MenuMode => _menuMode;

        /// <summary>Esc of Alpha 0x47E770: toggle the GUI flag and close the map.</summary>
        public void ToggleMenuMode()
        {
            if (_session.State != SessionState.InGame) return;
            _menuMode = !_menuMode;
            if (_panel == GamePanel.Map) ShowPanel(GamePanel.None, false);
            _contextChangedFrame = Time.frameCount;
            if (_menuRow != null) _menuRow.style.display = _menuMode ? DisplayStyle.Flex : DisplayStyle.None;
            _audio.Play(_menuMode ? "menu-open2.wav" : "menu-close2.wav");
        }

        private AlphaGuiIcons GuiIcons
        {
            get
            {
                if (_guiIcons == null)
                {
                    _guiIcons = new AlphaGuiIcons(LocalContentRoot(AlphaContentInventory.GuiFolder));
                    Debug.Log(_guiIcons.Available
                        ? $"[UI] GUI_ICONS=RUNTIME dir={_guiIcons.DirectoryPath}"
                        : "[UI] GUI_ICONS=ABSENT run tools/alpha_content/extract_gui_icons.py to stage the Alpha interface icons");
                }

                return _guiIcons;
            }
        }

        /// <summary>First local content root that holds <paramref name="folder"/> (same search as the audio).</summary>
        private string LocalContentRoot(string folder)
        {
            string[] roots =
            {
                SoundContentRoot,
                Environment.GetEnvironmentVariable(AlphaContentSource.EnvVar),
                AlphaActorLibrary.Shared.Source.Root,
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".scratch", "content")),
                Path.Combine(Application.persistentDataPath, "CubeGame", "AlphaContent"),
            };
            return roots.FirstOrDefault(root => !string.IsNullOrEmpty(root) && Directory.Exists(Path.Combine(root, folder)));
        }

        private void BuildHud()
        {
            var hud = new VisualElement { name = "hero-hud", pickingMode = PickingMode.Ignore };
            hud.AddToClassList("alpha-hud");
            _interface.Add(hud);

            // Top-left character widget (experiencebar template: name, class line, HP and XP bars).
            var character = Layer(hud, "hud-character", "alpha-hud-character");
            _nameLabel = HudText(character, "hud-name", "alpha-hud-name");
            _heroLabel = HudText(character, "hero-summary", "alpha-hud-class");
            _hpSmallBar = Bar(character, "hud-health-small", "HP", "alpha-bar-small alpha-bar-hp");
            _xpBar = Bar(character, "hero-experience", "XP", "alpha-bar-small alpha-bar-xp");

            // Top-right world line (ALPHA_CAPTURE position; TEMP/HUM, compass, region name and mini map are not ported).
            _timeLabel = HudText(hud, "hud-time", "alpha-hud-time");

            var crosshair = new VisualElement { name = "crosshair", pickingMode = PickingMode.Ignore };
            crosshair.AddToClassList("alpha-crosshair");
            var horizontal = new VisualElement { pickingMode = PickingMode.Ignore };
            horizontal.AddToClassList("alpha-crosshair-h");
            crosshair.Add(horizontal);
            var vertical = new VisualElement { pickingMode = PickingMode.Ignore };
            vertical.AddToClassList("alpha-crosshair-v");
            crosshair.Add(vertical);
            hud.Add(crosshair);

            // Bottom centre: HP / MP bars and the hotbar.
            _hpBar = Bar(hud, "hero-health", "HP", "alpha-bar-main alpha-bar-hp alpha-hp-anchor");
            _mpBar = Bar(hud, "hero-mana", "MP", "alpha-bar-main alpha-bar-mp alpha-mp-anchor");

            var hotbar = Layer(hud, "hotbar", "alpha-hotbar");
            for (int i = 0; i < HotbarCaptions.Length; i++)
            {
                int slot = i + 1;
                Button button = Slot(hotbar, $"hotbar-{slot}", HotbarCaptions[i], () =>
                {
                    if (slot == 1) HeroAttack();
                    else HeroSlot(slot);
                });
                _hotbarSlots[i] = button;
                _hotbarIcons[i] = button.Q<Image>();
                _hotbarShown[i] = -1;
                button.style.backgroundColor = i == 0 ? PrimarySlotFill : i == 1 ? SecondarySlotFill : EmptySlotFill;
            }

            _potionButton = Slot(hotbar, "use-potion", "Q", UsePotionShortcut);
            _potionButton.AddToClassList("alpha-slot-quick");
            _potionButton.Q<Image>().sprite = ItemIcon(LootRules.LifePotion);
            _potionCount = new Label { name = "potion-count", pickingMode = PickingMode.Ignore };
            _potionCount.AddToClassList("alpha-slot-count");
            _potionButton.Add(_potionCount);

            // Esc menu buttons (hidden until the GUI flag is on).
            _menuRow = Layer(hud, "menu-row", "alpha-menu-row");
            MenuButton("open-help", "help.png", "F1", () => TogglePanel(GamePanel.Help));
            MenuButton("open-skills", "skills.png", "X", () => TogglePanel(GamePanel.Skills));
            MenuButton("open-crafting", "crafting.png", "C", () => TogglePanel(GamePanel.Crafting));
            MenuButton("open-inventory", "inventory.png", "B", ToggleInventory);
            MenuButton("open-map", "worldmap.png", "M", () => TogglePanel(GamePanel.Map));
            MenuButton("system-menu", "system.png", "O", ToggleSystemMenu);
            _menuRow.style.display = _menuMode ? DisplayStyle.Flex : DisplayStyle.None;

            // Target life bar (Alpha draws enemylifebar over the creature itself — CUBEGAME_CONTRACT screen position).
            _targetPanel = Layer(hud, "target-panel", "alpha-hud-target");
            _targetLabel = HudText(_targetPanel, "combat-target", "alpha-hud-target-name");
            _targetBar = Bar(_targetPanel, "target-health", null, "alpha-bar-target alpha-bar-hp");

            // "[E] TALK" speech template of gui.plx; Alpha shows it over the NPC (CUBEGAME_CONTRACT position).
            _interactButton = new Button(InteractFromInterface) { name = "interact" };
            _interactButton.AddToClassList("alpha-interact");
            hud.Add(_interactButton);

            _panelHost = new VisualElement { pickingMode = PickingMode.Ignore };
            _panelHost.AddToClassList("panel-host");
            _interface.Add(_panelHost);

            // Message lines (Alpha ChatWidget area, bottom left — CUBEGAME_CONTRACT wording).
            _feedbackLabel = Text(_interface, "", "game-feedback", "alpha-feedback");
            _feedbackLabel.pickingMode = PickingMode.Ignore;
        }

        private void RefreshHud()
        {
            HeroState hero = _session.Hero;
            var progress = hero.Sheet.Progression;
            SetText(_nameLabel, hero.Name);
            // Alpha line "LVL " + level + class (Interface::drawCharacterCreation 0x425450 builds the same text).
            SetText(_heroLabel, $"LVL {progress.Level} {hero.Sheet.Class}");
            SetBar(_hpBar, hero.Health.CurrentHealth, hero.Health.MaxHealth);
            SetBar(_hpSmallBar, hero.Health.CurrentHealth, hero.Health.MaxHealth);
            SetBar(_xpBar, progress.Experience, progress.ExperienceToNextLevel);
            // mpbar text of 0x4929D0: (int)(mana · 100 + 0.5) "/100".
            SetBar(_mpBar, Mathf.RoundToInt(hero.Mana * 100f), 100);
            int minutes = _session.Clock.WorldTimeMs / 60_000;
            SetText(_timeLabel, $"TIME {minutes / 60}:{minutes % 60:00}");

            int[] bar = _session.HeroHotbar();
            for (int i = 0; i < _hotbarSlots.Length; i++)
            {
                int action = i < bar.Length ? bar[i] : 0;
                if (_hotbarShown[i] == action) continue;
                _hotbarShown[i] = action;
                _hotbarIcons[i].image = action > 0 ? GuiIcons.Get(AlphaGuiIcons.ActionIcon(action)) : null;
                _hotbarSlots[i].SetEnabled(action > 0 || i == 0);
            }

            int potions = hero.Inventory.CountOf(LootRules.LifePotion);
            SetText(_potionCount, potions > 0 ? potions.ToString() : string.Empty);
            _potionButton.Q<Image>().style.display = potions > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _potionButton.SetEnabled(!HeroDead && _panel == GamePanel.None);

            if (!_lastCombatTarget.IsNone && _session.Combat.TryGet(_lastCombatTarget, out Combatant target)
                && target.Health.CurrentHealth > 0)
            {
                _targetPanel.style.display = DisplayStyle.Flex;
                SetText(_targetLabel, target.Name);
                SetBar(_targetBar, target.Health.CurrentHealth, target.Health.MaxHealth);
            }
            else
            {
                _targetPanel.style.display = DisplayStyle.None;
            }

            bool npc = !_interactionTarget.IsNone && TryTownNpc(_interactionTarget, out _, out _);
            _interactButton.style.display = _interactionTarget.IsNone ? DisplayStyle.None : DisplayStyle.Flex;
            _interactButton.SetEnabled(!_interactionTarget.IsNone);
            SetText(_interactButton, npc ? "[E] Talk" : "[E] Open");

            while (_feedback.Count > 0 && _feedback.Peek().Until <= Time.unscaledTime)
            {
                _feedback.Dequeue();
            }

            string message = HeroDead ? "You fell. Returning to the world start…" :
                string.Join("\n", _feedback.Select(notice => notice.Message));
            SetText(_feedbackLabel, message);
            _feedbackLabel.style.display = message.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private Sprite ItemIcon(RawAlphaItemId id)
        {
            if (_itemIcons == null && !_itemIconsChecked)
            {
                _itemIconsChecked = true;
                AlphaIconAtlas.TryLoad(AlphaIconAtlas.DefaultDirectory, out _itemIcons, null);
            }

            return AlphaItemIcons.TryEntry(id, out string key) ? _itemIcons?.TryGetSprite(key) : null;
        }

        private void MenuButton(string name, string icon, string caption, Action action)
        {
            var button = new Button(() =>
            {
                action();
                _audio.Play("menu-select.wav");
            }) { name = name, text = string.Empty };
            button.AddToClassList("alpha-menu-button");
            var image = new Image { image = GuiIcons.Get(icon), pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("alpha-menu-icon");
            button.Add(image);
            var label = new Label(caption) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("alpha-menu-key");
            button.Add(label);
            _menuRow.Add(button);
        }

        private static Button Slot(VisualElement parent, string name, string caption, Action action)
        {
            var button = new Button(action) { name = name, text = string.Empty };
            button.AddToClassList("alpha-slot");
            var image = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("alpha-slot-icon");
            button.Add(image);
            var key = new Label(caption) { pickingMode = PickingMode.Ignore };
            key.AddToClassList("alpha-slot-key");
            button.Add(key);
            parent.Add(button);
            return button;
        }

        private static AlphaBar Bar(VisualElement parent, string name, string caption, string classes)
        {
            var root = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            root.AddToClassList("alpha-bar");
            foreach (string style in classes.Split(' ')) root.AddToClassList(style);
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("alpha-bar-frame");
            root.Add(frame);
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("alpha-bar-track");
            frame.Add(track);
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("alpha-bar-fill");
            track.Add(fill);
            var gloss = new VisualElement { pickingMode = PickingMode.Ignore };
            gloss.AddToClassList("alpha-bar-gloss");
            frame.Add(gloss);
            var value = new Label { name = name + "-text", pickingMode = PickingMode.Ignore };
            value.AddToClassList("alpha-bar-value");
            frame.Add(value);
            if (caption != null)
            {
                var label = new Label(caption) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("alpha-bar-caption");
                root.Add(label);
            }

            parent.Add(root);
            return new AlphaBar { Root = root, Fill = fill, Value = value, Ratio = -1f };
        }

        private static void SetBar(AlphaBar bar, double current, double max)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)(current / max)) : 0f;
            if (!Mathf.Approximately(bar.Ratio, ratio))
            {
                bar.Ratio = ratio;
                bar.Fill.style.width = Length.Percent(ratio * 100f);
            }

            SetText(bar.Value, $"{Math.Max(0, Math.Floor(current)):0}/{max:0}");
        }

        private static VisualElement Layer(VisualElement parent, string name, string style)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.AddToClassList(style);
            parent.Add(layer);
            return layer;
        }

        private static Label HudText(VisualElement parent, string name, string style)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList("alpha-hud-text");
            label.AddToClassList(style);
            parent.Add(label);
            return label;
        }
    }
}
