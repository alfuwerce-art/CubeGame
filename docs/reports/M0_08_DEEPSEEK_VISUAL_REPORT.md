# D86 M0-08A / M0-03C — внешность и иконки из Alpha-геометрии (партия DeepSeek, №5)

**TASK:** D86 (issue #13): визуальные системы в зоне DeepSeek — цвет волос по доказанному закону Alpha (D61), иконки предметов для UI из той же Alpha-геометрии (без перерисовки), привязка расы/вида к подготовленному актору без подмены.

**STATUS: `READY_FOR_REVIEW` — не `ACCEPTED`.**

**Ветка:** `deepseek/d85-visual-unity` (после `5855219`). **Worktree:** `D:\Projects\CubeGame-deepseek`. **Unity:** 6000.3.27f2.

---

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Цвет волос | закон D61 только в отчётах/пилоте | `AlphaAppearance.TrySetHairColour` (Presentation/Visual): tint части `hair` через `AlphaRig.SetTint`, проверка диапазона 0…1, отказ `NO_HAIR_PART`/`OUT_OF_RANGE` |
| Иконки предметов | не было | `AlphaItemIconRenderer`: рендер `.cubdec` тем же мешером/материалом в `Sprite` 16…512 px, прозрачный фон, `ICON_FILE_MISSING`/`ICON_MESH` без падений |
| Раса/вид игрока | сопоставление только в CreatureVisualCatalog | `AlphaAppearance.TryResolveRaceActor` — публичный шов для создания персонажа, без подмены отсутствующего вида |
| Тесты | — | +2 PlayMode (28/28 всего), EditMode без регрессий (337/337) |

---

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Presentation/Visual/CubeGame.Presentation.Visual.asmdef` | модуль визуальных утилит (AlphaModels + Creatures + Content) |
| `Assets/CubeGame/Presentation/Visual/AlphaAppearance.cs` | `AlphaAppearance` (hair tint по D61, resolve race actor) + `AlphaItemIconRenderer` (иконки из Alpha-модели) |
| `Assets/CubeGame/Tests/Visual/PlayMode/CubeGame.Tests.Visual.PlayMode.asmdef` | тестовая сборка |
| `Assets/CubeGame/Tests/Visual/PlayMode/AlphaVisualPlayModeTests.cs` | tint волос реального человека (значение + property block), отказ вне диапазона; иконка iron-sword 64×64 с непрозрачными пикселями, отказ для отсутствующего файла |

---

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| EditMode | `total=337 passed=337 failed=0` |
| PlayMode | `total=28 passed=28 failed=0` (+2 новых) |

---

## 4. ALPHA_REFERENCE + DELTA

| Элемент | Доказательство | Реализация |
|---|---|---|
| tint волос = `hair_rgb/255` | D61 (uniform30 = materialColor в VS c32), live hair mesh 2244 вершины | `SetTint(hair, colour)`, проверено property block’ом |
| Иконки | `item_icon_id` → модель (D64/D65); геометрия и мешер приняты | рендер Alpha-модели, а не нарисованный арт |
| Раса → модель | D65/D69 каталог видов; локально подготовлены 5 вариантов | `TryResolveRaceActor`, иначе `MISSING_VISUAL_CONTENT` |

---

## 5. PARTIAL / UNKNOWN

- **VFX/эффекты R-3, снаряды, динамические части R-8** — по-прежнему `UNKNOWN`: в этой партии ничего не имитировалось.
- **Аудио** — данных Alpha о звуках в подтверждённом виде нет (`UNKNOWN`), звуковая система не создавалась.
- **Иконки** есть как рантайм-рендер; импорт готовых PNG-иконок/атласов и UI-раскладка — M0-08A за Claude.
- **Цвет волос игрока** применяется как Alpha-тинт; UI-выбор цвета и стартовые значения — за созданием персонажа (M0-03A).
- **Расы**: только человек-воин подготовлен как визуальный актор; эльфы/прочие расы используют тот же motion family, но отдельные модели не подготовлены в этой партии.

---

## 6. Handoff

`AlphaAppearance.TrySetHairColour(view.Rig, colour, out reason)`, `AlphaAppearance.TryResolveRaceActor(kind, library, out actorId, out reason)`, `AlphaItemIconRenderer.RenderFromFile(path, size, problems)` — [D86_DEEPSEEK_HANDOFF_M0_08](D86_DEEPSEEK_HANDOFF_M0_08.md).

Оставшиеся системы DeepSeek: VFX/снаряды (после доказательств R-3), аудио (после данных), полный предметный слой левой руки/расходников, расширение визуалов видов, M0-09 покрытие и производительность.
