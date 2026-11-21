# M0-05A — первый играбельный бой (Claude, D87)

**Статус:** `READY_FOR_REVIEW`, не `ACCEPTED`. Ветка `integration/m0-all`, Unity 6000.3.27f2.

## Что работает в игре

| Механика | Где | Проверка |
|---|---|---|
| Боевая система на игровых часах: замах → удар → восстановление, один расчёт попадания в начале фазы удара, отказ с причиной (нет атакующего, мёртв, уже действует) | `Features/Combat/CombatSystem.cs` | EditMode `FullM0CombatTests` (10) |
| Урон только через Core S03/S04: `EquipmentCombatAdapter.BuildAttack` → `DamageResolver` → `HealthPool`; seed удара детерминирован (мир, атакующий, цель, номер действия) | `CombatSystem.ResolveHit` | EditMode: независимый пересчёт Core даёт тот же HP |
| Тайминги атаки героя из клипов S05 (DeepSeek), действие 57: замах 790, удар 592, восстановление 10 мс | `Presentation/Gameplay/AlphaActionTimingSource.cs`, `ActionTiming.FromAlpha` | PlayMode-лог: `windup 790 strike 592 recover 10 (EMULATED_ALPHA_CODE)` |
| Герой: стартовый двуручный меч в инвентаре и в руке, текущее HP, атака по ЛКМ, HUD с HP героя и цели | `Features/Session/GameSession.cs`, `GameBootstrap.cs` | PlayMode `FullM0CombatPlayModeTests` |
| Тренировочный волк (вид Alpha 0x1c) перед героем; смерть — одно событие `EntityDied` | `GameBootstrap.SpawnTrainingTarget` | PlayMode: 30 ударов до смерти, 1 событие смерти |
| HP героя в сейве: новое необязательное поле `heroHealth` формата сессии v1; старые сейвы без поля — полное HP | `SessionSaveData`, `GameSlotStore` | EditMode (round-trip + старый формат), PlayMode (урон → сейв → выход → загрузка) |
| События визуальному слою: `ActionStarted` (длительности фаз, ключ `action_057`), `ActionPhaseChanged`, `ActionResolved`, `HitResolved`, `EntityDied`, `EntitySpawned` героя (вид 0x02) и волка | шина `GameEventBus` | EditMode: порядок фаз и длительности |

## Прогон Unity

EditMode **429/431** (2 skipped, 0 failed), PlayMode **55/56** (1 skipped, 0 failed), exit 0.
Логи: `Logs/m005_claude_editmode.xml`, `Logs/m005_claude_playmode.xml` (локально, вне Git).

## Доказательность

| Значение | Статус |
|---|---|
| Фазы действия 57 (790 / 1382 / 1392 мс) | `EMULATED_ALPHA_CODE` (клип S05, `combat_getAttackWindup` / `combat_getAttackTiming`) |
| Формулы урона, защиты, HP | Core S03/S04 (принятые), без копирования |
| Момент засчитывания удара = конец замаха | `PLACEHOLDER_NOT_ALPHA` (точный кадр Alpha не восстановлен, C-28/C-31) |
| Дальность 3 блока, сектор ±70° | `PLACEHOLDER_NOT_ALPHA` (объём удара Alpha не восстановлен) |
| Шанс попадания 1.0, блок 0 | `PLACEHOLDER_NOT_ALPHA` (OQ-01 UNKNOWN, OQ-24 PARTIAL) |
| Стартовое оружие (двуручный меч L1) | `PLACEHOLDER_NOT_ALPHA` (стартовый набор класса Alpha не восстановлен) |
| HP волка = лист Core Warrior L1 | `PLACEHOLDER_NOT_ALPHA` (HP существ — M0-06) |
| Экипировка после загрузки: первое оружие из предметов в правую руку | правило CubeGame до сохранения слотов (SAVE-EQUIPMENT-GAP, M0-04D) |

## Не сделано / ограничения

- **Модели:** герой — капсула, волк — куб (`PLACEHOLDER`). Подключение моделей и анимаций — DeepSeek-2 через `IEntityVisualSink` (события уже публикуются).
- Волк не атакует (ИИ — DeepSeek, M0-06C); тренировочная цель не сохраняется, при загрузке создаётся заново.
- Нет блока, уклонения, комбо, способностей, смерти/возрождения героя (GS-06 UNKNOWN), наград и дропа.
- Максимум HP героя не пересчитывается при получении уровня во время игры (HealthPool создаётся при загрузке).

## Дополнение (Claude, режим одного агента): модели Alpha, ИИ волка, смерть героя

| Механика | Где | Проверка |
|---|---|---|
| Модели S05 в игре: герой (вид 0x02) с `iron-greatsword1`, волк (0x1c); клип действия по `ActionStarted` (`action_057` у героя), ходьба по `EntityMoved`; при отсутствии кэша — заглушки и `DATA_MISSING` | `Presentation/Gameplay/GameVisualSink.cs` | PlayMode `HeroAndWolfUseAlphaModelsAndAttackPlaysAction57`; кадры сборки `.scratch/m0/m0_03c/{front,attack,fight}.png` |
| ИИ существа Alpha (`CreatureAi`, перенос DeepSeek) управляет волком: преследование, атака действием Alpha 37 (замах 1185 мс), урон герою только через Core | `Presentation/Gameplay/CreatureCombatDriver.cs` | EditMode `FullM0CreatureAiTests` (4), PlayMode: 11 укусов героя в сквозном сценарии |
| HP существ по правилу Alpha `CalcCreatureMaxHealth` (база · уровень · 2^(0.25·звёзды), без класса): волк L1 = 100 HP | `Combatant.CreateCreature` | EditMode |
| Смерть героя: атаки и сейв запрещены, через 5 с игрового времени — возрождение с полным HP в стартовой точке мира | `GameSession.ReviveHero`, `GameBootstrap.RespawnHero` | EditMode (нет атак по мёртвому герою, воскрешение) |

Unity 6000.3.27f2: EditMode **433/435**, PlayMode **56/57**, 0 failed; Windows-сборка `BUILD_SUCCEEDED`.

| Значение | Статус |
|---|---|
| Масштаб модели (подгонка героя к росту 1.8, коэффициент 0.5825 на всех) | `PLACEHOLDER_NOT_ALPHA` |
| Скорость/амплитуда ходьбы в визуале | `PLACEHOLDER_NOT_ALPHA` (OQ-23) |
| Радиус агрессии 16, дистанция атаки 2, скорость 3.7 | значения `CreatureAi` (CUBEGAME_CONTRACT / PLACEHOLDER) |
| Выбор атаки существа: первая атака библиотеки (37) | `PLACEHOLDER_NOT_ALPHA` (`CombatBehavior_chooseAttack` C-29 не перенесён) |
| Урон существа (≈1 HP за укус: лист Core без оружия) | `PLACEHOLDER_NOT_ALPHA` (оружие/база урона существ — M0-06B) |
| Возрождение героя (5 с, старт мира, полное HP) | `PLACEHOLDER_NOT_ALPHA` (GS-06 UNKNOWN) |
| Смерть: тело на боку | `PLACEHOLDER_NOT_ALPHA` |
