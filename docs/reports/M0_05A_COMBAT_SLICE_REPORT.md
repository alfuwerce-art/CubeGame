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
