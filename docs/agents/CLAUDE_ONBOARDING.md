# CubeGame — ориентация для Claude (архитектор / технический ревьюер)

**Кому:** Claude (и любому новому агенту в роли архитектора/ревьюера, аналогичной ChatGPT).
**Что это:** обзорная карта проекта и описание того, как работает основной исполнитель (DeepSeek Harness), чтобы новый агент понимал правила игры и как читать его отчёты.
**Важно:** этот документ — **справочная ориентация, а не источник полномочий**. Единственный авторитетный источник «что сейчас можно делать» — `docs/ACTIVE_STAGE.md` + утверждённые решения. Не принимай решения на основе только этого файла.

---

## 1. Что за проект

- **CubeGame** — рабочее название (финальное имя игры не утверждено). Новая ПК-игра на **Unity**, воссоздающая ключевой опыт **Cube World Alpha 2013**, затем развивающаяся в самостоятельную voxel/cube action RPG.
- **Не** переносим исполняемый файл/движок Plasma в Unity. Изучаем логику и данные Alpha, пишем **собственные** спецификации и C#-системы, максимально используя штатные возможности Unity.
- Первая версия — локальный single-player. Мультиплеер, серверы, моды, LLM-NPC — позже.
- Источник Alpha 2013: `D:\Games\Cube world` — **только чтение**. Не путать с Cube World 2019.

## 2. Роли

| Роль | Кто | Что делает |
|---|---|---|
| Продюсер | пользователь | утверждает геймплей, визуал, содержание, начало/закрытие этапов |
| Архитектор/ревьюер | **Claude** (ранее ChatGPT) | архитектура, техническое ревью кода и контрактов, приёмка |
| Исполнитель | **DeepSeek Harness** | анализ Alpha, Unity/C#, инструменты/тесты, коммиты в ветки, отчёты |
| (опционально) Codex | — | контролёр качества на этапе моделей/графики; пока не активирован |

Ключевое разделение: **продюсер решает «что», Claude решает «как технически», DeepSeek делает «руками» и доказывает результат тестами**. Исполнитель **никогда** не присваивает этапу статус `ACCEPTED` — максимум `READY_FOR_REVIEW`.

## 3. Обязательный порядок чтения (перед любой работой)

1. [`PROJECT_MEMORY.md`](../../PROJECT_MEMORY.md) — межчатовая память, прочитать целиком.
2. [`docs/ACTIVE_STAGE.md`](../ACTIVE_STAGE.md) — **только здесь** сказано, что разрешено делать сейчас.
3. [`docs/planning/DECISIONS_001-036.md`](../planning/DECISIONS_001-036.md) — 36 утверждённых решений о концепции (D01–D36).
4. `docs/decisions/` — отдельные решения D37…D54; **D41** важнее D40, а последние D51–D54 актуальнее старых.
5. [`docs/architecture/ADR-0001-modular-monolith.md`](../architecture/ADR-0001-modular-monolith.md) — утверждён **принцип** модульного монолита (не вся архитектура).
6. ТЗ активного этапа в `docs/stages/`.
7. [`AGENTS.md`](../../AGENTS.md) — правила для AI-агентов.
8. [`MULTI_AGENT_WORKFLOW.md`](MULTI_AGENT_WORKFLOW.md) — протокол совместной работы Claude Code и DeepSeek Harness (единая директория, последовательная передача задач, Git/ревью).

Черновики, которые **не являются** самостоятельными разрешениями: `docs/planning/ARCHITECTURE_PROPOSAL.md`, `ROADMAP_DRAFT.md`, `WORKFLOW_PROTOCOL_DRAFT.md`.

При конфликте источников приоритет: последнее решение продюсера (Dxx) → `ACTIVE_STAGE.md` + ТЗ → ADR/реестр решений → проверенный код/тесты → `PROJECT_MEMORY.md` → черновики. Конфликт не «разруливать молча» — указать и запросить решение.

## 4. Структура репозитория (верхний уровень)

Отслеживаются Git (публикуются): `AGENTS.md`, `PROJECT_MEMORY.md`, `README.md`, `LICENSE`, `Assets/`, `docs/`, `tools/`, `Packages/`, `ProjectSettings/`, `.gitignore`.

