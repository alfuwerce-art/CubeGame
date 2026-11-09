# Передача Claude → Codex: часть интегратора в переносе D88

**Дата:** 2026-11-10. **Основание:** [D89](../decisions/D89-codex-takes-over-claude-part.md). **База:** `integration/m0-all` @ `48ca8bc` (опубликована в origin).
**Папка:** `D:\Projects\CubeGame` (Unity 6000.3.27f2: `D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe`). Перед работой — `git status`; `ProjectSettings/ProjectSettings.asset` изменён не нами (переводы строк) — не коммитить.

## 1. Сначала прочитать

1. `AGENTS.md` — особенно правило «было → станет» (до каждой правки показать продюсеру где, что сейчас → что будет, зачем, что не меняется; после — файлы и SHA).
2. [D88](../decisions/D88-two-agent-split-transfer-first.md), [D89](../decisions/D89-codex-takes-over-claude-part.md), полный план [M0_D88_FULL_WORK_PLAN](../planning/M0_D88_FULL_WORK_PLAN.md) (пакеты C1–C11, волны, стыки).
3. Матрица покрытия [M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX](../research/alpha2013/M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX.md) — коды строк (P-…, C-…, N-…, W-…, I-…, Q-…).
4. Журнал `docs/agents/AGENT_COORDINATION.txt` — последние записи (API, REPORT DeepSeek).
5. [Отложенные проверки PlayMode](../planning/M0_PLAYMODE_DEFERRED_CHECKS.md).

## 2. Правила продюсера, действующие сейчас

- **PlayMode не запускать** до конца переноса; каждую сценовую проверку и сбой — строкой в `M0_PLAYMODE_DEFERRED_CHECKS.md`. Немедленно — только если механика не подключается из-за пропущенной ранее проверки. На каждый пакет: EditMode (+ `tools\m0_pilots\run_pilots.ps1`, если менялся `Core/**`).
- **Совместимые исправления** (имена, расхождения без изменения чисел/сейвов/механик) — делать сразу, сообщать после. Изменение игровых чисел, формата сейва, принятых этапов (S04: Core) — сначала вопрос продюсеру. Пример: материал 2 в `ItemModifier` разрешён продюсером, при этом поправлен тест S04.
- Push своей рабочей ветки после коммитов разрешён; `main`, force push — нет. Оригинальные файлы Alpha не публиковать.
- После каждого пакета: коммит → push → `REPORT` в журнал; вливать `origin/deepseek/m0-content` после каждого REPORT DeepSeek (до сих пор — без конфликтов), прогнать EditMode, записать результат.

## 3. Метод: числа только из байтов Alpha

Реконструкция `.scratch/CubeWorld-Reversal/{cube,server}` (Ghidra) — указатель, **не источник чисел**: в ней потеряны аргументы (`arrayElem_stride4()` без индексов), имена часто неверны (`physics_isLandingHard` = «планирует», `randomizeVec3` = поле ветра). Каждое число сверять с байтами `D:\Games\Cube world\Cube.exe` (только чтение):

- `.scratch/c2/disx.py <start> <end>` — дизассемблер (capstone; запускать `python` без `-I`, пакет в user site), подписывает константы `.rdata`;
- `.scratch/c2/funcs.txt` / `sfuncs.txt` — адрес → имя функции клиента/сервера; `.scratch/c2/upd.asm`, `inp.asm` — дампы тика физики `0x60C510` и ввода `0x4A6B50`;
- таблицы `switch`: читать таблицу переходов в байтах (пример: стартовые наборы 0x4777D8, модели голов 0x42C41C);
- эмулятор x86 для исполнения функций целиком: `tools/alpha_models/universal/x86emu.py`, `tools/alpha_models/world/terrain_oracle.py`.

Статусы: `DIRECT` / `EMULATED_ALPHA_CODE` / `CUBEGAME_CONTRACT` / `PLACEHOLDER_NOT_ALPHA` / `UNKNOWN` — не выдумывать.

## 4. Что сделано Claude (D88)

