# D6 шаг 2 — тайлы карты из цветов Alpha (волна 3/5, пакет DeepSeek)

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content` @ `ce07d1a`.
**STATUS: `READY_FOR_REVIEW`.**

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Карта мира | данных для карты не было (иконки/атлас были, тайлы — нет) | `MapTileBuilder`: RGBA32-тайл `size×size`, цвета берутся **из портированных правил Alpha** (`Voxel` поверхности колонки), пустые колонки прозрачные, `FromSurface`/`FromColumn` для сцены |
| Честность | — | `COLORS=ALPHA_DATA (ported surface colour rules); tile size/sampling=CUBEGAME_CONTRACT (Plasma map widget not ported)` — размер тайла и сетка выборки не выдаются за Alpha |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Presentation/Visual/MapTileBuilder.cs` | рендер тайла (RGBA32, строка 0 = низ, порядок Unity), сэмплер поверхности/колонки, проверки размера |
| `Assets/CubeGame/Tests/Creatures/EditMode/MapTileBuilderTests.cs` | 6 тестов: размер/RGBA, прозрачность пустых колонок, детерминизм и координаты выборки, цвет вокселя, отказы, провенанс |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `121/121 passed`, 0 failed (`AlphaOutput/d6b_editmode2.xml`) |
| Полные EditMode/PlayMode | EditMode `539` (533 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

Красный прогон и причина: не был подключён `using CubeGame.Features.World` для `Voxel` — ошибка компиляции, исправлена.

## 4. PARTIAL / UNKNOWN

1. **Виджет карты Plasma** (`ui/`, платана) не портирован: рамка, маркеры игрока/меток, зум — `CUBEGAME_CONTRACT`
   или задача Codex (C9/C10).
2. **Тайлы «открытых» зон и память карты** (какие зоны игрок видел) — состояние игры, не контент.
3. **Палитра** — только собственные цвета поверхности Alpha; ручная роспись карты не делалась.

## 5. Что нужно от Codex (интегратор)

1. Кто рисует карту (C9/C10): взять `MapTileBuilder.Render(size, sampler)` и сделать `Texture2D` из буфера;
   сэмплер — поверхность колонки зоны (`MapTileBuilder.FromColumn`).
2. Если нужен другой размер тайла/зум — параметр `size`, контракт данных не меняется.
