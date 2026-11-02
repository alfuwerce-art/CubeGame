# S00-R02 — ARCHITECTURE_MAP: слои, владельцы данных и поток кадра Alpha 2013

**Проект:** CubeGame. **Этап:** S00-R02 (D41), партия 1. **Источники:** `SRC-01`, `SRC-02`.
**Назначение:** карта зависимостей и потоков данных, чтобы решения CubeGame опирались на фактическую структуру клиента, а не на догадки. Тела функций не переносятся; адреса — для проверяемости.

## 1. Слои клиента (подтверждение R01 + уточнения)

| Слой | Состав | Доказательство |
|---|---|---|
| Игровая логика `cube::` | `GameController`, `Controller`, `World`, `WorldInfo`, `WorldMap`, `Zone`, `ZoneTile`, `Region`, `Field`, `Chunk`, `ChunkBuffer`, `LandscapeTile`, `Dungeon`, `House`, `Spawn`, `Creature`, `Sprite`, `SpriteManager`, `Speech`, `QuestText`, `QuestTextNode`, дерево `Behavior` (8 классов), `Database`, виджеты UI | RTTI `Cube.exe` (R01 §5) + реконструкция |
| Движок `plasma::` | окно/D3D9, шрифты, виджеты, объектный поток `.plx` | RTTI + `FILE_FORMATS.md` §4 |
| Отражение/биндинги `abstr::` | `Function`, `Machine`, helper-классы | RTTI |
| Сторонние | SQLite 3.7.15.2 (статически), zlib 1.2.3, FreeImage, XAudio2, MSVC 2012 CRT | DLL в каталоге, строки |
| Сеть | `cube::Server`, `cube::Connection` (только в `Server.exe`), в клиенте — winsock-вызовы `Ordinal_19` в `World::updateActiveChunks` | `005946c0` (клиент), README Reversal |

**Вывод для ADR-0001:** граница «игровая логика ↔ представление» в оригинале проходила между `cube::` и `plasma::`; это независимое подтверждение разделения `Core/World` ↔ `Presentation` в CubeGame.

## 2. Кто владеет данными

| Данные | Владелец | Смещения/поля | Примечание |
|---|---|---|---|
| Персонаж игрока и существа | `Creature` (общий класс) | дерево полей — `BEHAVIOR_SPECS/E_combat.md`, §E1 | игрок, моб, NPC — один класс, различаются флагами/полями |
| Глобальное состояние приложения, мир, UI, сеть | `GameController` | `+0x8006d0` — мир (World), `+0x8006cc` — сокет, `+0x800714` — аудио, `+0x800d3c` — список покупок/продаж (cap 10) | два «пояса» полей: обычные и `0x800xxx` (мир/движок) |
| Активные чанки и сущности | `World` | `+0x8006ec`, `+0x8006f4`, `+0x8006fc` — три `std::list` активных чанков; `+0x800585` — флаг «сеть активна» | см. §3 |
| Рельеф/биом/структуры региона | `WorldInfo` | `+0x800168..0x800174` — смещения шума; `+0x8006d0` — ссылка на мир | см. `B_world.md` |
| Карта | `WorldMap` | ключи `reg<X>_<Y>` в `map_<мир>.db` | см. `DATA_AND_SAVE_SCHEMAS.md` |
| База ресурсов | `Database` | SQLite-обёртка, `data1..4.db` | см. `DATA_AND_SAVE_SCHEMAS.md` |
| Тексты/диалоги | `Speech`, `QuestText`, `QuestTextNode` | — | партия G/J |

## 3. Поток кадра (что подтверждено)

