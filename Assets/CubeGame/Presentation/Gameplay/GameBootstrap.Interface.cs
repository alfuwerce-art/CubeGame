using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Gameplay.Streaming;
using CubeGame.Presentation.Visual;
using UnityEngine;
using UnityEngine.UIElements;
using EquipmentSlot = CubeGame.Core.Equipment.EquipmentSlot;

namespace CubeGame.Gameplay
{
    public sealed partial class GameBootstrap
    {
        private enum GamePanel { None, Inventory, Skills, Town, Journal, Map }

        private GamePanel _panel;
        private SessionState? _drawnState;
        private GamePanel _drawnPanel;
        private bool _panelDirty;
        private int _contextChangedFrame = -1;
        private string _panelMessage = string.Empty;
        private PanelSettings _panelSettings;
        private VisualElement _interface;
        private VisualElement _panelHost;
        private Label _heroLabel, _resourcesLabel, _targetLabel, _statusLabel, _feedbackLabel;
        private ProgressBar _healthBar, _experienceBar;
        private Button _interactButton, _potionButton;
        private VisualElement _mapMarker;
        private HeroInventory _observedInventory;
        private HeroSkillRanks _skillDraft;
        private CharacterSpecialization _specializationDraft;
        private StableEntityId _interactionTarget, _lastCombatTarget;
        private AlphaIconAtlas _itemIcons;
        private GameAudio _audio;
        private readonly Queue<(string Message, float Until)> _feedback = new Queue<(string, float)>();

        public bool InventoryOpen => _panel == GamePanel.Inventory || _panel == GamePanel.Skills;

        /// <summary>Optional local cache root for audio; the shared Alpha content root is used by default.</summary>
        public string SoundContentRoot { get; set; }

        private void CreateInterface()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GameTheme");
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1280, 720);
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = 10;

