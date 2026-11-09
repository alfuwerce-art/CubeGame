# D2 шаг 2 — питомцы и патруль (волна 2, пакет DeepSeek)

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content` @ `d7851d5`.
**STATUS: `READY_FOR_REVIEW`.**

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Питомцы | только константы (`CreaturePets`: лимит 2, способность `0x5C`) | `CreaturePetRoster`: учёт питомцев владельца, лимит и способность **проверяются**, дубликаты/пустые id отклоняются, причина отказа через `ExplainSummon` (`PET_LIMIT`, `NOT_SUMMON_ABILITY`) |
| Патруль (N-14) | существо без цели ходило случайным направлением | `PatrolRoute` — детерминированное кольцо точек вокруг точки спавна (джиттер радиуса ±, прибытие 1 блок); `CreatureAi.AttachPatrol` — ИИ идёт по маршруту, `LastReason = "PATROL n"`, при отсутствии маршрута поведение прежнее |
| Честность | — | питомцы: `PROVEN: limit 2 / ability 0x5C`, `UNKNOWN: taming, riding, pet AI, teleport (CQ-09: no teleport)`; патруль: `CUBEGAME_CONTRACT: Alpha patrol/schedule UNKNOWN (N-13/N-14)` — паритет не заявляется |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Features/Creatures/CreaturePetRoster.cs` | учёт питомцев, лимит/способность, диагностика |
| `Assets/CubeGame/Features/Creatures/PatrolRoute.cs` | детерминированный маршрут (кольцо точек), прибытие, переход к следующей точке |
| `Assets/CubeGame/Features/AI/CreatureAi.cs` | `AttachPatrol`, ветка патруля в `Wander` |
| `Assets/CubeGame/Tests/Creatures/EditMode/CreaturePetAndPatrolTests.cs` | 5 тестов: лимит 2, только способность `0x5C`, дубликаты/id, детерминизм и цикл маршрута, радиус, ИИ идёт по патрулю |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `95/95 passed`, 0 failed (`AlphaOutput/d2b_editmode2.xml`) |
| Полные EditMode/PlayMode | EditMode `513` (507 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

Красный прогон и причина: локальный класс внутри метода теста (`CS1519`) — вынесен на уровень класса.

## 4. PARTIAL / UNKNOWN

1. **Приручение, езда, ИИ питомца, телепорт** — `UNKNOWN` (CQ-09: телепорта нет). Реализован только учёт.
2. **Патруль и расписание города** (N-13/N-14) — `CUBEGAME_CONTRACT`: в Alpha не найдены таблица точек,
   правило «день/ночь» и расписание; маршрут строится детерминированно от точки спавна.
3. **Боссы** (N-26/N-28) — фазы и поведение кроме дракона `0x65` остаются `UNKNOWN`; не выдумывались.
4. **Оружие NPC R-1/R-2** — ждёт значений записи спавна (не исполнена); C-29 уже готов принять оружие.

## 5. Что нужно от Claude

1. Питомцы: вызывать `CreaturePetRoster.TryAdd` при успешном применении способности `0x5C` (C4) и
   `Remove` при смерти/отзыве; лимит и отказ уже дают текст причины для интерфейса.
2. Патруль: при создании существа из `SpawnDefinition` можно прикрепить `new PatrolRoute(x, z, radius, 4, seed)`
   и вызвать `((CreatureAi)ai).AttachPatrol(route)`; радиус — `CUBEGAME_CONTRACT`.