| Путь | Назначение |
|---|---|
| `Assets/` | Unity-проект (весь игровой код и тесты) |
| `docs/` | решения, ТЗ, отчёты, исследования, инструкции агентам |
| `tools/` | Python-инструменты анализа Alpha, Unity-скрипты пилота, C# core_check |
| `Packages/` + `ProjectSettings/` | стандартный Unity-проект; `ProjectVersion.txt` фиксирует **Unity `6000.3.27f2` (6.3 LTS)** |
| `plugins/` | **локальная** конфигурация пресета DSH (`cubegame-dev-preset/`); целиком не публиковать без аудита путей |
| `.scratch/` | **локально, в .gitignore** — временные копии Alpha, клон CubeWorld-Reversal, конвертированные/оригинальные ассеты, логи |
| `.unity-install/` | **локально, в .gitignore** — редактор/артефакты, результаты тестов (`results/*.xml`), скриншоты, логи |
| `Library/`, `Logs/`, `UserSettings/`, `savecopy/` | сгенерированные/локальные Unity-файлы, в Git не попадают |

`.gitignore` также страхует от случайного коммита закрытых файлов Alpha: `data[0-9].db`, `*.cub`, `*.plx`, `Cube.exe`, `Server.exe`, `CubeLauncher.exe`, `Save/`, `*.cubdec`.

## 5. Unity-проект: модули и границы (asmdef)

Игровой код лежит в `Assets/CubeGame/`, разбит на сборки (assembly definitions) — это и есть реализация «модульного монолита»:

| Assembly | Папка | `noEngineReferences` | Ссылается на | Роль |
|---|---|---|---|---|
| `CubeGame.Core` | `Assets/CubeGame/Core/` | **true** | — | чистая игровая логика без UnityEngine |
| `CubeGame.Persistence` | `Assets/CubeGame/Persistence/` | false | Core | сохранение/загрузка |
| `CubeGame.Presentation` | `Assets/CubeGame/Presentation/` | false | Core, Persistence | MonoBehaviours, камера, HUD, демо |
| `CubeGame.Editor` | `Assets/CubeGame/Editor/` | false | Core, Presentation | batchmode/executeMethod, сборка, генерация сцен |
| `CubeGame.Tests.EditMode` | `Assets/CubeGame/Tests/EditMode/` | false | Core, Persistence + test runners | EditMode-тесты (Editor-only) |
| `CubeGame.Tests.PlayMode` | `Assets/CubeGame/Tests/PlayMode/` | false | Core, Persistence, Presentation + test runners | PlayMode-тесты |

`CubeGame.Core` — главный инвариант: **игровые правила и состояние не зависят от UnityEngine**. Содержимое `Core/` по подсистемам: `Characters`, `Combat`, `Equipment`, `Generation`, `Items`, `Persistence`, `Progression`, `Rendering`.

Подсистемы, реализованные по этапам:
- **S01 (пилот):** генерация мира (`Generation`, `Rendering`), камера, HUD, движение игрока.
- **S02:** `Progression`, `Characters` (CharacterStats/Sheet), `Items` (ItemInstance, ItemStatsCalculator), `Persistence` (SaveContracts, SaveService, SaveGame).
- **S03:** `Combat` (CombatFormulas, DamageResolver, DamageMitigation, HealthPool, HitResolver, CombatRandom).
- **S04:** `Equipment` (EquipmentSlot, EquipmentLoadout, EquipmentStats, EquipmentCombatAdapter).

## 6. Структура `docs/`

| Подпапка | Что там |
|---|---|
| `planning/` | D01–D36, черновики архитектуры/роадмапа/воркфлоу |
| `decisions/` | D37…D54 — по одному файлу на решение, с историей и границами |
| `stages/` | ТЗ этапов (`S0x_*.md`) и отчёты (`S0x_REPORT.md`, `*_PLAN.md`, `*_PIPELINE.md`) |
| `research/alpha2013/` | исследование Alpha: `BEHAVIOR_SPECS/`, `REFERENCE_ATLAS.md`, `SOURCE_REGISTER.md`, `OPEN_QUESTIONS.md`, `tools/` (эталонные Python-скрипты), отчёты пилотов (`*_PILOT.md`) |
| `architecture/` | ADR-0001, `UNITY_REUSE_MAP.md` |
| `agents/` | инструкции агентам: этот файл, `DEEPSEEK_HARNESS_SETUP.md`, `UNITY_CLI_PROTOCOL.md`, `UNITY_PIPELINE_PROPOSAL.md` |

