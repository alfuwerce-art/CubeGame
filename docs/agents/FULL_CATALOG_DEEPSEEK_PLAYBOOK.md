# Методичка DeepSeek: массовый перенос ВСЕГО каталога Alpha → Unity (D63 по методу D64)

**Статус:** результат D64 **утверждён продюсером** — [D65](../decisions/D65-d64-result-approved-deepseek-full-catalog-run.md) (2026-11-06). Запуск разрешён; база `claude/s05b2-full-catalog-research` @ `b3bc102`.
Отчёт-основание: [S05B2_CLAUDE_FULL_RESEARCH_REPORT](../reports/S05B2_CLAUDE_FULL_RESEARCH_REPORT.md).
Код: [tools/alpha_models/catalog](../../tools/alpha_models/catalog/README.md) + существующий универсальный конвейер.

## 0. Правила (не меняются)

- Рабочая папка одна — `D:\Projects\CubeGame`; одновременно с Claude не работать. `D:\Games\Cube world` — только чтение.
- Всё, что получено из данных Alpha (`.cubdec`, кадры, сцены, каталоги `.json` с именами/размерами), — только в `.scratch`; в Git — код, отчёты и файлы доказательств из чисел.
- Не править: `main`, S04, чужие ветки; без merge/force-push. Push своей stage-ветки — по постоянному разрешению.
- Статусы происхождения: `EMULATED_ALPHA_CODE` (исполнение байт), `RECONSTRUCTED_FROM_EVIDENCE` (формула, совпадающая с исполнением), `TRANSFERRED_FROM_ALPHA` — **только** реальный захват. `live_check=NOT_CAPTURED` не скрывать.
- Ничего не выдумывать: нет доказательства — `BLOCKED` с кодом причины; продолжать остальное.

## 1. Подготовка (один раз, ~3 ч машинного времени, можно на ночь)

```powershell
cd D:\Projects\CubeGame
git status --short --branch                       # чисто, своя stage-ветка от claude/s05b2-full-catalog-research
python -m unittest discover -s tools/alpha_models/tests -t .     # ожидание: все OK (Alpha-тесты не пропущены)
# 1. статическая таблица с ИСПРАВЛЕННЫМ реестром (B-3) — перезаписать старую
python -m tools.alpha_models.universal.pipeline scan --out .scratch/universal/static/alpha_static.json
# 2. реестр ассетов (+ alpha_static/item_models/registry_sizes в .scratch/catalog, если их нет)
python -m tools.alpha_models.catalog.asset_catalog
# 3. триггеры ИИ всех видов (~3–4 ч на 10 процессах) и экспорт доказательств; ожидание: 155 CONDITIONAL + 1 PARTIAL (0x76)
python -m tools.alpha_models.catalog.trigger_catalog --out .scratch/catalog/trigger_catalog.json
python -m tools.alpha_models.catalog.trigger_catalog export
# 4. ходьба против оракула — уже в Git (156/156); пересчитывать только если менялся param_walk/drawmodel_emu
#    python -m tools.alpha_models.catalog.walk_oracle
# 5. каталог анимационных путей и зонд экипировки (для отчёта)
python -m tools.alpha_models.catalog.anim_catalog --out .scratch/catalog/anim_catalog.json
python -m tools.alpha_models.catalog.equipment_probe --out .scratch/catalog/equipment_probe.json
# 6. план (должен показать всех AUTO_READY, кроме перечисленных в отчёте блокеров)
python -m tools.alpha_models.universal.transfer_planner plan --out-dir .scratch/universal/plan_d64
python -m tools.alpha_models.universal.transfer_planner validate --out .scratch/universal/plan_d64/validate.json
```

Если `trigger_catalog export` или `git diff` показывает изменение `tools/alpha_models/catalog/npc_trigger_evidence.json`
относительно коммита Claude — это значит, что код или exe отличаются; **остановиться и сообщить**, не подменять.

## 2. Три независимых потока массового переноса

