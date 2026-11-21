# D6 — звук Alpha: инвентарь 93 звуков и библиотека (волна 2, пакет DeepSeek)

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content`.
**STATUS: `READY_FOR_REVIEW`** — инвентарь и разрешение имён доказаны; соответствие «событие → звук»
остаётся `UNKNOWN` (кроме mission complete, Q-09).

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Инвентарь звуков | «в `data2.db` 93 `.wav`, формат не проверялся» | все 93 записи извлечены: контейнер Alpha снимается `cub_format.deobfuscate`, каждый файл проверен как настоящий RIFF/WAVE (каналы, частота, биты), sha256 и размеры записаны в коммит-JSON |
| Аудио | нет | раскодированные `.wav` лежат в локальном кэше `.scratch/content/sounds` (в Git не попадают) |
| Обращение из игры | нет | `AlphaSoundCatalog` (93 имени) + `AlphaSoundLibrary`: поиск по имени, разрешение файла в кэше, **событие → звук только для доказанного** mission complete; для остальных событий — явный отказ `EVENT_SOUND_UNKNOWN`, без подбора «похожего» звука |
| Числовые id звуков | Q-09 упоминал id `0x1e` | id **не** равен rowid в data2.db (missioncomplete.wav: rowid 57), поэтому таблица id→имя не выдумывается: звуки адресуются по имени |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `tools/alpha_content/extract_sounds.py` | извлечение, деобфускация, проверка WAVE, стейджинг, каталог (+`--check`) |
| `tools/alpha_content/data/sound_catalog.json` | инвентарь: имена, rowid, размеры, sha256, параметры WAVE, известные события |
| `Assets/CubeGame/Content/Generated/AlphaSoundCatalog.g.cs` | 93 имени для рантайма |
| `Assets/CubeGame/Presentation/Visual/AlphaSoundLibrary.cs` | поиск/разрешение/событие, честные отказы |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaSoundLibraryTests.cs` | 3 EditMode-теста |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| `python -m tools.alpha_content.extract_sounds` | `SOUND_CATALOG_RESULT=PASS count=93 problems=[]`, файлы в `.scratch/content/sounds` |
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `82/82 passed`, 0 failed (`AlphaOutput/d6_editmode.xml`) |

## 4. PARTIAL / UNKNOWN

1. **Событие → звук**: доказан только `missioncomplete.wav` (Q-09). Остальные 92 звука в игре не
   воспроизводятся, пока не найдена таблица id→звук (она не совпадает с rowid) или вызовы `PlaySound`
   в `Cube.exe` — это research-шаг, а не догадка.
2. **Воспроизведение**: библиотека даёт путь к файлу; фактический `AudioSource`/`AudioClip` подключает
   Claude (C10 интерфейс/звук сцены) — контракт события не менялся.
3. **Громкость/пространство/микшер** — `UNKNOWN`, собственных решений не принимали.

## 5. Что нужно от Claude (C10)

1. Загрузить клипы из `AlphaSoundLibrary` (путь = `<content>/sounds/<имя>.wav`) и играть их на событиях;
   сейчас доказано одно событие — завершение задания (`mission_complete`).
2. Если нужен id→звук: дай знать, я подниму research по вызовам звука в `Cube.exe` (R-задача), иначе
   оставляем имена.
