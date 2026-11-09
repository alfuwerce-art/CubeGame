# D86 — индекс handoff DeepSeek → Claude (M0-01…M0-09)

**Ветка:** `deepseek/d85-visual-unity`. **Статус:** `READY_FOR_REVIEW` (не ACCEPTED; приёмка — продюсер/ChatGPT).
**Голова на момент индекса:** `b29dd09` + текущая партия. Рабочий репозиторий: `D:\Projects\CubeGame-deepseek`.

## 1. Handoff-файлы

| Файл | Что внутри |
|---|---|
| [M0-01 runtime](D86_DEEPSEEK_HANDOFF_M0_01.md) | actor API `/ walk/idle/action | контент-кэш | воспроизведение |
| [M0-02 мир](D86_DEEPSEEK_HANDOFF_M0_02.md) | W-18 (`AlphaScatterTree`/`AlphaScatterSelector`/`AlphaScatterField`/`AlphaVegetationPlanner`), структуры, данж, сервисы |
| [M0-03 персонаж](D86_DEEPSEEK_HANDOFF_M0_03.md) | `AlphaPlayerVisualBuilder`, дескриптор с бронёй/предметом, `CharacterVisualRequest.Default` |
| [M0-04 предметы](D86_DEEPSEEK_HANDOFF_M0_04.md) | оружие/левая рука/расходники/броня, законы слотов, отказы |
| [M0-06 существа](D86_DEEPSEEK_HANDOFF_M0_06.md) | AI/спавн/действия, тайминг действий, троттлинг, нагрузка/память |
| [M0-08 визуал](D86_DEEPSEEK_HANDOFF_M0_08.md) | тинт, иконки, пакетный рендер |
| [M0-09 витрина](D86_DEEPSEEK_HANDOFF_M0_09.md) | `AlphaContentShowcase`, смоук/память, сводный отчёт |

## 2. Покрытие по этапам (зона DeepSeek)

| Этап | Статус | Факты |
|---|---|---|
| M0-01 | `PARTIAL` | runtime + actor API, 156 акторов кэша, EditMode/PlayMode |
| M0-02 | `PARTIAL` | W-18 транслирован (77 id, `TRANSLATED_FROM_CODE`), размещение, структуры/данж/сервисы; живой сверки нет |
| M0-03 | `PARTIAL` | визуал игрока из дескриптора (раса/волосы/оружие/рука/броня/предмет) |
| M0-04 | `PARTIAL` | 26+10 якорей, 14 удерживаемых, 32 брони, иконки; лампа/пет/плечо — отказ |
| M0-05 | `PARTIAL` | действия 129/130 видов (900 клипов), тайминги, AI→бой; VFX R-3/R-8 — `BLOCKED` |
| M0-06 | `PARTIAL` | 156/156 видов, AI/спавн/декор/ID/blob/высота/нагрузка; RuneGiant R-9 `BLOCKED` |
| M0-07 | `PARTIAL` | 67 объектов, 133 имени, 27 навыков, дома/данж-скелет, сервисы; торговля/задания — Claude |
| M0-08 | `PARTIAL` | тинт, иконки 28 моделей; UI/аудио — Claude/нет данных |
| M0-09 | `PARTIAL EVIDENCE` | витрина, смоук (0.172 мс/кадр), churn (+41.8 МБ, 0 утечек), сводный отчёт; финальная сборка — Claude |

## 3. Воспроизводимость (все зелёные)

```
python -m tools.alpha_content.build_content_tables --check
python -m tools.alpha_content.build_world_evidence --check
python -m tools.alpha_content.build_staged_visuals --check
python -m tools.alpha_content.extract_scatter_table --check
python -m tools.alpha_content.scatter_tree_translate2 --check
python -m tools.alpha_content.build_held_item_models --check
python -m tools.alpha_content.build_equipment_models --check
```

## 4. Известные пределы (честно)

- W-18: код-происхождение, но **не** живая сверка с Alpha; `World_placeObjectWithSpacing` не портирован — позиции `CUBEGAME_CONTRACT`.
- Действия: библиотеки `EMULATED_ALPHA_CODE` (D65/S05A-2/S05B/D83), не live-захват; RuneGiant без действий (R-9).
- Лампа (`lamp`), пет (`pet`), шея/плечо — законы не реализованы, API отказывает, а не рисует.
- Аудио/VFX/снаряды — данных нет (R-3/R-8); торговые правила/задания — зона Claude (F-U5, J-U4/U5).
- Финальная интеграционная сборка/профилирование с GameSession — за Claude; приёмка не запрашивалась.
