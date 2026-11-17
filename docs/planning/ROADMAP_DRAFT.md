# CubeGame — дорожная карта разработки собственной игры

**Наименование:** CubeGame — рабочее название проекта; окончательное название игры не утверждено.

**Статус:** активная дорожная карта разработки собственной игры.  
**Решение продюсера:** 2026-11-17 — прекращается цель полного переноса Cube World Alpha 2013. Уже выполненные исследования, код, тесты и перенесённые механики сохраняются как техническая база и библиотека референсов.  
**Источник игровых требований:** `DECISIONS_001-036.md` и действующие утверждённые проектные решения.  
**GDD:** не пересобирается этим изменением; идея игры и утверждённые игровые решения сохраняются.  
**Alpha 2013:** больше не является спецификацией CubeGame. Она используется как исследовательский и инженерный референс там, где это помогает построить собственную систему.

---

## 1. Основной принцип разработки

CubeGame разрабатывается как самостоятельная игра.

Рабочий цикл:

`ТРЕБОВАНИЕ CUBEGAME → ПРОВЕРКА УЖЕ ГОТОВОГО → RESEARCH ПРИ НЕОБХОДИМОСТИ → СОБСТВЕННЫЙ DESIGN/SPEC → ПРОТОТИП → ИНТЕГРАЦИЯ → PLAYTEST → ПРИЁМКА → МАСШТАБИРОВАНИЕ`.

Исследование Alpha, других игр, GDC, технических статей, papers и открытых реализаций выполняется только для конкретной задачи CubeGame. Исследование не является самоцелью и не блокирует разработку всей игры, если неизвестная деталь не нужна текущему продукту.

### Research Loop

Для любого сложного или неизвестного элемента разрешён отдельный исследовательский контур:

1. **PROBLEM** — что именно требуется CubeGame.
2. **CURRENT STATE** — что уже реализовано и что можно переиспользовать.
3. **CW_ALPHA_REFERENCE** — как соответствующая проблема решена в Alpha, если это полезно.
4. **EXTERNAL RESEARCH** — открытые источники, другие игры, алгоритмы и практики.
5. **CUBEGAME DESIGN** — выбранная собственная архитектура/формула/правило.
6. **PROTOTYPE** — минимальная проверка.
7. **INTEGRATION** — включение в реальный gameplay.
8. **PLAYTEST / METRICS** — проверка качества, производительности и поведения.
9. **LOCK / ITERATE** — принять, изменить или отбросить.

Alpha parity не является критерием PASS, если конкретный механизм явно не выбран как `ALPHA_VERIFIED`.

---

## 2. Категории использования наследия Alpha

Каждая система или элемент относится к одной из трёх категорий:

- **ALPHA_VERIFIED** — намеренно сохраняем конкретный подтверждённый алгоритм, формулу или поведение Alpha.
- **ALPHA_ADAPTED** — используем принцип или результат исследования Alpha, но архитектура и поведение адаптированы под CubeGame.
- **CUBEGAME_NATIVE** — собственная система; Alpha может использоваться только как один из референсов.

Запрещено продолжать reverse engineering только ради закрытия старого parity backlog, если это не требуется активному этапу CubeGame.

---

## 3. Наследие старого M0

Старый **M0 ALPHA FULL UNITY PORT** прекращается как продуктовая цель и переводится в статус:

`LEGACY / RESEARCH BASE / NOT A RELEASE TARGET`.

Сохраняются:

- Unity runtime и модульная архитектура;
- seed/world generation и streaming;
- world/environment prototypes;
- NPC/population/AI исследования;
- combat, items, loot, equipment и progression prototypes;
- crafting, trading и NPC services prototypes;
- save/load и persistence experiments;
- Windows Player проверки;
- EditMode/PlayMode тесты;
- декомпиляция, отчёты, таблицы функций, RNG и state исследования;
- Character/Creature Factory исследования;
- модельный, rig/animation и ComfyUI pipelines.

Больше не требуется:

- точное Alpha menu/HUD parity;
- точное Alpha audio/VFX parity;
- полное восстановление старого save format;
- полное воспроизведение каждой Alpha-функции;
- точные Alpha timing/RNG вне выбранных `ALPHA_VERIFIED` механизмов;
- закрытие всех `UNKNOWN/PARTIAL/PLACEHOLDER_NOT_ALPHA` старого M0.

Старые документы M0 остаются историческими и исследовательскими материалами, но не определяют порядок активной разработки.

