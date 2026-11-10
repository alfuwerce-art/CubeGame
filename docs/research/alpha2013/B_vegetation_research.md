# B — Растительность Alpha: исследование (2026-11-10)

**Статус:** `RESEARCH — READY_FOR_IMPLEMENTATION`
**Автор:** DeepSeek (продюсер авторизовал «Растительность (research W-18 scatter)»).
**Источник:** декомпиляция `Cube.exe` (Ghidra headless), `.scratch/CubeWorld-Reversal/cube/game_misc/game_misc.cpp`.

---

## 0. Главный вывод

**Текущий CubeGame ошибочно использует `WorldInfo_scatterObjectsInArea @ 0x5F56C0` как «растительность»**
(`AlphaVegetationPlanner` → `AlphaScatterSelector` → `AlphaScatterTree`). На самом деле `0x5F56C0` выбирает
**шаблоны существ и объектов** (волк 0x1C, кот 0x20, подземные 0x91–0x9B и т.д.) — это подтверждает F1A.

Настоящая растительность — **две отдельные функции**:

| Функция | Адрес | Назначение |
|---|---|---|
| `World_generateTreeRoot` → `World_generateVegetationCluster` | `0x5D9AD0` → `0x5D8750` | **деревья** (виды 0x01–0x1E по климату) |
| `World_serializeCellData` | `0x5F6E20` | **посев декора** (мелкая растительность/камни) |

Обе вызываются из `WorldInfo_generateBiomeContent @ 0x5E4850` (см. `B_world.md` §B8, строка 127).

---

## 1. Деревья — `World_generateTreeRoot @ 0x5D9AD0`

Выбор вида дерева по климату клетки (поля `cell+0x0c` и `cell+0x10` — температура/влажность, точная
привязка поля к величине — `PARTIAL`):

```text
default                          kind ∈ [1, 10]      (0x01–0x0A)
cell+0x10 < 0.2                  kind ∈ [10, 20]     (0x0A–0x14)   холодно/сухо
cell+0x0c < 0.2 && cell+0x10>0.8 kind ∈ [15, 25]     (0x0F–0x19)   жарко-сухо
cell+0x0c > 0.8 && cell+0x10>0.8 kind ∈ [10, 20]     (0x0A–0x14)   жарко-влажно
cell+0x08 == 1 (горы?)           kind ∈ [20, 30]     (0x14–0x1E)
kind = rand() % (max - min + 1) + min
```

- Ориентация `rand() & 3`, позиция со смещением `2 + (rand() & 3)` вокселей внутри клетки.
- На чанк — до 2 кластеров: `rand() % 6 == 0` → `World_generateVegetationCluster`.
- Дерево строится вызовом `dtor_listBuffer(chunk_x, chunk_z, kind)` (регистрация объекта).

## 2. Декор — `World_serializeCellData @ 0x5F6E20`

```text
rand() % 0x32 == 0  (редко, 1/50): kind 10,                scale (1.5, 1.0, 1.0)
иначе rand() % 3:
  0: мелкая растительность kind 0x20–0x2B (по типу биома), scale (2.0, 1.0, 1.57)
  1: средний объект          kind 0x0C–0x0F (по типу),     scale (3.0, 3.0, 1.0)
  2: мелкий камень           kind 0x10–0x11 (по типу),     scale (1.0, 1.0, 0.5)
```

Биомный параметр `type` (3/4/5/прочее) сдвигает базовый вид:
- мелкая растительность: type3 → 0x20+, type4 → 0x26+, type5 → 0x29+, иначе 0x23+;
- средний объект: type4 → 0x0D, type5 → 0x0E, иначе 0x0C;
- мелкий камень: type4 → 0x0F, type5 → 0x11, иначе 0x10.

Масштаб декодирован из IEEE-754: `0x40000000=2.0`, `0x3fc8f5c3≈1.57`, `0x40400000=3.0`,
`0x3f800000=1.0`, `0x3f000000=0.5`, `0x3fc00000=1.5`.

## 3. Модели уже в кэше

Все 156 акторов содержат и растительность: `d86_kind_0x01…0x1e_formula` (деревья),
`d86_kind_0x20…0x2b_formula` (мелкая растительность), `d86_kind_0x0c…0x11_formula` (средние/камни),
`d86_kind_0x78_formula` (растение) и т.д. — модели конвертированы и стейджятся в Player (R0).

## 4. План реализации (следующий шаг)

1. Заменить/дополнить `AlphaZoneContentProvider` (или новый провайдер `AlphaVegetationProvider`):
   выбор деревьев по климату (`generateTreeRoot`) вместо `AlphaScatterTree` (существа).
2. Поднять `VegetationPerZone > 0` в `D3ZonePopulation` (или завести отдельный канал) — семантика
   «сколько деревьев/декора на зону» переносится из `generateTreeRoot`/`serializeCellData`.
3. Декор `serializeCellData` — как пассивные объекты (faction 6) с масштабами из таблицы выше.
4. Проверить: деревья/растительность в Player → `REAL_ACTOR_VIEWS` вырастет на растительность,
   `MAGENTA_COUNT=0`, без подмены существ.

## 5. Provenance

- Деревья/декор — `ALPHA_DECOMPILED` (логика выбора вида/масштаба из байтов), но живой сверки с Alpha нет → `PARTIAL` до визуальной приёмки.
- Точная привязка `cell+0x0c`/`cell+0x10` к «влажность/температура» — `PARTIAL` (нужна сверка с `AlphaClimateGrid`).
- Позиции внутри клетки и число кластеров — из байтов (`rand()&3`, `rand()%6`), без плотностной политики.
