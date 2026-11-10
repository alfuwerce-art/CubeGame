# M0 — ALPHA FULL UNITY PORT

**Status:** ACTIVE / REWORKED BY D92  
**Decision:** [D92](../decisions/D92-m0-full-alpha-unity-port.md)  
**Implementation checkpoint:** `codex/m0-alpha-code-parity-p1` @ `c1f9f758598d90d76974d352d1a3689ceb04c8cc`  
**Parity registry:** [M0 Alpha Parity Backlog](../planning/M0_ALPHA_PARITY_BACKLOG.md)

## Цель

Полностью перенести одиночную Cube World Alpha 2013 на Unity как связанную игру. Уже выполненные исследования, конвертация моделей/анимаций, world foundation, combat foundation, UI foundation и packaging используются повторно, но ни один прежний checkpoint сам по себе не доказывает parity.

## Общий цикл каждой системы

`SCAN EXISTING → TRACE ALPHA → SPEC → IMPLEMENT → INTEGRATE → WINDOWS PLAYER → COMPARE → FIX → REVIEW`.

Если существующая реализация соответствует Alpha — сохранить. Если это placeholder/contract/fallback — заменить. Если правило неизвестно — исследовать, а не угадывать.

## P0 — BOOT / MENU / INPUT / SAVE SELECTION

Восстановить:
- startup state machine;
- main menu / options / exit;
- character list/create/editor/select;
- world list/create/select;
- back/cancel transitions;
- input context routing;
- связь character ↔ world ↔ saved position;
- переход в runtime.

**PASS:** Player проходит тот же логический flow Alpha; gameplay input не протекает в UI и наоборот; временный смешанный debug-screen отсутствует.

## P1 — WORLD CORE / STREAMING / RENDER DISTANCE

Восстановить по коду:
- coordinate scales: voxel/chunk/zone/cell/region;
- seed/RNG и порядок world generation;
- terrain / water / climate;
- ZoneTile / LandscapeTile semantics;
- load priority;
- generation radius;
- visible radius;
- keep/cache/unload;
- frustum/render distance/camera constraints.

**PASS:** мир не выглядит локальным островком; streaming и unload соответствуют Alpha-правилам, а не ручным Unity budget-параметрам без provenance.

## P2 — ENVIRONMENT / WORLD OBJECTS / STRUCTURES

Восстановить:
- отдельные ID-space actor / world-object / vegetation / item / structure;
- trees: selection, density, placement, scale, rotation, clustering;
- grass / flowers / bushes / rocks / water plants / decor;
- structures и материалы;
- city layout;
- dungeon geometry/content.

**PASS:** нет creature-моделей в роли растительности; неизвестные ID не подменяются actor kind; стартовый мир получает Alpha-плотность и типы окружения.

## P3 — POPULATION / NPC / CREATURE AI

Восстановить:
- spawn creation sites и RNG;
- activation/despawn/respawn;
- factions / roles / classes / equipment;
- NPC schedules / paths / services;
- creature idle/wander/notice/chase/attack/recover/return states;
- state transitions и timers.

**PASS:** существа не являются общим `walk back-and-forth + attack every N ms`; наблюдаемое поведение происходит из восстановленной state machine.

## P4 — AUTHORITATIVE ACTION / COMBAT / ANIMATION

Это критический контур.

Восстановить единую цепочку:

`AI/player intent → Alpha action id/state → windup → animation pose/clip → strike/release window → hit volume/projectile → damage → recovery → next state`.

Требования:
- gameplay и animation используют один action state;
- урон не живёт на независимом fallback timer;
- fallback `400/600/800` не допускается как финальная Alpha parity;
- weapon/action-specific timing;
- melee/projectile/hitscan/area/bomb;
- block/dodge/abilities по доказанному Alpha;
- death/interruption;
- animation sync.

**PASS:** визуальный момент атаки и gameplay hit являются одной системой и проверены на representative hero + creature + ranged actor.