            var view = new GameObject("Game Interface");
            view.SetActive(false);
            view.transform.SetParent(transform, false);
            var document = view.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            view.SetActive(true);
            _interface = document.rootVisualElement;
            _interface.name = "game-interface";
            _interface.pickingMode = PickingMode.Ignore;
            _interface.styleSheets.Add(Resources.Load<StyleSheet>("GameInterface"));
            ApplyAlphaFont();
            _audio = view.AddComponent<GameAudio>();
        }

        private void BindWorldPresentation()
        {
            SetPixelScaleUi(false);
            _audio.Release();
            BindLocalAudio();
            _observedInventory = _session.Hero.Inventory;
            _observedInventory.Changed += InvalidatePanel;
            _session.Events.Subscribe<HitResolved>(PresentHit);
            _session.Events.Subscribe<ExperienceGained>(PresentExperience);
            _session.Events.Subscribe<WorldItemRemoved>(PresentPickup);
            _session.Events.Subscribe<HeroItemUsed>(PresentItemUse);
            _session.Events.Subscribe<HuntChanged>(PresentHunt);
            _session.Events.Subscribe<EntityDied>(PresentDeath);
        }

        private void UnbindWorldPresentation()
        {
            if (_observedInventory != null)
            {
                _observedInventory.Changed -= InvalidatePanel;
                _observedInventory = null;
            }
            if (_session != null)
            {
                _session.Events.Unsubscribe<HitResolved>(PresentHit);
                _session.Events.Unsubscribe<ExperienceGained>(PresentExperience);
                _session.Events.Unsubscribe<WorldItemRemoved>(PresentPickup);
                _session.Events.Unsubscribe<HeroItemUsed>(PresentItemUse);
                _session.Events.Unsubscribe<HuntChanged>(PresentHunt);
                _session.Events.Unsubscribe<EntityDied>(PresentDeath);
            }
            _audio?.Release();
            _feedback.Clear();
            _interactionTarget = _lastCombatTarget = default;
        }

        private void OnDestroy()
        {
            UnbindWorldPresentation();
            ReleaseMap();
            _itemIcons?.Dispose();
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void BindLocalAudio()
        {
            if (SoundContentRoot != null)
            {
                _audio.Bind(SoundContentRoot);
                return;
            }

            string[] roots =
            {
                Environment.GetEnvironmentVariable(AlphaContentSource.EnvVar),
                AlphaActorLibrary.Shared.Source.Root,
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".scratch", "content")),
                Path.Combine(Application.persistentDataPath, "CubeGame", "AlphaContent"),
            };
            _audio.Bind(roots.FirstOrDefault(root => !string.IsNullOrEmpty(root)
                && Directory.Exists(Path.Combine(root, AlphaSoundLibrary.DirectoryName))));
        }

        private void UpdateInterfaceInput()
        {
            // One frame owns one input context. Alpha GameController::vfunc_1 0x47F1D0 makes the whole GUI "active"
            // while any screen, panel or text field is up, and 0x47E1B0 returns before the gameplay key switch while
            // a start screen is visible.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_session.State != SessionState.InGame) return;
                if (TextInputFocused())
                {
                    (_interface.focusController.focusedElement as VisualElement)?.Blur();
                    return;
                }

                if (_panel != GamePanel.None)
                {
                    ShowPanel(GamePanel.None);
                    return;
                }

                if (_overlay == GameOverlay.Options)
                {
                    CloseOptions();
                    return;
                }

                ToggleSystemMenu();
                return;
            }

            if (_session.State != SessionState.InGame || HeroDead || _overlay != GameOverlay.None || TextInputFocused())
            {
                return;
            }

            // Cube.exe 0x47E1B0 dispatches B/I to 0x488C00 and M to the map flag.
            // Resolve one UI transition before allowing gameplay to read the same frame.
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.I)) ToggleInventory();
            else if (Input.GetKeyDown(KeyCode.M)) TogglePanel(GamePanel.Map);
            else if (Input.GetKeyDown(KeyCode.K)) TogglePanel(GamePanel.Skills);
            else if (Input.GetKeyDown(KeyCode.J)) TogglePanel(GamePanel.Journal);
            if (!AcceptsGameplayInput())
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Q)) UsePotionShortcut();
            if (Input.GetKeyDown(KeyCode.E)) InteractFromInterface();
        }

        private bool AcceptsGameplayInput() => _session.State == SessionState.InGame && !HeroDead
            && _panel == GamePanel.None && _overlay == GameOverlay.None && !TextInputFocused()
            && _contextChangedFrame != Time.frameCount;

        private bool PointerOverInterface()
        {
            IPanel panel = _interface?.panel;
            if (panel == null)
            {
                return false;
            }
            Vector2 screen = Input.mousePosition;
            screen.y = Screen.height - screen.y;
            VisualElement hit = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, screen));
            return hit != null && _interface.Contains(hit);
        }

        public void ToggleInventory() => TogglePanel(GamePanel.Inventory);
        private void TogglePanel(GamePanel panel) => ShowPanel(_panel == panel ? GamePanel.None : panel);

        private void ShowPanel(GamePanel panel, bool playCue = true)
        {
            if (_panel == panel || (panel != GamePanel.None && (_session.State != SessionState.InGame || HeroDead)))
            {
                return;
            }

            if (_panel == GamePanel.Town) _townNpc = default;
            if (_panel == GamePanel.Map) ReleaseMap();
            _panel = panel;
            _contextChangedFrame = Time.frameCount;
            _panelMessage = string.Empty;
            _skillDraft = null;
            if (panel == GamePanel.Skills) BeginSkillDraft(_session.Hero);
            SetGameplayInput(_session.State == SessionState.InGame);
            InvalidatePanel();
            if (playCue) _audio.Play(panel == GamePanel.None ? "menu-close2.wav" : "menu-open2.wav");
        }

        private void UpdateInteractionTarget()
        {
            if (_panel == GamePanel.Town && !TryTownNpcInReach(_townNpc, out _))
            {
                ShowPanel(GamePanel.None);
            }
            _interactionTarget = _panel == GamePanel.None && !HeroDead ? NearestInteraction() : default;
        }

        private void InteractFromInterface()
        {
            if (_panel == GamePanel.None && _session.State == SessionState.InGame && !HeroDead)
            {
                SyncHeroFromView();
                StableEntityId id = NearestInteraction();
                if (!id.IsNone) TryInteract(id);
            }
        }

        private void UsePotionShortcut()
        {
            if (_panel == GamePanel.None && !UseFirst(LootRules.LifePotion))
            {
                Notify(_combatLog);
            }
        }

        private void InvalidatePanel() => _panelDirty = true;

        private void LateUpdate()
        {
            if (_interface == null)
            {
                return;
            }

            SessionState state = _session.State;
            GameOverlay overlay = state == SessionState.InGame ? _overlay : GameOverlay.None;
            if (_drawnState != state || _drawnOverlay != overlay || _redraw)
            {
                _redraw = false;
                _interface.Clear();
                _statusLabel = null;
                _drawnState = state;
                _drawnOverlay = overlay;
                _drawnPanel = GamePanel.None;
                switch (state)
                {
                    case SessionState.MainMenu:
                        BindLocalAudio();
                        DestroyPreviewStage();
                        _previewPanel = null;
                        BuildStartupScreen();
                        break;
                    case SessionState.Paused:
                        BuildHud();
                        break;
                    case SessionState.InGame:
                        BuildHud();
                        InvalidatePanel();
                        if (overlay != GameOverlay.None)
                        {
                            _interface.Add(BuildOverlayMenu());
                        }
                        else
                        {
                            DestroyPreviewStage();
                            _previewPanel = null;
                        }
                        break;
                }
            }

            if (state == SessionState.InGame)
            {
                RefreshHud();
                if (overlay == GameOverlay.None && (_panelDirty || _panel != _drawnPanel))
                {
                    bool preserveScroll = _panel == _drawnPanel;
                    BuildPanel(preserveScroll);
                    _drawnPanel = _panel;
                    _panelDirty = false;
                }
                if (_panel == GamePanel.Map) RefreshMapMarker();
            }

            if (_statusLabel != null)
            {
                string status = state == SessionState.InGame ? _panelMessage : _status;
                SetText(_statusLabel, status);
                _statusLabel.style.display = status.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void BuildHud()
        {
            var hud = Surface("hero-hud", "hero-hud");
            _interface.Add(hud);
            _heroLabel = Text(hud, "", "hero-summary", "heading");
            _healthBar = new ProgressBar { name = "hero-health" };
            _experienceBar = new ProgressBar { name = "hero-experience" };
            _healthBar.AddToClassList("health-bar");
            _experienceBar.AddToClassList("experience-bar");
            hud.Add(_healthBar);
            hud.Add(_experienceBar);
            _resourcesLabel = Text(hud, "", "hero-resources", "muted");
            _targetLabel = Text(hud, "", "combat-target");

            _panelHost = new VisualElement { pickingMode = PickingMode.Ignore };
            _panelHost.AddToClassList("panel-host");
            _interface.Add(_panelHost);

            _feedbackLabel = Text(_interface, "", "game-feedback", "feedback");
            _feedbackLabel.pickingMode = PickingMode.Ignore;
            var actions = Row(_interface, "action-bar");
            ButtonOf(actions, "Inventory [B/I]", "open-inventory", ToggleInventory);
            ButtonOf(actions, "Skills [K]", "open-skills", () => TogglePanel(GamePanel.Skills));
            ButtonOf(actions, "Journal [J]", "open-journal", () => TogglePanel(GamePanel.Journal));
            ButtonOf(actions, "Map [M]", "open-map", () => TogglePanel(GamePanel.Map));
            _potionButton = ButtonOf(actions, "Potion [Q]", "use-potion", UsePotionShortcut);
            _interactButton = ButtonOf(actions, "Interact [E]", "interact", InteractFromInterface);
            ButtonOf(actions, "Menu [Esc]", "system-menu", ToggleSystemMenu);
        }

        private void RefreshHud()
        {
            HeroState hero = _session.Hero;
            var progress = hero.Sheet.Progression;
            int minutes = _session.Clock.WorldTimeMs / 60_000;
            SetText(_heroLabel, $"{hero.Name}  ·  {hero.Sheet.Class}  ·  Level {progress.Level}");
            SetBar(_healthBar, hero.Health.CurrentHealth, hero.Health.MaxHealth, "HP");
            SetBar(_experienceBar, progress.Experience, progress.ExperienceToNextLevel, "XP");
            string movement = HeroDead ? "Downed" : !_motor.enabled ? "Menu open" : _motor.Swimming ? "Swimming"
                : _motor.Climbing ? "Climbing" : _motor.Gliding ? "Gliding"
                : _motor.Sprinting && _motor.HorizontalSpeed > 0.1f ? "Sprinting" : "Ready";
            SetText(_resourcesLabel, $"{movement}  ·  {hero.Copper} copper  ·  {hero.ManaCubes} mana cubes  ·  {minutes / 60:00}:{minutes % 60:00}");
            if (!_lastCombatTarget.IsNone && _session.Combat.TryGet(_lastCombatTarget, out Combatant target))
            {
                SetText(_targetLabel, $"{target.Name}  ·  {target.Health.CurrentHealth:0}/{target.Health.MaxHealth:0} HP");
            }
            else
            {
                ItemInstance weapon = hero.Loadout.GetEquipped(EquipmentSlot.MainHand);
                string equipped = weapon == null ? "Unarmed" : RawItemName(new RawAlphaItemId((byte)weapon.Type, weapon.Subtype));
                SetText(_targetLabel, $"{equipped}  ·  Attack [LMB]");
            }

            SetText(_potionButton, $"Potion ×{hero.Inventory.CountOf(LootRules.LifePotion)} [Q]");
            _potionButton.SetEnabled(!HeroDead && _panel == GamePanel.None);
            _interactButton.SetEnabled(!_interactionTarget.IsNone);
            bool npc = !_interactionTarget.IsNone && TryTownNpc(_interactionTarget, out _, out _);
            SetText(_interactButton, _interactionTarget.IsNone ? "Interact [E]" : npc ? "Talk [E]" : "Open chest [E]");
            while (_feedback.Count > 0 && _feedback.Peek().Until <= Time.unscaledTime)
            {
                _feedback.Dequeue();
            }

            string message = HeroDead ? "You fell. Returning to the world start…" :
                string.Join("\n", _feedback.Select(notice => notice.Message));
            SetText(_feedbackLabel, message);
            _feedbackLabel.style.display = message.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void Notify(string message)
        {
            if (_feedback.Count == 4) _feedback.Dequeue();
            _feedback.Enqueue((message, Time.unscaledTime + 5f));
        }

        private void PresentHit(HitResolved hit)
        {
            bool incoming = hit.Target.Equals(StableEntityId.Hero);
            if (!incoming && !hit.Attacker.Equals(StableEntityId.Hero))
            {
                return;
            }
            _lastCombatTarget = incoming ? hit.Attacker : hit.Target;
            if (!hit.Blocked && hit.Damage <= 0)
            {
                Notify(incoming ? "Enemy missed" : "Attack missed");
                return;
            }
            string direction = incoming ? "Received" : "Dealt";
            Notify(hit.Blocked ? "Attack blocked" : $"{direction} {hit.Damage:0.#} damage{(hit.Critical ? " · critical" : "")}");
            // CUBEGAME_CONTRACT: generic resolved hits use an Alpha sample. Weapon/creature selection is separate debt.
            _audio.Play(hit.Blocked ? "block.wav" : "hit.wav");
        }

        private void PresentExperience(ExperienceGained gained)
        {
            Notify(gained.LevelsGained > 0 ? $"+{gained.Amount} XP  ·  Level {gained.Level}" : $"+{gained.Amount} XP");
            if (gained.LevelsGained > 0)
            {
                _audio.Play("levelup2.wav");
                if (_panel == GamePanel.Skills) InvalidatePanel();
            }
        }

        private void PresentPickup(WorldItemRemoved removed)
        {
            if (removed.PickedBy.Equals(StableEntityId.Hero))
            {
                Notify($"Picked up {RawItemName(removed.Raw)} ×{removed.Count}");
                _audio.Play("pickup.wav");
            }
        }

        private void PresentItemUse(HeroItemUsed used)
        {
            Notify(used.HealthRestored > 0 ? $"Restored {used.HealthRestored:0.#} HP" : $"Used {RawItemName(used.Raw)}");
            if (used.Raw.Equals(LootRules.LifePotion)) _audio.Play("drink2.wav");
        }

        private void PresentHunt(HuntChanged hunt)
        {
            Notify(hunt.Complete ? $"Hunt completed · {KindName(hunt.Kind)}" : $"Hunt discovered · {KindName(hunt.Kind)}");
            if (_panel == GamePanel.Journal || _panel == GamePanel.Skills || _panel == GamePanel.Map) InvalidatePanel();
            if (hunt.Complete) _audio.Play(AlphaSoundCatalog.MissionComplete);
        }

        private void PresentDeath(EntityDied died)
        {
            if (died.Killer.Equals(StableEntityId.Hero))
            {
                Notify("Target defeated");
            }
        }

        private void BuildPanel(bool preserveScroll)
        {
            Vector2 offset = preserveScroll ? _panelHost.Q<ScrollView>()?.scrollOffset ?? Vector2.zero : Vector2.zero;
            _panelHost.Clear();
            _statusLabel = null;
            if (_panel == GamePanel.None)
            {
                return;
            }

            var panel = Surface("game-panel", "game-panel");
            if (_panel == GamePanel.Map) panel.AddToClassList("map-panel");
            _panelHost.Add(panel);
            var title = Row(panel, "panel-title");
            Text(title, _panel switch
            {
                GamePanel.Town => "Town services",
                GamePanel.Journal => "Mission journal",
                GamePanel.Map => "Local map",
                _ => _panel.ToString(),
            }, "panel-heading", "heading");
            ButtonOf(title, "Close [Esc]", "close-panel", () => ShowPanel(GamePanel.None));
            _statusLabel = Text(panel, "", "panel-status", "muted");
            if (_panel == GamePanel.Map)
            {
                BuildMap(panel);
                return;
            }

            var scroll = new ScrollView { name = "panel-scroll" };
            panel.Add(scroll);
            switch (_panel)
            {
                case GamePanel.Inventory: BuildInventory(scroll); break;
                case GamePanel.Skills: BuildSkills(scroll); break;
                case GamePanel.Town: BuildTown(scroll); break;
                case GamePanel.Journal: BuildJournal(scroll); break;
            }
            if (offset != Vector2.zero)
            {
                scroll.schedule.Execute(() => scroll.scrollOffset = offset);
            }
        }

        private void BuildInventory(VisualElement body)
        {
            HeroState hero = _session.Hero;
            HeroInventory inventory = hero.Inventory;
            Text(body, $"{inventory.UsedSlots}/{HeroInventory.Capacity} bag slots  ·  {hero.Copper} copper", "bag-summary", "muted");
            ButtonOf(body, "Brew Life Potion · 1 Heartflower + 1 Water Flask", "brew", () => TryBrewLifePotion());
            EquipmentStatSummary stats = EquipmentStats.Compute(hero.Loadout);
            Text(body, "Equipment", null, "section");
            Text(body, $"{hero.Health.MaxHealth:0} max HP  ·  {hero.Sheet.WeaponDamage + stats.WeaponDamageBonus:0.0} base damage  ·  {stats.TotalBlockValue:0.0} block",
                "equipment-stats", "muted");
            foreach (EquipmentSlot slot in EquipmentLoadout.Slots)
            {
                ItemInstance item = hero.Loadout.GetEquipped(slot);
                var row = ItemRow(body, item == null ? null : InventoryEntry.FromCore(item), slot.ToString());
                if (item != null)
                {
                    ButtonOf(row, "Unequip", $"unequip-{slot}", () =>
                        InventoryResult(inventory.TryUnequip(slot), "Equipment moved to bag"));
                }
            }

            foreach (ExtraEquipmentSlot slot in HeroInventory.ExtraSlots)
            {
                InventoryEntry entry = inventory.GetExtra(slot);
                var row = ItemRow(body, entry, slot.ToString());
                if (entry != null)
                {
                    ButtonOf(row, "Unequip", $"unequip-{slot}", () =>
                        InventoryResult(inventory.TryUnequipExtra(slot), "Equipment moved to bag"));
                }
            }

            Text(body, "Bags", null, "section");
            for (int i = 0; i < HeroInventory.Capacity; i++)
            {
                InventoryEntry entry = inventory[i];
                if (entry == null) continue;
                int slot = i;
                VisualElement row = ItemRow(body, entry, $"Bag {i / HeroInventory.SlotsPerBag + 1} · {i % HeroInventory.SlotsPerBag + 1}");
                row.name = $"bag-slot-{slot}";
                bool extra = false;
                foreach (ExtraEquipmentSlot destination in HeroInventory.ExtraSlots)
                {
                    if (!HeroInventory.CanEquipExtra(destination, entry)) continue;
                    extra = true;
                    string label = destination == ExtraEquipmentSlot.LeftRing ? "Left ring"
                        : destination == ExtraEquipmentSlot.RightRing ? "Right ring" : "Equip";
                    ButtonOf(row, label, $"equip-{slot}-{destination}", () => WithBagEntry(slot, entry, () =>
                        InventoryResult(inventory.TryEquipExtra(slot, destination), "Item equipped")));
                }
                if (!extra && entry.IsCore)
                {
                    ButtonOf(row, "Equip", $"equip-{slot}", () => WithBagEntry(slot, entry, () =>
                        InventoryResult(inventory.TryEquip(slot, out _), "Item equipped")));
                }
                else if (entry.Raw.Type == 1)
                {
                    ButtonOf(row, "Use", $"use-{slot}", () => WithBagEntry(slot, entry, () =>
                    {
                        if (!_session.TryUseItem(slot)) Report(false, _session.LastRejection);
                    }));
                }
            }
        }

        private void InventoryResult(bool success, string message)
        {
            Report(success, success ? message : _session.Hero.Inventory.LastRejection);
        }

        private void WithBagEntry(int slot, InventoryEntry entry, Action action)
        {
            if (ReferenceEquals(_session.Hero?.Inventory[slot], entry))
            {
                action();
            }
            else
            {
                Report(false, "The bag changed. Select the item again.");
                InvalidatePanel();
            }
        }

        private void BuildSkills(VisualElement body)
        {
            HeroState hero = _session.Hero;
            int level = hero.Sheet.Progression.Level;
            var draft = _skillDraft;
            Text(body, $"{Math.Max(0L, draft.UnspentLevelPoints(level, hero.ManaCubes))} points available", "skill-budget", "muted");
            ButtonOf(body, $"Specialization · {SpecializationName(hero.Sheet.Class, _specializationDraft)}", "specialization", () =>
            {
                _specializationDraft = _specializationDraft == CharacterSpecialization.Primary
                    ? CharacterSpecialization.Secondary : CharacterSpecialization.Primary;
                InvalidatePanel();
            });
            for (int i = 0; i < HeroSkillRanks.Count; i++)
            {
                var skill = (HeroSkill)i;
                var row = Row(body, "item-row");
                Text(row, $"{skill}  ·  {draft[skill]}", $"rank-{skill}", "grow");
                ButtonOf(row, "−", $"lower-{skill}", () => { draft.TryLower(skill); InvalidatePanel(); }).SetEnabled(draft[skill] > 0);
                ButtonOf(row, "+", $"raise-{skill}", () => { draft.TryRaise(skill, level, hero.ManaCubes); InvalidatePanel(); })
                    .SetEnabled(draft.CanRaise(skill, level, hero.ManaCubes));
            }

            bool valid = hero.SkillRanks.TryPriceChange(draft, level, hero.ManaCubes,
                _specializationDraft != hero.Sheet.Specialization, out long price);
            Text(body, valid ? $"Apply for {price} copper  ·  Wallet {hero.Copper}" : "Skill allocation is incomplete", "skill-cost", "muted");
            if (valid && price > 0) Text(body, $"Requires a nearby {hero.Sheet.Class} trainer.", null, "muted");
            var controls = Row(body);
            ButtonOf(controls, "Apply", "apply-skills", () =>
            {
                if (TryApplyHeroProgression(draft, _specializationDraft))
                {
                    BeginSkillDraft(hero);
                    ApplyHeroMovementState(hero);
                    InvalidatePanel();
                }
            }).SetEnabled(valid);
            ButtonOf(controls, "Discard draft", "discard-skills", () => { BeginSkillDraft(hero); InvalidatePanel(); });
        }

        private void BuildTown(VisualElement body)
        {
            if (!TryTownNpcInReach(_townNpc, out int role)) return;
            HeroState hero = _session.Hero;
            if (AlphaShop.IsVendor(role))
            {
                Text(body, $"Merchant  ·  {hero.Copper} copper  ·  {hero.Platinum} platinum", "vendor-wallet", "section");
                var stock = VendorStock(_townNpc, role);
                for (int i = 0; i < stock.Count; i++)
                {
                    int offer = i;
                    var item = InventoryEntry.FromAlpha(stock[i]);
                    var row = ItemRow(body, item, $"For sale · L{stock[i].Level}");
                    ButtonOf(row, $"Buy · {AlphaTradeValue.Of(item)} copper", $"buy-{offer}", () => TryBuyFromVendor(offer));
                }
                Text(body, "Buyback", null, "section");
                for (int i = 0; i < _session.Buyback.Count; i++)
                {
                    int index = i;
                    var item = _session.Buyback[i];
                    var row = ItemRow(body, item, "Sold");
                    ButtonOf(row, $"Buy back · {AlphaTradeValue.Of(item)} copper", $"buyback-{index}", () => TryBuyBackFromVendor(index));
                }
                Text(body, "Sell one from a stack", null, "section");
                int heroLevel = hero.Sheet.Progression.Level;
                for (int i = 0; i < HeroInventory.Capacity; i++)
                {
                    InventoryEntry item = hero.Inventory[i];
                    if (item == null) continue;
                    int slot = i;
                    var row = ItemRow(body, item, $"Bag {i / HeroInventory.SlotsPerBag + 1} · {i % HeroInventory.SlotsPerBag + 1}");
                    ButtonOf(row, $"Sell · {AlphaTradeValue.Of(item)} copper", $"sell-{slot}",
                        () => WithBagEntry(slot, item, () => TrySellToVendor(slot)));
                    if (!item.IsCore && item.Record.Type == AlphaLoot.TypeLeftovers)
                    {
                        ButtonOf(row, $"Identify · {AlphaShop.IdentifyPrice(item)} copper", $"identify-{slot}",
                            () => WithBagEntry(slot, item, () => TryIdentifyAtVendor(slot)));
                    }
                    if (item.IsCore && item.Core.Level < heroLevel)
                    {
                        ButtonOf(row, $"Adapt · {AlphaShop.AdaptPrice(item.Core.Level, item.Core.Rarity, heroLevel)} platinum", $"adapt-{slot}",
                            () => WithBagEntry(slot, item, () => TryAdaptAtVendor(slot)));
                    }
                }
            }
            else if (role == TownNpcPopulation.InnkeeperClass)
            {
                Text(body, "Innkeeper", null, "section");
                Text(body, "Rest available from 18:00 to 06:00.", null, "muted");
                ButtonOf(body, "Rest until 07:00", "rest", () => TryRestAtInn());
            }
            else
            {
                CharacterClass trainer = CharacterClasses.FromId(role - 1);
                Text(body, $"{trainer} trainer", null, "section");
                if (trainer == hero.Sheet.Class)
                {
                    ButtonOf(body, "Review specialization and ranks", "trainer-skills", () => ShowPanel(GamePanel.Skills));
                }
                else
                {
                    Text(body, $"Find a {hero.Sheet.Class} trainer for paid changes.", null, "muted");
                }
            }
        }

        private void BuildJournal(VisualElement body)
        {
            Text(body, "Regional hunts", null, "section");
            Text(body, "Approach a hostile creature to discover a hunt.", null, "muted");
            var hunts = _session.Hero.Missions.Hunts;
            foreach (bool complete in new[] { false, true })
            {
                Text(body, complete ? "Completed" : "Active", null, "section");
                bool any = false;
                foreach (HuntMission hunt in hunts)
                {
                    if (hunt.Complete != complete) continue;
                    any = true;
                    Text(body, $"{KindName(hunt.Kind)}  ·  Region cell {hunt.CellX}, {hunt.CellZ}",
                        $"hunt-{hunt.Target.Value}", complete ? "hunt-complete" : "hunt-active");
                }
                if (!any) Text(body, "No entries", null, "muted");
            }
        }

        private static string KindName(KindId id) =>
            AlphaCreatureCatalog.TryKind(id.Value, out var kind) && !string.IsNullOrEmpty(kind.NameHint)
                ? kind.NameHint : id.ToString();

        private static string RawItemName(RawAlphaItemId id)
        {
            if (id.Equals(LootRules.LifePotion)) return "Life Potion";
            if (id.Equals(AlphaLifePotionRecipe.Heartflower)) return "Heartflower";
            if (id.Equals(AlphaLifePotionRecipe.WaterFlask)) return "Water Flask";
            if (AlphaItemIcons.TryEntry(id, out string name)) return name.Replace('-', ' ');
            if (id.Type == (byte)ItemType.Weapon) return ((WeaponSubtype)id.Subtype).ToString();
            return id.Type <= byte.MaxValue && Enum.IsDefined(typeof(ItemType), (byte)id.Type)
                ? ((ItemType)id.Type).ToString() : $"Item {id}";
        }

        private VisualElement ItemRow(VisualElement parent, InventoryEntry entry, string location)
        {
            var row = Row(parent, "item-row");
            var itemText = new VisualElement();
            itemText.AddToClassList("grow");
            if (entry == null)
            {
                Text(itemText, $"{location}  ·  Empty", null, "muted");
            }
            else
            {
                ItemInstance core = entry.Core;
                string model = GameVisualSink.GroundItemModelName(entry.Raw, (int)(core?.Modifier ?? ItemModifier.None),
                    core?.Rarity ?? 0, core?.Seed ?? 0);
                string key = AlphaItemIcons.EntryOf(model);
                if (_itemIcons == null && !_itemIconsChecked)
                {
                    _itemIconsChecked = true;
                    AlphaIconAtlas.TryLoad(AlphaIconAtlas.DefaultDirectory, out _itemIcons, null);
                }

                if (key != null && _itemIcons?.TryGetSprite(key) is Sprite icon)
                {
                    var image = new Image { sprite = icon, scaleMode = ScaleMode.ScaleToFit };
                    image.AddToClassList("item-icon");
                    row.Add(image);
                }
                Text(itemText, $"{RawItemName(entry.Raw)} ×{core?.StackCount ?? entry.Count}", null, "item-name");
                Text(itemText, core == null ? location : $"{location}  ·  L{core.Level}  ·  R{core.Rarity}{ItemStat(core)}", null, "muted");
            }
            row.Add(itemText);
            return row;
        }

        private bool _itemIconsChecked;

        private static string ItemStat(ItemInstance item)
        {
            if (item.Type == ItemType.Weapon)
            {
                return $"  ·  {ItemStatsCalculator.WeaponDamage(item.Level, item.Rarity, (WeaponSubtype)item.Subtype, item.PowerBonus):0.0} damage";
            }
            if (ItemStatsCalculator.IsArmorType(item.Type))
            {
                return $"  ·  {ItemStatsCalculator.Armor(item.Level, item.Rarity, item.Type, item.Modifier, item.Seed, item.PowerBonus):0.0} armor";
            }
            return "";
        }

        private static VisualElement Surface(string name, string style)
        {
            var element = new VisualElement { name = name };
            element.AddToClassList("surface");
            element.AddToClassList(style);
            return element;
        }

        private static VisualElement Row(VisualElement parent, string style = null)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            if (style != null) row.AddToClassList(style);
            parent.Add(row);
            return row;
        }

        private static Label Text(VisualElement parent, string text, string name = null, string style = null)
        {
            var label = new Label(text) { name = name };
            if (style != null) label.AddToClassList(style);
            parent.Add(label);
            return label;
        }

        private static Button ButtonOf(VisualElement parent, string title, string name, Action action)
        {
            var button = new Button(action) { text = title, name = name };
            parent.Add(button);
            return button;
        }

        private static void SetBar(ProgressBar bar, double current, double max, string caption)
        {
            bar.highValue = (float)max;
            bar.value = (float)current;
            string title = $"{caption}  {current:0}/{max:0}";
            if (bar.title != title) bar.title = title;
        }

        private static void SetText(TextElement element, string text)
        {
            if (element != null && element.text != text) element.text = text;
        }
    }
}
