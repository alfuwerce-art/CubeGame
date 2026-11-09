# D3 шаг 5 — вырезание данжа в мир (волна 2, пакет DeepSeek)

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content` @ `5a08438`.
**STATUS: `READY_FOR_REVIEW`.**

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Данж в мире | комнаты и сундуки публиковались только как записи спавна, самих полостей не было | `AlphaDungeonCarver` вырезает комнаты (3×3×3, пол сохраняется) и L-коридоры (2 блока высотой) из породы зоны |
| Подключение | — | `AlphaDungeonContentProvider.Populate` считает базовую высоту по центральной колонке зоны и вырезает скелет в той же зоне, где публикует записи; `CarvedVoxels` для диагностики |
| Честность | — | провенанс `PARTIAL: Q-16 room 3×3×3, step 7, L-corridors proven; carving order/width, contents, light UNKNOWN` |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Features/Creatures/AlphaDungeonCarver.cs` | чистое вырезание комнат/коридоров через делегат записи, `Cells()` для тестов |
| `Assets/CubeGame/Features/Creatures/AlphaDungeonContentProvider.cs` | вызов вырезания при населении данж-зоны |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaDungeonCarverTests.cs` | 5 тестов: комнаты и пол, двухблочные коридоры, детерминизм/счёт, уровень пола, провенанс |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `105/105 passed`, 0 failed (`AlphaOutput/d3e_editmode2.xml`) |
| Полные EditMode/PlayMode | EditMode `523` (517 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

Красный прогон и причина: тест требовал «в коридоре выше 2 блоков пусто» и падал на колонках, которые
перекрывает сама комната (3 блока высотой) — проверка ограничена колонками вне комнат.

## 4. PARTIAL / UNKNOWN

1. **Порядок/ширина вырезания, содержимое комнат, свет, встречи** — `UNKNOWN` (Q-16): воксели вырезаются
   по минимальному правилу (комната 3×3×3, коридор 2 в высоту, 1 в ширину).
2. **Высота/вход данжа** в Alpha (надземный/подземный вход) — `UNKNOWN`: базовая высота берётся из
   центральной колонки зоны (`CUBEGAME_CONTRACT`).
3. **Сундуки**: позиции публикуются (Q-17), содержимое — за игрой (OQ-11).

## 5. Что нужно от Codex (ранее Claude, D89)

1. Регистрация провайдеров: `AlphaZoneContentProvider`, `AlphaDungeonContentProvider(predicate)`,
   `AlphaCityContentProvider(predicate)` — после проходов рельефа.
2. `service_object` / `structure_static` / `chest_static` — статичные сущности без ИИ; взаимодействие
   (торговля, сундук, вход в данж) — игровая сторона.
3. Для проверки в сцене: сгенерировать данж-зону — комнаты и коридоры будут вырезаны ниже поверхности.
