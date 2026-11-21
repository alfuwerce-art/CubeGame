# Перенос рабочего пространства CubeGame на диск D:

**Задача:** `S05A2-MIGRATION-ANIMATION-01`, блок A (D56 — разрешение продюсера на перенос).
**Дата:** 2026-11-05. **Статус:** `MIGRATION_RESULT=PASS`.

## 1. Пути

| | Путь |
|---|---|
| Источник | `C:\Users\bow\Desktop\CubeGame` |
| Целевой каталог | `D:\Projects\CubeGame` |
| Оригинальная игра (не перемещалась) | `D:\Games\Cube world` |
| Unity 6000.3.27f2 (не переустанавливался) | `D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe` |

## 2. Свободное место

Перед копированием: на `D:` свободно **660.8 ГБ** при объёме источника **4.5 ГБ** — запаса достаточно.

## 3. Состояние Git до переноса

* HEAD `ea2bd13dcf2be7e8540a8a9acf7d3ebdf59a0980`, ветка `stage/s05a2-proportions-tint`;
* remote `https://github.com/alfuwerce-art/CubeGame.git`;
* незакоммиченное: 1 файл (`Assets/CubeGame/Core/Domain.meta`, не относится к этапу);
* все локальные ветки: 13 (включая 12 удалённых в origin).

## 4. Как копировалось

`robocopy C:\Users\bow\Desktop\CubeGame D:\Projects\CubeGame /E /COPY:DAT /DCOPY:DAT /R:2 /W:2`

* **без** `/MIR` и `/PURGE` — существующие данные не удалялись;
* скопированы скрытые и игнорируемые каталоги, включая `.git`, `.scratch` (исследовательские копии Alpha, реконструкция Ghidra, Unity-пилоты, PNG, логи), `.unity-install`;
* `git clone` как замена переносу не использовался.

Код возврата robocopy `1` = «файлы скопированы», без ошибок.

## 5. Сверка

| Метрика | Источник (C:) | Цель (D:) | Итог |
|---|---|---|---|
| Файлов | 6 642 | 6 642 | совпадает |
| Байт | 4 804 309 078 | 4 804 309 078 | совпадает |

SHA256 критических файлов (все `MATCH`):

| Файл | SHA256 (первые 12) |
|---|---|
| `.scratch\alphatest2\Cube.exe` | `84A7A132A84D` (совпадает с `D:\Games\Cube world\Cube.exe`) |
| `.scratch\CubeWorld-Reversal\cube\control\GameController.cpp` | `6C40170F34C1` |
| `tools\alpha_models\character\proportions.json` | `DFAECB2A5339` |
| `docs\research\alpha2013\ANIMATION_EXTRACTION_RESEARCH.md` | `CA6DBFFA8466` |

Git из нового каталога: HEAD и ветка те же, все локальные и удалённые ветки на месте, рабочее дерево чистое (кроме того же `Domain.meta`).

## 6. Исправленные абсолютные пути

Обновлено **105 текстовых файлов** (`.ps1`, `.py`, `.md`, `.json`, `.cs`, `.txt`): заменены `C:\Users\bow\Desktop\CubeGame` → `D:\Projects\CubeGame` (в том числе JSON-экранированные варианты).

Вручную дополнено:

* `PROJECT_MEMORY.md` — строка «Локальный Git workspace» теперь указывает на `D:\Projects\CubeGame`;
* `docs/agents/DEEPSEEK_HARNESS_SETUP.md` — путь репозитория в примере;
* скрипты запуска/съёмки Alpha (`.scratch/alpha_ctl/*.ps1`), скрипты отчётов и тестов.

Каталоги `Library`, `Temp`, `.git` намеренно не переписывались: их пути регенерируются Unity при первом открытии.

**Вне репозитория:** профиль DSH `cubegame-dev` и рабочая область Unity Hub задаются в интерфейсе приложения; их нужно переключить на `D:\Projects\CubeGame` при следующем открытии (значения чужой конфигурации не редактировались автоматически).

## 7. Проверки из нового каталога

| Проверка | Команда | Результат |
|---|---|---|
| Python unittest | `python -m unittest discover -s tools/alpha_models/tests -t tools/alpha_models` | **62/62 PASS** |
| Unity self-tests S05A-1 | `CharacterPilotVerifier.RunSelfTests` | **82/82 PASS**, exit 0 |
| Сборка персонажа | `CharacterPilotVerifier.Run` | **`CHARACTER_RESULT=PASS`**, exit 0 |
| PlayMode (ходьба) | `CharacterPilotVerifier.RunPlayMode` | **`PLAYMODE_RESULT=PASS`**, 40/40 кадров, exit 0, session `bfbbb14177bc477a8e4d8599b8b72a2f` |
| Загрузка моделей | внутри сборки (8 частей, 0 проблем) | PASS |
| Git | `git status`, `git log` из D: | HEAD/ветка/remote на месте |

Выходные файлы (PNG, отчёты) пишутся в `D:\Projects\CubeGame\.scratch\...` — проверено по наличию 84 PNG в `PilotOutput` после прогона из нового каталога.

Режим PlayMode атаки (`-pilotAttack`) остаётся в прежнем состоянии `FAIL/BLOCKED` — это **не** результат миграции; работа по нему вынесена в блок B и описана в отчёте об анимациях.

## 8. Старая копия

`C:\Users\bow\Desktop\CubeGame` **не удалялась** — по условию задания удаление требует отдельного разрешения продюсера. Обе копии существуют; дальнейшая работа ведётся только на `D:`.

## 9. Итог

`MIGRATION_RESULT=PASS`: рабочее пространство перенесено полностью (файлы, `.git`, `.scratch`, исследовательские данные), пути внутри репозитория обновлены, весь регресс проходит из нового каталога, старые данные сохранены.

**Требуется от продюсера:** подтвердить миграцию и разрешить удаление C:-копии (или оставить её как резервную).