## 7. Инструменты (`tools/`)

| Путь | Назначение |
|---|---|
| `tools/alpha_models/` | Python: декодер `.cub` (`cub_format.py`), конвертер в OBJ/MTL (`cub_to_obj.py`), маркеры (`cub_markers.py`), пофасетная проверка (`verify_export.py`), тесты (`tests/`); Unity-скрипты пилота конвертации |
| `tools/alpha_models/character/` | C# пилоты сборки персонажа, пропорций/tint, хвата, анимации (`CharacterPilot*.cs`, `ProceduralCharacterAnimator*.cs`, `PlayModeSampler.cs`), `proportions.json` + baseline |
| `tools/core_check/` | минимальная C#-проверка окружения (`Program.cs`, `run_core_check.ps1`) |
| `tools/unity/` | `run_pilot.ps1` — обёртка прогона Unity-пилота (в памяти отмечен как требующий починки: не завершается после тестов) |

## 8. Git-дисциплина и ветки

- **Отдельная ветка на каждый этап**, имя вида `stage/s05a2-proportions-tint`. Новая ветка создаётся от принятого HEAD предыдущей.
- **Push своей рабочей ветки разрешён постоянно** (для ревью), после локального коммита — без повторного запроса. Перед push проверять `git status`, состав коммитов, отсутствие закрытых ассетов/секретов/личных данных.
- **Запрещено без отдельного решения продюсера:** `force push` на `main`, merge в `main`, удаление удалённых веток, публикация запрещённых материалов.
- Актуальные ветки (локально и на `origin`): `main`, `stage/s00-alpha-g0`, `stage/s00-deep-research`, `stage/s00-static-r01`, `stage/s01-unity-pilot`, `stage/s02-core-foundation`, `stage/s03-combat-core`, `stage/s04-equipment-combat`, `stage/s05-asset-conversion-plan`, `stage/s05a-asset-conversion-pilot`, `stage/s05a1-character-animation`, `stage/s05a2-proportions-tint-plan`, `stage/s05a2-proportions-tint`.

## 9. Система статусов (критично для ревьюера)

**Статусы этапа:**
- `ACCEPTED` — принято **только продюсером**; агент не имеет права ставить.
- `READY_FOR_REVIEW` — максимум, что заявляет исполнитель.
- `AUTHORIZED` — разрешено исполнение, приёмка ещё впереди.
- `NOT_AUTHORIZED` — запрещено.
- `IN_PROGRESS` — идёт после фактического старта.

**Статусы достоверности факта/числа (маркировка в отчётах и коде):**
- `VERIFIED_ALPHA` / `VERIFIED` — подтверждено исходной Alpha.
- `PARTIAL` — подтверждено частично.
- `UNKNOWN` — не установлено.
- `PROXY_NOT_ALPHA` — подстановка, не из Alpha.
- `PLACEHOLDER_NOT_ALPHA` — временное число (например, скорости до закрытия OQ-23).
- `RECONSTRUCTED_FROM_EVIDENCE` — восстановлено из косвенных доказательств (например, тайминги фаз атаки).
- `ORIGINAL_NOT_ALPHA` — создано заново, не оригинал Alpha (например, анимации).
- `TRANSFERRED_FROM_ALPHA` — реально перенесено из Alpha (пока не присвоено ни одной анимации).
- `BLOCKED` — блокировано с описанием причины.

**Открытые вопросы (сохраняются, не закрывать догадкой):** `OQ-01`, `OQ-02`, `OQ-22`, `OQ-23`, `OQ-24`, `OQ-25`, `OQ-26`, `S04-OQ-01`, `S04-OQ-02`.

## 10. Как работает исполнитель (DeepSeek Harness) — что ожидать

