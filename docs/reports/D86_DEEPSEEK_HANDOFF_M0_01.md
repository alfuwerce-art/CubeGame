# D86 — handoff DeepSeek → Claude: визуальный слой Alpha (M0-01)

**Дата:** 2026-11-09. **Ветка:** `deepseek/d85-visual-unity`. **Отчёт:** [M0_01_DEEPSEEK_RUNTIME_REPORT](M0_01_DEEPSEEK_RUNTIME_REPORT.md).
**Статус:** `READY_FOR_REVIEW`; общая сцена, Core и сейвы не менялись. Это опубликованный контракт, по которому Claude может подключать визуал к единой игре.

## 1. Что уже есть в основном Unity-проекте

| Модуль | asmdef | Что даёт |
|---|---|---|
| `Assets/CubeGame/AlphaModels` | `CubeGame.AlphaModels` (runtime) | риг/клипы/контроллер Alpha, загрузка локального кэша, `AlphaActorView` |
| `Assets/CubeGame/Content` | `CubeGame.Content` (runtime, без UnityEngine) | таблицы Alpha: предметы/подтипы/способности/регионы/виды/слоты/действия |
| `Assets/CubeGame/AlphaModels.Import` | `CubeGame.AlphaModels.Import` (Editor) | материалы в Resources, проверка кэша, рендер ключевых кадров |

**Данные Alpha в Git не входят.** Для работы визуала нужен локальный кэш (ключи/ассеты Alpha не публикуются):

```
CUBEGAME_ALPHA_CONTENT = <любой путь>
<root>/actors/<actorId>/rig.json, models/*.cubdec, clips/*.json, contract.json, locomotion.json, actions.json
```
Порядок поиска: аргумент → переменная окружения → `<project>/.scratch/content` → `<persistentDataPath>/CubeGame/AlphaContent`.
Если кэша нет — `AlphaContentDiagnostics.Inspect().Diagnosis` даёт точную причину (`ALPHA_CONTENT_NOT_CONFIGURED`), игра не падает.

## 2. Публичный API (только это вызывать из геймплея)

```csharp
using CubeGame.AlphaModels;

// 1. загрузка (один раз; кэшируется)
var lib = AlphaActorLibrary.Shared;
if (!lib.TryGet("d86_human_0x02_greatsword", out var def))
    Debug.LogWarning(lib.Diagnosis);            // точная причина, без исключений

// 2. создание визуала под сущностью (parent — трансформ вашего entity)
var problems = new List<string>();
var view = AlphaActorView.Spawn(def, entityTransform, problems);

// 3. каждый тик игры (единственные часы — ваши; MonoBehaviour.Update у контроллера отключён)
view.ApplyLocomotion(new AlphaLocomotionState {
    SpeedXY = horizontalSpeed,      // Alpha units/s; масштаб OQ-23 OPEN (PLACEHOLDER_NOT_ALPHA)
    Amplitude = walkAmplitude,      // 0 = покой, ~0.19 = ровная ходьба (как в захватах)
    Boost = sprinting, Accelerating = accelerating, OnGround = onGround,
});
view.Tick(deltaSeconds);

// 4. действие по Alpha-коду (только если вид/профиль его реально запускает)
if (!view.TryPlayAction(alphaActionCode, out var reason))
    Debug.Log(reason);              // ACTION_NOT_IN_KIND_PROFILE / ACTIONS_BLOCKED / ACTION_REFUSED

// 5. чтение состояния для UI/логов (не менять!)
string phase = view.Controller.Phase.ToString();     // Locomotion/Stopping/EnteringAction/Action/Recovery/ExitingAction
string action = view.Controller.CurrentAction;       // null, пока действие не началось (сначала стоп)
string[] actions = view.AvailableActions;            // ровно то, что доказано для этого актора
```

**Чего визуальный слой не делает:** не меняет HP, стамину, ману, урон, деньги, инвентарь, сейвы; не решает, попал ли удар; не запускает собственные `Update`-циклы (кроме страховочного режима, если `externalClock` снять — не снимать).

## 3. Таблицы контента (только чтение)

```csharp
AlphaContentDatabase.ItemTypes / WeaponSubtypes / Abilities / RegionTypes / CreatureKinds / ItemSlots / ItemActions
AlphaContentDatabase.TryItemType(3, out var weapon);           // Weapon
AlphaCreatureCatalog.RoleOf(0x78)                              // PassiveObject (26 видов)
AlphaCreatureCatalog.TryActionClipName(kind, action, out clip) // false = вид это действие не запускал
AlphaItemRules.IsTwoHanded(15)                                 // true (15/16/17)
AlphaItemRules.WeaponInHand(action)                            // IN_HAND vs ON_BACK
```
Источник — `tools/alpha_content/build_content_tables.py` из подтверждённых evidence-JSON; `--check` в CI/ревью ловит расхождение. Менять числа в `.g.cs` вручную нельзя: меняется evidence JSON с провенансом.

## 4. Что нужно от Claude (минимальные точки стыковки)

1. **GameClock** → `view.Tick(dt)`; визуал не читает `Time.deltaTime` сам.
2. **StableEntityId / KindId → actorId**: таблица «вид/вариант → id кэша» (`alpha/<kind>_<variant>`); у DeepSeek она появится в M0-06 (реестр спавна), сейчас можно брать имена из `AlphaContentDiagnostics`.
3. **Скорость/амплитуда** из вашего мотора: `AlphaLocomotionState` — единственный вход анимации перемещения.
4. **Код действия** из боевого резолвера (`actionCode` Alpha) → `TryPlayAction`; `false` = играть нечего, это не ошибка боя.
5. **Экипировка**: смена модели оружия в руке/за спине — следующий пункт DeepSeek (M0-04B). Пока `view.Rig.SetWeaponOnBack(bool)` и клип сами ставят оружие.
6. **Не редактировать** `Assets/CubeGame/AlphaModels/**`, `AlphaModels.Import/**`, `Content/**`; изменения контракта — через issue/handoff, не «тихим» коммитом.

## 5. Текущие ограничения (честно)

- Только 5 акторов в локальном кэше (человек-воин, дракон 0x65, белый кот, plainrunner, волк); массовый каталог 156 видов **не** пересобирался (D86: без технической необходимости не повторять 2856 моделей).
- Действия человека проигрываются с оружием за спиной (как в принятых клипах); якорь «в руке» проверен отдельно.
- Эффекты/снаряды/динамические части (R-3/R-8/R-9) отсутствуют.
- Визуальная приёмка масштаба greatsword/пропорций — за продюсером; в этой партии ничего не «улучшалось» относительно принятого S05.

## 6. Проверки, на которые можно ссылаться

`AlphaOutput/editmode.xml` — 315/315; `AlphaOutput/playmode.xml` — 22/22; `AlphaOutput/content_status.txt` — 5 акторов READY; `AlphaOutput/preview.txt` + 12 PNG — реальные кадры. Локальные артефакты (не в Git): `AlphaOutput/`, `.scratch/content/`.