## P5 — CHARACTER / ITEMS / PROGRESSION

Восстановить:
- races/sex/appearance;
- model composition/transforms/tints;
- equipment attachments;
- inventory/item generation;
- loot/chests;
- shops/services;
- crafting;
- XP/levels/classes/specializations/skills;
- mana/resource rules.

**PASS:** item/character loop соответствует Alpha данным и формулам; временные loot/reward tables отсутствуют на основном пути.

## P6 — UI / HUD / MAP / JOURNAL

Исследовать Plasma/data/state и перенести:
- Alpha HUD state and placement semantics;
- HP/MP/XP/portrait;
- action bar;
- crosshair;
- map/minimap;
- inventory;
- skills/crafting/services;
- journal/quest UI;
- menu navigation.

Не требуется сохранять оригинальный renderer Plasma; требуется восстановить структуру, данные, поведение и Alpha baseline presentation в Unity.

**PASS:** UI больше не является generic debug UI; все основные экраны и controls соответствуют Alpha flow.

## P7 — QUESTS / CITIES / DUNGEONS / GAMEPLAY CONTENT

Восстановить:
- mission generation/types/targets/rewards;
- city NPC roles and services;
- dungeon entry/layout/population;
- bosses/chests/rewards;
- pets и другие обязательные одиночные Alpha content systems.

**PASS:** полный игровой loop формируется Alpha правилами, а не демонстрационными охотами/таблицами CubeGame.

## P8 — AUDIO / VFX / TIME / PRESENTATION

Восстановить:
- event→sound mapping;
- spatial/weapon/creature context;
- combat/UI/world sounds;
- Alpha VFX;
- time/daylight and applicable visual states;
- sky/cloud/world presentation baseline, если они принадлежат Alpha runtime/data.

**PASS:** staged assets не просто существуют — вызываются правильными Alpha событиями.

## P9 — PERSISTENCE

Проверить:
- character/world records;
- player position;
- world modifications;
- opened containers;
- NPC/creature persistent state, где это делает Alpha;
- save/exit/load/continue;
- отсутствие duplication/state loss.

CubeGame bridge-format допустим внутри Unity, если поведение пользователя и семантика состояния соответствуют Alpha.

## P10 — FINAL PARITY RUN

Обязательный Windows Player scenario:

`launch → menu → select/create character → select/create world → enter → travel/stream → environment → NPC → creature combat → loot/equip → city/service → dungeon/boss/chest → quest/progression → inventory/map/HUD → save → exit → load → continue`.

Финальная проверка:
1. полный EditMode/PlayMode;
2. no critical runtime errors;
3. no known placeholder on required path;
4. parity registry без blocking debts;
5. representative side-by-side visual/gameplay review;
6. producer acceptance.

## Системный статус

Для каждого P-блока использовать:

- `NOT_STARTED`
- `RESEARCH`
- `PARTIAL`
- `READY_FOR_PRODUCER_REVIEW`
- `PASS` только после решения продюсера.

Внутри evidence по-прежнему использовать `ALPHA_CODE_STATIC / ALPHA_DECOMPILED / EMULATED_ALPHA_CODE / CUBEGAME_CONTRACT / PLACEHOLDER_NOT_ALPHA / UNKNOWN`.

## Запреты

- Не закрывать систему по количеству тестов.
- Не считать наличие модели переносом поведения.
- Не маскировать UNKNOWN fallback'ом.
- Не смешивать ID-space разных подсистем.
- Не писать tooling вместо игры.
- Не начинать M1/M2 эволюцию до принятия M0.
- Не менять оригинальную Alpha.
- Не публиковать Alpha assets.

## Текущий вход

P1 Codex уже дал полезные факты по menu flow, input и streaming priority, но общий результат `PARTIAL`. Recovery Claude/DeepSeek сохранил упаковку AlphaContent, модели и отсутствие magenta. Эти результаты становятся входом нового M0, а не доказательством его завершения.