---

# 4. Производственная дорожная карта

Разработка организована по стандартной схеме game development:

`PRE-PRODUCTION → CORE PROTOTYPE → VERTICAL SLICE → PRODUCTION → ALPHA → BETA → RELEASE CANDIDATE → RELEASE`.

Каждая стадия обязана завершаться играбельной сборкой и проверкой предыдущего функционала.

---

## G0 — DEVELOPMENT PIVOT / BASELINE FREEZE

**Фаза:** переход от порта к собственной игре.

### Цель

Зафиксировать текущую техническую базу и убрать Alpha parity из активного производственного контура.

### Работы

- заморозить состояние старого M0 как legacy/research baseline;
- составить reuse map уже реализованных систем;
- разделить код на `KEEP / REWORK / RESEARCH / RETIRE`;
- убрать из активных задач обязательное восстановление Alpha UI/audio/VFX/save semantics;
- сохранить все доказательства reverse engineering;
- зафиксировать, что GDD и игровые решения не меняются;
- определить текущую стабильную Unity-базу для дальнейшей разработки.

### PASS

- есть один понятный baseline проекта;
- известно, какие части M0 используются дальше;
- активные задачи больше не требуют полного Alpha parity;
- проект собирается и существующие критические тесты проходят.

---

## G1 — TECHNICAL FOUNDATION

**Фаза:** pre-production / foundation.

### Цель

Превратить уже созданный runtime в устойчивую платформу CubeGame.

### Объём

- bootstrap и game states;
- input abstraction;
- data definitions и stable IDs;
- simulation/update boundaries;
- world loading;
- save/load foundation;
- event/message boundaries;
- diagnostics/logging;
- test infrastructure;
- Windows Player build pipeline;
- профилирование CPU/RAM/GC;
- миграции данных и versioning save-схемы.

### Что переиспользуем

M0-F2/F3/F4, существующие gameplay contracts, save/load, тесты и runtime infrastructure — после аудита качества.

### PASS

Новая игра стабильно запускается, создаёт/загружает собственный save, входит в игровой мир и не зависит от старого Alpha content path как обязательного runtime-компонента.

---

## G2 — WORLD FOUNDATION

**Фаза:** pre-production / core technology.

### Цель

Сделать существующую процедурную генерацию собственным генератором CubeGame.

### Объём

- deterministic seed;
- terrain/height;
- climate;
- biome/zone rules;
- rivers/water bodies;
- chunk/region streaming;
- visibility/culling/LOD strategy;
- world object placement foundation;
- persistent world modifications;
- generation debug tools;
- multi-seed regression tests.

### Принцип

Работающая генерация не переписывается ради отличия от Alpha. То, что уже даёт нужный результат, становится технологией CubeGame. Alpha исследуется только для конкретных отсутствующих правил.

### PASS

Несколько seed воспроизводимы; мир стабильно стримится; изменения игрока сохраняются; генератор допускает будущие новые биомы, структуры и правила без зависимости от Alpha parity.

---

## G3 — CHARACTER & CREATURE FACTORY

**Фаза:** production technology R&D.

**Технические референсы:**  
[M1 — эволюция моделей, ригов и анимаций](../stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md)  
[G3 — Humanoid Character Factory](../stages/G3_HUMANOID_CHARACTER_FACTORY_STAGE.md)  
[D94 — modular humanoid appearance + NPC generation](../decisions/D94-g3-humanoid-modular-appearance.md)

### Цель

Сначала построить производственный контур **всех поддерживаемых гуманоидных рас**, используя общую модульную Factory. Не создавать отдельную полную модель для каждого NPC.

Игрок и NPC используют одну библиотеку внешности:

```text
PLAYER: Race → Character Creator → AppearanceRecipe ┐
                                                    ├→ Humanoid Factory
NPC:    Race → deterministic randomizer → Recipe   ┘
```

NPC appearance randomizer намеренно простой: population выбирает расу и `PersistentNpcId`, затем `WorldSeed + PersistentNpcId` даёт стабильный `AppearanceSeed`, а `RaceAppearanceProfile` разрешает только подходящие этой расе лицо, волосы, бороду, клыки, рога, уши, цвета и другие признаки.

### Порядок G3

- **G3-0 — Humanoid Race Mannequins**  
  Базовые манекены всех поддерживаемых гуманоидных рас и необходимых sex/body variations.

