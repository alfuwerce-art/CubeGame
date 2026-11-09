# D87 — проверка воспроизводимости данных на общей базе (зона DeepSeek-сессия A)

**Дата:** 2026-11-09. **База:** `integration/m0-all` @ `4419fb9` (+ мои локальные артефакты, в Git не входят).
**Статус:** `READY_FOR_REVIEW`, не `ACCEPTED`. **Unity в этой проверке не запускался** (слот держал другой агент).

## 1. Задача

При переходе на общую папку `D:\Projects\CubeGame` проверить, что данные контента Alpha
воспроизводимы в новом каталоге: `tools/alpha_content/**` и локальный кэш.

## 2. Что фактически сделано

1. **Локальный кэш перенесён** (в Git не попадает, `.gitignore`: `.scratch/`, `AlphaOutput/`):
   - `D:\Projects\CubeGame-deepseek\.scratch\content` → `D:\Projects\CubeGame\.scratch\content`
     (471,6 МБ, 5115 файлов, включая `actors/` — 156 видов);
   - `D:\Projects\CubeGame-deepseek\.scratch\tmp` → `D:\Projects\CubeGame\.scratch\tmp`
     (1,5 МБ, 110 файлов, включая `item_models_full.json`, `world_ctor_raw.json`).
   Без этих файлов три генератора не могут провериться в принципе (см. §3).
2. **Прогнаны все 7 проверок генераторов** из `D86_DEEPSEEK_HANDOFF_INDEX.md` §3.

## 3. Результат прогона (фактические строки вывода)

| Команда | Результат |
|---|---|
| `python -m tools.alpha_content.build_staged_visuals --check` | `STAGED_VISUALS=PASS entries=156` |
| `python -m tools.alpha_content.extract_scatter_table --check` | `SCATTER_EVIDENCE=PASS groups=6` |
| `python -m tools.alpha_content.scatter_tree_translate2 --check` | `SCATTER_TREE=PASS ids=77` |
| `python -m tools.alpha_content.build_world_evidence --check` | `WORLD_CTOR_EVIDENCE=PASS objects=67 items=133 skills=27` |
| `python -m tools.alpha_content.build_held_item_models --check` | `HELD_ITEM_MODELS=PASS items=14` |
| `python -m tools.alpha_content.build_equipment_models --check` | `EQUIPMENT_MODELS=PASS items=32` |
| `python -m tools.alpha_content.build_content_tables --check` | `ALPHA_CONTENT_TABLES=FAIL drift (rebuild the tables)` |

До копирования `.scratch/tmp` три последних в таблице падали не по существу данных, а из-за
отсутствия локальных файлов: `raw sweep missing .../item_models_full.json` и
`FileNotFoundError: .../world_ctor_raw.json`. После копирования — PASS.

## 3a. Сверка кэша с закоммиченной таблицей

Проверка `STAGED_ROWS` ↔ локальный кэш (`.scratch/tmp/cache_match_check.py`, вне Git):

```
STAGED_ROWS=156 KINDS=156
CACHE_VARIANTS=156
MISSING_IN_CACHE=0
NO_RIG_JSON=0
CACHE_MATCH=PASS
```

То есть в общей папке теперь лежат все 156 подготовленных вариантов из
`AlphaStagedVisuals.g.cs`, у каждого есть `rig.json` — визуальный набор пригоден для
Unity PlayMode без Python в рантайме.

## 4. Причина единственного FAIL: артефакт перевода строк, не расхождение данных

`build_content_tables.build()` сгенерировал таблицу, идентичную закоммиченной (73360 байт),
различие ровно в одной строке — провенанс-хеш входного файла:

```
- tools/m0_transfer/data/alpha_confirmed_tables.json sha256=23da642e…faae36   (в файле)
+ tools/m0_transfer/data/alpha_confirmed_tables.json sha256=ecc121b6…c2929f   (сейчас)
```

Причина доказана измерением:

| Байты файла | SHA-256 |
|---|---|
| как лежат в рабочем дереве (LF) | `ecc121b6…c2929f` |
| те же строки с CRLF | `23da642e…faae36` |

`sha()` в `build_content_tables.py` считает `path.read_bytes()`, поэтому хеш зависит от
`core.autocrlf`/checkout, а не от содержимого. На машине, где таблица генерировалась (CRLF),
проверка проходит; в общем каталоге (LF) — «drift». Проверка **машинно-зависима**, данные не расходятся.

## 5. Предлагаемое исправление (передано владельцу сгенерированных таблиц)

1. В `tools/alpha_content/build_content_tables.py` в `sha()` нормализовать переводы строк
   (`path.read_bytes().replace(b"\r\n", b"\n")`) перед хешированием — тогда хеш стабилен на любой ОС.
2. После этого один раз перегенерировать `Assets/CubeGame/Content/Generated/AlphaContentTables.g.cs`
   (изменится только строка провенанса).
Файл в `Assets/**` — зона DeepSeek-2/Claude; сам не правил, запись `HANDOFF` в журнале.

## 6. Ограничения этой проверки

- Unity не запускался: EditMode/PlayMode по объединённому дереву выполнял другой агент
  (`Logs/merge_editmode.xml`: 417/419, 2 skipped — по записи в журнале координации).
- Проверены только генераторы данных и их входы; рантайм-поведение (`AlphaModels`, `Content`)
  этим не подтверждается.
- `commit`/`push` не выполнялись: на момент работы Git-слот удерживал Claude (D87 §5).