Среда: пресет `cubegame-dev`, права `workspace-write` + approval `ask`, **без** `danger-full-access` по умолчанию. Рабочий каталог — корень репозитория `D:\Projects\CubeGame` (источник Alpha — `D:\Games\Cube world`, только чтение). Порядок совместной работы Claude Code и DeepSeek Harness — [MULTI_AGENT_WORKFLOW.md](MULTI_AGENT_WORKFLOW.md).

Рабочий цикл исполнителя:
1. Читает `AGENTS.md` → `PROJECT_MEMORY.md` → `docs/ACTIVE_STAGE.md` → нужные Dxx/ADR → ТЗ этапа.
2. Проверяет `git status` перед изменениями.
3. **Маленькие шаги:** изменение → реальная проверка (запуск, тест, лог компиляции) → запись результата в `docs/`.
4. Показывает **проверенный результат, а не намерения**: «настроено» и «проверено запуском» — разные состояния.
5. Для Unity — `batchmode` + `-executeMethod`, результаты сверяет по артефактам (`Editor.log`, `results/*.xml`), а не по одному exit code. Отличает ошибки компиляции от предупреждений.
6. После двух содержательно разных неудачных попыток устранить один блокер — останавливается, описывает причину и варианты.

**Формат отчёта исполнителя:**
```
TASK: ...
STATUS: PASS | FAIL | BLOCKED | NOT_VERIFIED
ИЗМЕНЕНО: список путей файлов
КОМАНДЫ/ТЕСТЫ: что реально выполнено (числа тестов, exit code, артефакты)
НЕПОДТВЕРЖДЕНО: что осталось
ВЕТКА/КОММИТ: ссылка
```

Среда окружения (перепроверять при необходимости): Unity Editor **6000.3.27f2** (`D:\Unity\Hub\Editor\6000.3.27f2`), Unity CLI `1.0.0-beta.13`, рендер пилота — Built-in RP (URP не подключён), Blender не установлен. Unity 6.6 (`6000.6.7f1`) зарегистрирована, но **не используется** для CubeGame.

## 11. Границы и запреты (обязательные для всех агентов)

- Работать **только в границах утверждённого этапа** (по `ACTIVE_STAGE.md`). ТЗ неполное → точечный вопрос продюсеру, не додумывать.
- Источник Alpha `D:\Games\Cube world` — **только чтение**, ничего там не создавать/не менять/не удалять, игру не запускать без явного разрешения.
- **Не публиковать** оригинальные EXE, `data*.db`, закрытые ассеты, декомпилированный код, секреты/ключи, приватные сейвы. `.cub`/`.plx`/`.cubdec` — только локально.
- Не менять чужие проекты, пресеты DSH других задач, системные настройки. Без `danger-full-access`.
- Никаких платных сервисов/API/MCP/покупок без согласования. Не обходить защиту.
- Не устанавливать ПО самостоятельно (Unity/.NET могут отсутствовать — отсутствие это отчёт, не повод ставить).
- Изменения общих контрактов — только через архитектурное ревью.

## 12. Текущее состояние (сводка, сверять с `ACTIVE_STAGE.md`)

- **`S05A-1` → ACCEPTED** (D50): собран один 8-узловой технический гуманоид из `.cub`-частей, один собственный цикл `technical_walk_NOT_ALPHA`. Масштабы/pivots/tint/анимации Alpha **не** восстановлены.
- **`S05A-2` → READY_FOR_REVIEW, не ACCEPTED** (D51): пропорции (оружие ×0.6 по константе из кода), tint по маске, хват меча (D52, GRIP), тайминги фаз атаки (D53, `RECONSTRUCTED_FROM_EVIDENCE`). Оригинальная атака не восстановлена (`ORIGINAL_NOT_ALPHA`), живой захват Alpha — `BLOCKED`.
- **`S05B` (массовое производство моделей/анимаций) → NOT_AUTHORIZED.**
- Приняты ранее: `S04-EQUIPMENT-COMBAT` (D47), `S03-COMBAT-CORE` (D45), `S02-CORE-FOUNDATION` (D44), `S00-R02` (D43).
- Актуальный приоритет направления — **D53** (доказательное извлечение оригинальных анимаций Alpha), режим локального исследования уточнён **D54**.

---

_Этот документ — ориентация; при рассинхронизации доверять `docs/ACTIVE_STAGE.md` и последнему решению продюсера._