- **G3-1 — Character Editor Visual Library**  
  Полный набор визуальных категорий, реально влияющих на редактор: face/head, hair, beard/moustache, colors и race-specific traits.

- **G3-2 — AppearanceRecipe + Modular Assembly**  
  Один data-driven assembly path игрока и NPC.

- **G3-3 — Race-based NPC Appearance Randomizer**  
  Простой детерминированный выбор разрешённых модулей расы. NPC не создаются отдельными готовыми моделями.

- **G3-4 — Humanoid Rig + Base Animations**  
  Общий Humanoid skeleton/rig contract где совместимо, weights, locomotion и базовые action packages; небольшие race/body corrections при необходимости.

- **G3-5 — Equipment / Armor / Weapons**  
  Одежда, броня и оружие — отдельные модульные слои, не склеенные с базовой внешностью.

- **G3-6 — World NPC Integration**  
  Подключение к G2-1 `EntityDefinition / EntityVisualRecipe`, population и persistence.

### Негуманоидные существа

Creature Factory остаётся общей архитектурой (`Quadruped`, `Floating`, `Avian`, `Insect`, `Arachnid`, `Custom`), но **массовый каталог существ заранее не производится**.

Конкретные существа создаются по фактической необходимости текущего региона, данжа, босса или другого контента. Последовательность `1 → 2 → 5 → 10 → batch` используется как метод проверки качества/масштабируемости, а не как обязательство заранее изготовить все семейства.

### PASS

G3 humanoid foundation принят, когда:

- все поддерживаемые гуманоидные расы имеют принятые mannequin bases;
- редактор располагает race-specific visual libraries;
- Player и NPC получают один и тот же тип `AppearanceRecipe`;
- NPC appearance детерминирован через `WorldSeed + PersistentNpcId`;
- общий Humanoid rig/animation contract работает на принятых расовых телах;
- equipment остаётся модульным отдельным слоем;
- NPC собирается через G2-1 entity seam и сохраняет ту же внешность после unload/load и save/load;
- для разнообразия NPC не требуется хранить отдельную полную модель каждой комбинации.

---

## G4 — CORE COMBAT

**Фаза:** core gameplay prototype.

### Цель

Построить собственную боевую систему CubeGame на основе выбранных механик и исследований.

### Объём

- authoritative action state;
- `decision → animation → hit window → hit detection → damage → reaction → recovery`;
- melee/ranged/magic foundations;
- weapon families;
- block/dodge;
- interruption/stagger/death;
- projectiles;
- resource costs;
- damage/armor/resistance formulas;
- combat AI hooks;
- animation event contracts;
- combat debug visualizers.

### Research

Формулы и поведение Alpha можно извлекать как `ALPHA_VERIFIED` или `ALPHA_ADAPTED`, но старый animation dispatcher и fallback timing не обязаны воспроизводиться.

### PASS

Несколько типов оружия и противников дают устойчивый, тестируемый и приятный боевой цикл в Windows Player; формулы документированы и не зависят от magic numbers без причины.

---

## G5 — NPC & LIVING WORLD FOUNDATION

**Фаза:** core gameplay / simulation.

### Цель

Создать архитектуру живых NPC и существ под требования CubeGame.

### Объём

- spawn/population rules;
- persistent actor identity;
- activation/despawn/respawn;
- state machine / utility/behavior architecture;
- idle/wander/notice/investigate/chase/attack/return;
- navigation/pathfinding;
- home/work/service anchors;
- schedules;
- city roles;
- faction hooks;
- combat behavior;
- off-screen simulation policy;
- save/load NPC state.

### Дальнейшее расширение

- relationships;
- reputation;
- travel;
- social behavior;
- emergent events;
- локальная LLM и память NPC — только на соответствующем позднем этапе и не как источник истины мира.

### PASS

NPC живёт в мире независимо от игрока в пределах выбранной simulation policy, корректно реагирует на игрока и события, сохраняет важное состояние и не ломает streaming.

---

## G6 — RPG, ITEMS & ECONOMY FOUNDATION

**Фаза:** core systems.

### Цель

Сформировать собственную математическую и data-driven основу RPG.

### Объём

- character stats;
- XP/level curve;
- damage/armor/resistance;
- item power;
- rarity;
- affixes/modifiers;
- equipment;
- loot tables;
- enemy rewards;
- crafting;
- merchants;
- pricing;
- buy/sell/buyback;
- services;
- skill/progression hooks.

### Метод