### 2.1 Статичная геометрия всех моделей (2857 блобов, все маршруты)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\alpha_models\catalog\run_catalog_import.ps1 -Select all
```
- Успех: `CATALOG_IMPORT_RESULT=PASS exit=0`, `models=2857 pass=2857 fail=0`.
- Проверяется каждая модель: размер сетки, число сплошных и маркерных вокселей (правило D61) равны рассчитанным в Python, есть грани, prefab создан.
- Возобновление: повторный запуск пропускает импортированные с тем же SHA256 (`resumed=N`). Сбой одной модели не останавливает остальные: строка `PROBLEM <name>: <reason>` в `CatalogOutput/catalog_import.txt`.
- 21 модель, которые код Alpha загружает по имени, но которых нет в `data1.db`, — в манифесте `absent` (`ALPHA_EMPTY_MODEL`): в Alpha они пустые, в Unity их не создавать.

### 2.2 Существа: геометрия + риг + ходьба/покой всего каталога

```powershell
python -m tools.alpha_models.universal.transfer_planner batch --authorization D63 --scope d63_full --dry-run
python -m tools.alpha_models.universal.transfer_planner batch --authorization D63 --scope d63_full --producer-approved-d64
```
- Отбор динамический: **все `AUTO_READY` виды**, кроме уже перенесённых S05B-1/S05B-2; не готовые перечислены в `summary.json → not_ready` с причинами.
- Покрытие B (каждая модель каждого слота хотя бы раз). Фаза 1 = базовый seed каждого вида; фаза 2 = варианты. **Ошибка одного вида не останавливает каталог** (`phase_gate=regression_only`); фазу 2 останавливает только сломанная регрессия HumanMale 98/98.
- Итог: `PASS_ORACLE_VERIFIED` = Unity PASS + формула ходьбы вида совпала с исполненным `drawModel` (`walk_oracle_evidence.json`), `live_check=NOT_CAPTURED`. `PASS_FORMULA_ONLY` появится, только если у вида нет `ORACLE_VERIFIED` — это сигнал для Claude.
- Возобновление/повтор: тот же запуск продолжает с `batch_state.json` (SHA256 задания и входов); для повтора одной записи удалить её ключ из `batch_state.json → entries`.

### 2.3 Действия ИИ существ (безоружные запуски, доказанные исполнением)

```powershell
python -m tools.alpha_models.universal.transfer_planner batch --authorization D63 --scope d63_actions --dry-run
python -m tools.alpha_models.universal.transfer_planner batch --authorization D63 --scope d63_actions --producer-approved-d64
```
- Отбор: все `AUTO_READY` виды с профилем `creature_ai` (профили D64 из `npc_trigger_evidence.json`; профили D60 S05B-A имеют приоритет и не повторяются). Фаза 1 = один вид на семейство + регрессия.
- Перед запуском проверяется дрейф: SHA256 кода, которым сделаны доказательства, должен совпасть (`TRIGGER_EVIDENCE_CODE_DRIFT` → отказ).
- Позы — `EMULATED_ALPHA_CODE` (выпечка `drawModel`), условия запуска (класс/специализация NPC) записаны в профиле `launch_conditions`. Действие, чья поза статична (`ACTION_STATIC`) или длительность не доказана, — `BLOCKED` с причиной, не заглушка.
- **Не входят:** действия, запускаемые только с оружием (`weapon_conditional_actions`), пока у рига вида не доказан слой оружия (сейчас — только гуманоид S05A-2). Их список — в отчёте.

## 3. Что сдавать

- `summary.json`/`summary.md` каждого из трёх потоков + `CatalogOutput/catalog_import.txt`.
- Таблица: вид → GEOMETRY / RIG / WALK / IDLE / ACTIONS (n из m) / EQUIPMENT / UNITY; предметы и статика — по маршрутам.
- Все `BLOCKED`/`FAIL` с кодом причины; список для Claude — только то, что требует нового исследования.
- Отчёт `READY_FOR_REVIEW` и точный SHA ветки. Визуальная приёмка — за продюсером **после** всего прогона.

## 4. Типовые ошибки и что делать

| Симптом | Причина | Действие |
|---|---|---|
| `MODEL_NOT_IN_DB … orc-head-m01.cub\|orc-head.cub` | старая статическая таблица (до D64) | шаг 1.1 — пересобрать `alpha_static.json` |
| `KIND_SPECIFIC_CODE … (walk oracle: NOT_RUN)` | нет `walk_oracle_evidence.json` в рабочей копии | `git checkout` файла из ветки Claude |
| `TRIGGER_EVIDENCE_CODE_DRIFT` | изменён код ИИ/эмулятора | не править доказательства вручную; пересчитать шаг 1.3 и сообщить |
| `ACTION_STATIC`, `ACTION_DURATION_UNPROVEN` | действие не меняет позу / нет тайминга | оставить `BLOCKED` в отчёте |
| `ANCHOR_RULE_NOT_ESTABLISHED offhand_b` | второй блок левой руки не установлен | `BLOCKED`, в список Claude |
| Unity `exit != 0` при PASS-маркере | сбой редактора | повторить запись; при повторе — `FAIL` с логом |
