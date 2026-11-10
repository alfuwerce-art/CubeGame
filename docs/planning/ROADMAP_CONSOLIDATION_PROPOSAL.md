# CubeGame — Master Roadmap (консолидация, предложение)

**Статус:** REVIEW ONLY; не заменяет `docs/planning/ROADMAP_DRAFT.md` и не авторизует будущие этапы. **Основание:** D01–D36, D59–D82, D90, M1/S06 stage concepts. Уточнение выпуска ожидает решения Q2/Q3.

## M0 — ALPHA FOUNDATION / текущий авторизованный контур
Цель: работающая одиночная Unity-основа на механиках Alpha 2013 с явным backlog неполного parity. **D90 governing plan.**
- F1 World Fix — исторический результат в F2 integration.
- F2 Character & Combat — ACCEPTED.
- F3 Gameplay Loop — ACCEPTED.
- **F4 Presentation — ACTIVE по последнему проверенному ACTIVE_STAGE** (audio, HUD, inventory, skills, map, journal).
- F5 Final — Windows build, регрессии, полный путь создания/игры/сейва.
**Контроль:** `docs/ACTIVE_STAGE.md`, `docs/decisions/D90-m0-five-stage-finish-plan.md`, `docs/planning/M0_ALPHA_PARITY_BACKLOG.md`.

## M1 — VISUAL / CONTENT EVOLUTION (будущие этапы, NOT_AUTHORIZED)
- `docs/stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md`: EntityRecipe, Character/Creature Factory, rig archetypes, AnimationResolver, weapon/IK; эталон → семейства → партии.
- `docs/stages/S06_RENDER_ENVIRONMENT_EVOLUTION_STAGE.md`: материалы/текстуры, освещение, растительность, вода, погодный state и реакции мира; сезоны — только архитектурный задел до postlaunch.
- Будущий UI evolution: пользовательский художественный редизайн после M0 presentation baseline; подробности/очередность подлежат отдельной авторизации.
- Art Direction D67 и Production Pipeline: художественные и производственные критерии для каждого пакета.

## M2 — EXPANDED RPG / LIVING WORLD (будущие этапы, NOT_AUTHORIZED)
- Первый игровой час, портативный лагерь, потребности, отношения NPC и связи поселений.
- Боёвка по D65/D66/D70/D71; практика физических навыков и единое магическое дерево.
- Мир: экономика, NPC социальная память, фракции, последствия событий; процедурные квесты D74 + добровольная четырёхактная сюжетная ось D60.
- Заморский континент и редкие Фильдор-NPC — указаны D82 в основной версии, не в postlaunch-каталоге.
- Детальная последовательность зависит от готовых вертикальных срезов и approval; отсутствие связанного implementation spec не означает отмену концепта.

## M3 — NETWORK / RELEASE (будущие, границы выпуска OPEN)
- Пользовательские серверы/моддинг и выпуск в стратегическом направлении D59/D01–D36.
- Q2: отдельный публичный одиночный релиз до сети или единый релиз M3?
- Q3: нужен этап полного континента/кампании до первого публичного релиза или отдельная milestone до финальной версии?

## POSTLAUNCH — APPROVED CONTENT CATALOG, IMPLEMENTATION NOT AUTHORIZED
- D82: новые 9 основных биомов / 41 микробиом; добавочные животные/монстры; сезонные изменения D81.
- Это **не** отложение оригинальных Alpha биомов и существ. Демография/старение D81 не автоматически относится к пострелизному пакету: её срок открыт.

## Разделение ролей документации
- GDD отвечает на вопрос **ЧТО**, этот Roadmap — **КОГДА**, stage spec + ACTIVE_STAGE — **ЧТО СЕЙЧАС РАЗРЕШЕНО**, AGENTS/Production — **КАК**, parity backlog — **ЧТО НЕ ЗАБЫТЬ**.
- Здесь нет новых числовых параметров, даты выпуска или автоматического разрешения разработки.