| Пакет | Состояние | Отчёт |
|---|---|---|
| C2 движение | бег 12/шаг 6 (Shift), ускорение 80, трение, прыжок, гравитация 30; плавание (плавучесть, сопротивление), лазание (Ctrl), правило дельтаплана (G); OQ-23 и B-U5 закрыты | [C2](../research/alpha2013/C2_ALPHA_MOVEMENT_PHYSICS.md) |
| C1 создание героя | 8 рас × 2 пола → вид, лица/причёски, палитра 112, имя ≤15, стартовые наборы классов, внешность в сейве и в `EntitySpawned.Appearance`, редактор IMGUI | [C1](../research/alpha2013/C1_ALPHA_HERO_CREATION.md) |
| C6 шаг 1 | `WorldPopulation`: заселение зон вокруг героя из `AlphaWorldPopulation`, ID по зоне `ForZoneSpawn`, убитые — дельта мира, реген фракции 5 | [C6](../research/alpha2013/C6_WORLD_POPULATION_IN_GAME.md) |
| C3 шаг 1 | опыт за убийство (0x595A60), повышение уровня с HP = максимум (0x447B00), событие `ExperienceGained` | [C3](../research/alpha2013/C3_ALPHA_PROGRESSION.md) |

Последний полный EditMode: 530/532 (0 failed, 2 skipped).

## 5. Что дальше (порядок плана, волны 2–5)

| Пакет | Остаток | Подсказки |
|---|---|---|
| C3 | ранги навыков P-13 (`+0x1138`, 11 int: Taming, Riding, Climbing, HangGliding, Swimming, Boat, Ability1…5), очки/покупка/сброс P-14 (OQ-04), специализации P-12 | ранги уже читает `AlphaMovement` (`ClimbingRank`, `HangGlidingRank`, `SwimmingRank`) — подключить к состоянию героя и сейву |
| C9 | вода W-07 (вид), пещеры W-08, карта W-14, имена регионов W-15, сутки Q-24/N-15 | вода уже есть вокселями (`Voxel.IsWater`), плавание подключено через `PlayerMotor.WaterAt` |
| C6 остаток | лимит/вне экрана (N-20/N-21: сервер `0x5322D0` удаляет существо, если игрок дальше `+0x1D2C + 20`; поле не разобрано), звёзды существ, NPC-таблица, подключение `AlphaZoneContentProvider` каналом 1, убрать тренировочного волка (на нём тесты боя PlayMode) | `GameBootstrap.StartPopulation/OnSpawnAppeared` |
| C4 | бой: стамина атак, блок, способности 16 узлов, кулдауны/мана, комбо, стан, рывок (`0x4A72A6…0x4A73D9`), скорости состояний атак (0x619Exx–0x61A7xx), атаки по оружию (лук/посох сейчас бьют атакой 57 HumanMale) | |
| C5 | 13 слотов (кольца, амулет, лампа, особый слот — дельтаплан 0x17/0 и лодка 0x17/1 → `AlphaMovement.HasGlider`, лодка = состояние 0x6B), кошелёк, сундуки, крафт | стартовые кольца/лампа сейчас в сумке |
| C7, C8 | торговля, диалоги, задания | наценки торговцев — UNKNOWN (вопрос продюсеру в плане §8) |
| C10, C11 | интерфейс, сквозные сценарии, Windows-сборка | редактор героя и HUD сейчас IMGUI |

## 6. Открытое

- **Дефект P1** (`M0_PLAYMODE_DEFERRED_CHECKS.md`): два PlayMode-теста стриминга (коллайдер отредактированной колонны не обновляется за 10 кадров) после включения заселения — причина не установлена.
- DeepSeek ждёт: превью модели героя по `HeroAppearance` в редакторе (его D4), API для сундуков/сервисов зоны (его D3). Герой пока рисуется HumanMale (`GameVisualSink.BuildHero` — файл DeepSeek).
- `.scratch/c2/stash_pop_backup/` — копии двух файлов, затронутых ошибочным `git stash pop` старого stash S05B (сам stash `stash@{0}` цел); можно не трогать.