1. **Кадр/тик приложения** — виртуальные методы `GameController` (`vfunc_0`…`vfunc_11`, адреса `0046d340`…`004ac260`). Точное сопоставление «vfunc → фаза» пока не восстановлено (`PARTIAL`), но состав фаз виден по вызовам: ввод (`handleKeyPress` `0047e1b0`), отрисовка UI (`drawQuestHud`, `drawItemTooltip`, `drawCurrencyPanel`…), мир (`generateRegion`, `unloadZone`, `loadLandscapeTile`, `streamTileChunk`), сохранение (`saveEntityBlob`, `saveStructureBlob`).
2. **Обновление мира** — `World::updateNearbyEntities` (`004690a0`), `World::updateObjectLabels` (`004c9680`), `World::updateActiveChunks` (`00469c10`).
3. **Порядок загрузки зоны**: `GameController_generateRegion` (`005da280`) → `loadLandscapeTile` (`006024d0`) → `streamTileChunk` (`00603a00`) → `unloadZone` (`00602160`) при уходе.
4. **Сериализация состояния сущностей** — `EntityState_serializeToBuffer` (`004cdaa0`), затем `deserializeState` у `Creature` (`0044be40`).

### Важная находка (C)

`World::updateActiveChunks` (`00469c10`) **не является локальным стримингом**: тело содержит `timeBeginPeriod/timeGetTime`, критическую секцию, очистку трёх списков активных чанков и **отправку сериализованного состояния сущностей через winsock** (`Ordinal_19` на сокете `GC+0x8006cc`). То есть имя функции вводит в заблуждение: это клиентская часть сетевой синхронизации чанков, а не загрузка геометрии.

Следствия:
- локальная загрузка/выгрузка зоны — в `GameController` (`loadLandscapeTile`/`streamTileChunk`/`unloadZone`), а не в `World`;
- три списка активных чанков в `World` — это **сетевые** буферы (отправка/приём/подтверждение), а не кэш мешей;
- в CubeGame сетевой слой не реализуется на этом этапе (`DEFERRED`), поэтому эту часть Alpha не переносим.

### FEATURE_ID: A.chunk-sync-misnomer
SUBSYSTEM: A/C
ORIGINAL_OBSERVATION: функция «updateActiveChunks» шлёт данные по сети и очищает списки буферов; время берётся через `timeGetTime`, точность таймера поднимается `timeBeginPeriod(1)`.
RECONSTRUCTION_REF: `cube/world/World.cpp` → `00469c10`; `attribution.tsv`/`GAP_ANALYSIS.md` (комментарий `NOTE(re)` о реклассификации).
LOCAL_BINARY_REF: winsock-импорт `Ordinal_19` присутствует только в клиенте.
CONTRADICTIONS: имя функции и разбиение по классам Reversal предполагают «обновление чанков», тело — сетевой обмен.
CONFIDENCE: STRONGLY_SUPPORTED
CUBEGAME_DECISION: DEFERRED (мультиплеер вне этапа); в спецификации стриминга использовать `GameController`-путь, а не `World::updateActiveChunks`.
UNITY_MAPPING: own-game-logic для локального стриминга; сеть — вне области.
GAPS: где именно клиент решает «какие чанки держать» — в `streamTileChunk`/`loadLandscapeTile` (см. `B_world.md`).

## 4. Зависимости подсистем (что от чего зависит)

```text
Database (SQLite data*.db)  ──►  ресурсы (модели .cub, звуки, иконки, словари)
        │
        ▼
GameController ──► World ──► WorldInfo ──► Zone ──► ZoneTile ──► Chunk ──► ChunkBuffer
        │            │           │            └── Region ──► Field
        │            │           └── структуры: Dungeon / House / Spawn
        │            ├──► Creature (игрок, мобы, NPC) ──► Behavior-дерево (AI)
        │            ├──► WorldMap ──► Save/map_<мир>.db
        │            └──► Save/world_<мир>.db (сущности), worlds.db (список миров)
        ├──► Save/characters.db (персонаж)
        ├──► UI-виджеты (Plasma) ──► .plx
        └──► Audio (XAudio2), Render (CubeShader, SmoothMeshShape)
```

Проверяемые зависимости, важные для CubeGame:

1. **Генерация зависит от мира только через seed и координаты** — это позволяет CubeGame иметь детерминированную генерацию без глобального состояния (подтверждается сигнатурами `WorldInfo_sampleTerrainHeight(x, y)` и чтением `world + 0x800168`).
2. **Бой зависит от экипировки через слоты** (`0x990…0xcd8`), то есть предметная система — предпосылка боевых формул (пакеты E и F связаны).
3. **AI зависит от боевого резолва** (`applyMeleeHit`) и threat-таблицы; без неё поведение врагов не воспроизводимо.
4. **Сохранения зависят от стабильных идентификаторов сущностей** (координатные ключи `monster<X>_<Y>`), а не от указателей.

