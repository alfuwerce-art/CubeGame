# M0-00A — зависимости и безопасный порядок интеграции (предложение для M0-00C)

**Статус: `PROPOSAL / READY_FOR_REVIEW`** — не утверждённое решение. Не меняет ADR-0001, D44–D47, D66–D69 и
план [M0_FULL_ALPHA_INTEGRATION](M0_FULL_ALPHA_INTEGRATION.md); M0-01 этим документом **не** открывается.
База: SHA `721d222`. Факты — в [аудите M0-00A](../reports/M0_00A_EXISTING_UNITY_ARCHITECTURE_AUDIT.md).

Принцип: **ничего работающего не переписывать**. Принятые `Core` (S02–S04) и рантайм S05 подключаются как есть;
новое — только связующие слои (Features, импорт/адаптеры ID, игровой цикл) и перенос файлов S05 без изменения логики.

## 1. Целевой граф сборок (без циклов)

```text
                         CubeGame.Core  (есть; правила, без UnityEngine)
                        /      |       \
   CubeGame.Persistence (есть)  |   CubeGame.Features  (НОВОЕ: сценарии мира/героя/боя/предметов/NPC,
                        \      |       /                 игровые часы, команды → события; без MonoBehaviour)
                         CubeGame.Presentation (есть; MonoBehaviour, сцена, UI, ввод → команды)
                                 |
                         CubeGame.AlphaModels  (НОВОЕ имя для рантайма S05: AlphaRig, VoxelMesh, клипы,
                                 |              локомоция, контроллер, контракт; БЕЗ ссылок на Core)
                         CubeGame.AlphaModels.Import (НОВОЕ: загрузка локального кэша данных Alpha,
                                                     конвертеры ID Alpha → Core: класс, слот, тип предмета)
   Не в игровой сборке:  CubeGame.AlphaModels.Verification (probes S05), CubeGame.Editor, тесты, просмотрщик
```
Правила направления: `Core` ни на что не ссылается; `Features` → `Core` (+ интерфейсы хранилища из `Core`);
`Presentation` → `Features`, `Core`, `Persistence`, `AlphaModels`; `AlphaModels` → только UnityEngine;
`AlphaModels.Import` → `AlphaModels`, `Core` (только для перевода ID). Проверка — тест на ссылки asmdef.

## 2. Владение состоянием

| Состояние | Владелец | Кто читает | Как меняется |
|---|---|---|---|
| Мир (seed, версия генератора, дельты) | Features/World | Presentation (чанки) | команды → события `ChunkChanged` |
| Герой (класс, уровень, HP, XP) | Core `CharacterSheet` через Features/Hero | HUD, анимации | события `HealthChanged`, `LevelUp` |
| Инвентарь и слоты | Core `EquipmentLoadout` через Features/Items | модели оружия/брони | `ItemEquipped/Unequipped` |
| Бой | Core `Combat*` через Features/Combat | анимации, VFX | `ActionStarted(entity, actionId)`, `HitResolved` |
| Существа/NPC | Features/Creatures (`EntityId`, `kind`, режим S05) | AlphaModels | `CreatureSpawned/Despawned`, `ActionStarted` |
| Анимационная поза | AlphaModels (только отображение) | — | никогда не меняет игровое состояние |
| Время | Features `GameClock` | все | Presentation подаёт `deltaTime` один раз за кадр |

## 3. Безопасный порядок интеграции (внутри утверждённых подэтапов)

1. **M0-01A (структура, без новой логики):** перенести `tools/alpha_models/transfer/unity/Runtime` в
   `Assets/CubeGame/AlphaModels` одной копией с asmdef `CubeGame.AlphaModels`; probes и `Editor/*` — в
   `Verification`/Editor-сборки. `tools/...` остаётся источником для стендов, пока стенды не переведены на
   сборку из `Assets` (один источник истины — решить в M0-00C). Регрессия: полный EditMode/PlayMode S02–S04 +
   D69-проверка на тех же числах (HumanMale 98/98, 35927/2962).
2. **M0-01A:** asmdef `CubeGame.Features` пустой каркас + тест графа ссылок (нет циклов, `Core` без ссылок).
3. **M0-01C:** `GameClock`, команды/события, машина состояний игры (меню → загрузка → игра → выход);
   `PilotBootstrap` становится одной из реализаций состояния «игра», а не точкой связности.
4. **M0-01D:** механизм миграций сейва `vN→vN+1` с тестом «сейв v1 грузится» — **до** добавления новых полей
   (мир, сущности, назначение слотов).
5. **M0-01A/M0-03C:** `AlphaModels.Import` — локальный кэш данных Alpha вне Git, один загрузчик вместо
   `Application.dataPath`; конвертеры ID (R-ID-1…3) с тестами на известных значениях
   (например: сырой класс 3 ↔ `CharacterClass.Mage`).
6. Дальше — по плану M0-02…M0-09; бой подключается к анимациям только через `ActionStarted` (контроллер S05 в
   режиме `externalClock`), а `STATIC_OBJECT`/`LOCOMOTION_ONLY` уважаются Features.

## 4. Предложения к M0-00C / M0-00D

* M0-00C: утвердить граф §1 и таблицу владения §2 (или исправить) — это закрывает R-LIFE, R-TIME, R-PROBE.
* M0-00C: единый модуль `AlphaIds` (класс/спец, слот, тип предмета, `kind`) с документированными источниками;
  сырые значения Alpha живут только в импорте.
* M0-00C: решение «один источник рантайма S05» — после переноса в `Assets` стенды `.scratch` собираются из него
  (иначе две копии разойдутся).
* M0-00D: golden-проверки первых этапов: (а) запуск → новый мир → сохранение → перезапуск → тот же мир и герой;
  (б) экипировать меч → удар → тот же урон, что в `EquipmentCombatIntegrationTests`; (в) HumanMale 98/98 после
  переноса рантайма.
* M0-00D: критерий M0-01A — «ни одна принятая проверка S02–S05 не изменила чисел».

## 5. Вопросы продюсеру

1. Ввод: оставить `UnityEngine.Input` (Input Manager) в M0 или перейти на пакет Input System в M0-01C?
   (влияет на `PlayerMotor`/камеру; переход — отдельная работа).
2. Рантайм S05 после переноса в `Assets`: удалить копию в `tools/alpha_models/transfer/unity` (стенды собираются из
   `Assets`) или сохранить её как источник для стендов? Рекомендация — один источник в `Assets`.
3. Масштаб моделей к миру (R-SCALE): разрешить исследование коэффициента блок↔воксель по коду Alpha как часть
   M0-00B/M0-03C (до доказательства — явная `PLACEHOLDER_NOT_ALPHA`-константа)?
4. Данные Alpha в игровом проекте: локальный кэш, генерируемый из `D:\Games\Cube world` при первом запуске
   редактора (не в Git, D54) — подтверждаете этот путь для M0?