Для спорной формулы: Alpha research + external research + CubeGame balance goals → собственная versioned specification, например `DAMAGE_FORMULA_V1`, `XP_CURVE_V2`.

### PASS

Полный цикл `kill → reward → loot → equip/sell/craft → progression → save/load` работает без ручных заглушек.

---

## G7 — CORE GAMEPLAY PROTOTYPE

**Фаза:** playable prototype.

### Цель

Впервые собрать основные технологии в одну собственную игру.

### Сквозной сценарий

`create character → spawn near settlement → interact with NPC → receive objective/event → explore → fight → loot → equip → return → service/trade → progression → save → exit → load → continue`.

### Ограничение

Качество графики может быть промежуточным. Главный вопрос — работает ли и интересно ли ядро игры.

### PASS

Сквозной цикл проходится в Windows Player без developer shortcuts и критических state-loss ошибок. Продюсер принимает core loop как основу дальнейшей production-разработки.

---

## G8 — VERTICAL SLICE

**Фаза:** pre-production exit / production proof.

### Цель

Создать небольшой, но почти финального качества кусок CubeGame.

### Минимальный состав

- 1 регион;
- несколько biome/landscape situations;
- 1 полноценное поселение/город;
- основные сервисные NPC;
- несколько обычных и элитных противников;
- 1 dungeon;
- 1 boss;
- несколько weapon families;
- рабочая RPG progression;
- UI/HUD;
- save/load;
- sound/VFX;
- модели и анимации production-quality;
- визуал, близкий к утверждённому voxel north-star проекта.

### Визуальный ориентир

Яркий, насыщенный, современный voxel-мир с крупными читаемыми формами, богатой растительностью, качественной водой, освещением и атмосферной глубиной; детализированные voxel-персонажи и архитектура. Это целевая стилистика CubeGame, а не задача копирования конкретной чужой игры или изображения.

### PASS

Vertical Slice можно дать человеку без объяснений и получить репрезентативную оценку игры, визуала, боя, мира, NPC и progression.

---

# PRODUCTION

После принятого Vertical Slice начинается масштабирование уже доказанных технологий. Крупные архитектурные эксперименты здесь должны быть исключением.

---

## G9 — WORLD & VISUAL PRODUCTION

**Технический референс:** [S06 — рендер, материалы, окружение и погода](../stages/S06_RENDER_ENVIRONMENT_EVOLUTION_STAGE.md).

### Объём

- production terrain/biomes;
- forests/vegetation;
- rocks/world props;
- structures;
- caves;
- roads;
- settlements;
- dungeons/landmarks;
- semantic material/surface system;
- texture/material production;
- shader families;
- lighting/shadows/atmosphere;
- modern water;
- wind;
- weather;
- реакция мира на погодные условия;
- foundation под будущую Season System;
- quality presets и performance budgets.

### PASS

Несколько production-регионов выглядят и работают на уровне принятого Vertical Slice; мир остаётся стабильным при длительном перемещении и streaming.

---

## G10 — CHARACTER, CREATURE & ANIMATION PRODUCTION

### Объём

- массовое производство humanoids/creatures;
- расы/пол/телосложения;
- faces/hair/accessories;
- equipment sets;
- enemy families;
- boss models;
- animation families;
- weapon-specific animation variants;
- race/body-type motion variation;
- procedural/IK corrections;
- runtime editor integration.

### Правило масштабирования

Не запускать сотни моделей, пока pipeline не доказан на меньшей выборке.

### PASS

Factory производит контент предсказуемого качества с минимальным ручным исправлением и выдерживает установленный throughput.

---

## G11 — CONTENT, QUESTS & WORLD SYSTEMS

### Цель

Заполнить доказанные системы содержанием.

### Объём

- cities/settlements;
- factions;
- region identities;
- dungeons;
- bosses;
- camps;
- encounters;
- world events;
- exploration rewards;
- quest framework;
- NPC stories;
- regional progression;
- faction/reputation consequences;
- pets и иные утверждённые игровые системы.

### Quest rule

Не строить основной контент на примитивном `collect N / kill N`, если это не оправдано контекстом. Предпочтение — ситуации, выборы, последствия, исследование и взаимодействие с миром.

### PASS

Контент использует общие systems/contracts и не требует уникальных hardcoded костылей для каждого задания или региона.

---

## G12 — UI / UX / AUDIO / VFX PRODUCTION

### Объём