## 5. Что это означает для модулей CubeGame (ADR-0001)

| Модуль CubeGame | Что покрывает из Alpha | Не покрывает (осознанно) |
|---|---|---|
| `Core` (чистая логика) | формулы E2/E4/E8/E9, кривые, прогрессия | рендер, ввод, анимации |
| `World` | координаты, генерация, стриминг, карта | сеть, `.plx`-UI |
| `Entities`/`Combat` | поля существа, бой, AI-решения | анимационные автоматы Plasma |
| `Items` | предметы, слоты, руны | `.cub`-модели как runtime-формат |
| `Persistence` | версии, миграции, стабильные ID | SQLite-схемы Alpha 1:1 |
| `Presentation` | камера, ввод, UI, звук — средствами Unity | собственная виджет-система Plasma |

## 6. Распределение игрового кода по классам (измерено)

Подсчёт по `cube/attribution.tsv` (клиент, коммит `e6e906e4`), выполнен 2026-11-03:

- всего функций: 12 983 строки-функции; по полю `kind`: `lib` = 11 300, `gamemisc` = 671, `game` = 1 012.
- суммы по классам (топ): `game_misc` 671, `GameController` **620**, `AdaptionWidget` 51, `SmoothMeshShape` 32, `Speech` 26, `World` 25, `XAudio2Engine` 24, `WorldInfo` 16, `QuestText` 15, `ChatWidget` 13, `Global` 13, `Creature` **10**, `Interface` 10, `CombatBehavior` 9, `CharacterStyleWidget` 9, `Controller` 8, `ChunkBuffer` 8; далее — по 1…6 (поведения AI, `Zone`/`Region`/`Chunk`/`Field`, виджеты).

**Вывод, важный для CubeGame:**

1. Игровая логика Alpha **не** разложена по классам: 671 свободная функция `game_misc` + 620 методов `GameController` против 10 у `Creature` и 25 у `World`. Классы существуют как «якоря данных» (RTTI), а поведение живёт в свободных функциях и в «божественном» контроллере.
2. Следовательно, у CubeGame **нет** готовой модульной декомпозиции Alpha, которую можно скопировать: разбиение на модули (`Combat`, `World`, `Items`, `Persistence`) — наше архитектурное решение, а не перенос структуры. Это подтверждает ADR-0001 и предупреждает попытку «повторить классы Alpha».
3. `GameController` (≈4 МБ псевдокода, 620 методов) — главный риск связности. При переносе его нельзя воспроизводить как один класс.
4. `AdaptionWidget` (51 функция) — крупнейший UI-класс; в Alpha он содержит не только отрисовку, но и логику «адаптации» (прокачки). При переносе UI-часть заменяется Unity UI, логика — в `Core`.

**Расхождение:** README Reversal указывает 1 370 игровых функций («class plus game_misc»), мой подсчёт по `attribution.tsv` даёт 1 683 (`game` + `gamemisc`). Причина, вероятно, в том, что часть `game`-функций Reversal относит к «не игровым» (виджеты/рендер). Числа в спецификациях беру из собственного подсчёта и помечаю как измеренные.

## 7. Открытые архитектурные вопросы

1. `GameController` — «божественный объект» на ~4 МБ кода; при переносе его обязанности нужно резать по швам (`World`, `Combat`, `UI`, `Save`, `Net`), иначе CubeGame повторит эту связанность. Решение — `PROPOSED`, требует ревью архитектора.
2. Три списка «активных чанков» в `World` относятся к сети; в CubeGame нужен свой менеджер чанков с явным владельцем (сцена/сервис), а не поля в `World`.
3. Тайминги Alpha используют миллисекунды системного таймера (`timeGetTime`), а не «кадры». В Unity нужно явно решить: `Time.deltaTime`-аккумулятор (проще) или фиксированный шаг (детерминизм). `PROPOSED`: фиксированный шаг симуляции 60 Гц для боя, рендер — переменный.