- main menu;
- character creator;
- HUD;
- inventory;
- character/equipment;
- skills;
- crafting;
- merchants/services;
- map/minimap;
- journal/navigation;
- settings;
- controller/input UX;
- combat/world/UI audio;
- music;
- VFX;
- accessibility basics;
- onboarding/tutorialization.

### PASS

Основные пользовательские сценарии понятны без developer knowledge; UX не зависит от старого Alpha UI; presentation соответствует уровню Vertical Slice.

---

# ALPHA / BETA / RELEASE

## G13 — GAME ALPHA

**Определение:** feature complete.

### Требования

- основные игровые системы присутствуют;
- игра проходится сквозным способом;
- world generation, combat, NPC, RPG, content, UI и persistence интегрированы;
- допускаются bugs, balance issues, placeholder content и performance debt;
- новые крупные механики после Alpha требуют отдельного решения.

### PASS

Полный end-to-end playthrough возможен; нет отсутствующей фундаментальной системы, без которой невозможно оценить продукт.

---

## G14 — GAME BETA

**Определение:** content complete.

### Фокус

- bug fixing;
- balance;
- performance;
- UX;
- save corruption/state-loss;
- edge cases;
- long-session stability;
- compatibility;
- input/controller;
- loading/memory;
- regression automation.

### PASS

Контент зафиксирован; blocker/critical defects закрыты по beta criteria; изменения преимущественно исправляют, а не расширяют игру.

---

## G15 — RELEASE CANDIDATE / 1.0

### Финальная проверка

`clean install → launch → create character → world → town → NPC → exploration → combat → loot → dungeon/boss → quests/progression → UI/map → save → exit → load → continue → long session`.

Дополнительно:

- crash/regression tests;
- performance profiles;
- save migration;
- build/release reproducibility;
- hardware/quality presets;
- known issues;
- backup/recovery process.

### PASS

Release Candidate не содержит известных blocker/critical проблем и может стать 1.0 без изменения архитектуры или обязательного контента.

---

# 5. Параллельные производственные направления

Этапы выше — milestones, а не запрет параллельной работы. После G1 несколько дисциплин могут идти одновременно при общей интеграции:

- **Engineering** — runtime, tools, performance, saves.
- **World** — generation, streaming, environment.
- **Gameplay** — combat, progression, economy.
- **AI/Simulation** — NPC, creatures, city life.
- **Character/Creature Factory** — models, rigs, animations.
- **Art/Rendering** — materials, lighting, weather, VFX.
- **Content** — quests, dungeons, encounters, cities.
- **UI/UX**.
- **Audio**.
- **QA/Automation/Playtest**.

Каждый production stream обязан регулярно сходиться в общей playable build. Запрещено месяцами развивать систему изолированно без интеграции с реальной игрой.

---

# 6. Критерий готовности любой системы

Система не считается готовой только потому, что код существует.

Минимальный Definition of Done:

`SPEC/DESIGN → IMPLEMENTED → INTEGRATED → TESTED → WINDOWS PLAYER → SAVE/LOAD WHEN APPLICABLE → PERFORMANCE CHECK WHEN APPLICABLE → PLAYTEST → PRODUCER REVIEW`.

Допустимые статусы:

- `RESEARCH`
- `PROTOTYPE`
- `INTEGRATED`
- `READY_FOR_PRODUCER_REVIEW`
- `ACCEPTED`
- `REWORK`
- `DEFERRED`

Старые M0 статусы `UNKNOWN/PARTIAL/PLACEHOLDER_NOT_ALPHA/CUBEGAME_CONTRACT` сохраняются только в legacy parity-документах и больше не являются глобальными blockers CubeGame.

---

# 7. Ближайший порядок работ

Текущий практический маршрут:

1. **G0** — зафиксировать pivot и reuse map существующего M0.
2. **G1** — стабилизировать технический foundation на текущем Unity-коде.
3. **G2** — принять существующую генерацию как CubeGame World Foundation и исследовать только реальные пробелы.
4. Параллельно открыть **G3 Character/Creature Factory**, поскольку pipeline уже исследуется и нужен Vertical Slice.
5. Довести **G4 Combat**, **G5 NPC/Living World** и **G6 RPG** до интегрируемых собственных систем.
6. Собрать **G7 Core Gameplay Prototype**.
7. После принятия core loop сделать **G8 Vertical Slice**.
8. Только после Vertical Slice переходить к массовому production G9–G12.

Главная ближайшая продуктовая цель — **не “закрыть старый M0”, а получить первый убедительный Vertical Slice собственной CubeGame**.
