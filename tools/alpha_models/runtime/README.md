# `tools/alpha_models/runtime` — захват структуры существа во время `Creature_drawModel`

Инструменты задачи `S05A2-D58` ([D57](../../../docs/decisions/D57-continuous-animation-transfer-authorization.md),
[D58](../../../docs/decisions/D58-animation-research-autonomy.md)): получить **реальный адрес
существа** в рантайме и привязать модель к её анимационной матрице.

| Файл | Назначение |
|---|---|
| `model_read_scan.py` | статический поиск инструкций, читающих 8 индексов моделей `creature+0x8c..+0x9a` (чистый Python, тестируется) |
| `drawmodel_probe.py` | рантайм-отладчик (ctypes): точка останова на чтении индекса, съём `eax`=существо / `edi`=this / `esi`=вектор моделей |

## Что доказано (S05A2-D58)

`Creature_drawModel` = `Cube.exe` RVA `0x128F0`. Восемь `int16`-индексов моделей лежат в
полях существа `+0x8c..+0x9a` и читаются пачкой `movsx ecx, word ptr [eax+0xNN]`, где
`eax` = `*this` (указатель существа), а `edi` = `this` (объект отрисовки):

| адрес (RVA) | поле | слот модели |
|---|---|---|
| `0x12A42` | `+0x8c` | `self[0x2f]` |
| `0x12A6C` | `+0x8e` | `self[0x30]` |
| `0x12A9F` | `+0x90` | `self[0x33]` |
| `0x12AD2` | `+0x92` | `self[0x36]` |
| `0x12B05` | `+0x94` | `self[0x31]` |
| `0x12B38` | `+0x96` | `self[0x35]` |
| `0x12B6B` | `+0x9a` | `self[0x34]` |
| `0x12B9E` | `+0x98` | `self[0x32]` |

Индекс — это позиция в глобальном векторе моделей (`param_4`, в `esi`): `модель =
*(int*)([esi+4] + index*4)`. Значение индекса равно ключу `data1.db` (проверено: 333 →
`dragon-body.cub`, 334 → `dragon-foot.cub`).

## Запуск

```powershell
python -m tools.alpha_models.runtime.model_read_scan --exe <Cube.exe> --start 0x4128f0 --end 0x423f20
python -m tools.alpha_models.runtime.drawmodel_probe <pid> out.json [max_hits]
python -m unittest tools.alpha_models.tests.test_model_read_scan
```

Игра запускается из изолированной копии (`.scratch/alphatest2`), только чтение процесса;
записывается один байт `INT3`, который отладчик восстанавливает.

## WOW64-особенности (проверены `self_check`)

64-битный отладчик видит в 32-битном коде `INT3` как `STATUS_WX86_BREAKPOINT`
(`0x4000001F`), а single-step как `0x4000001E`; `DEBUG_EVENT` выровнен на 8 байт, запись
исключения начинается со смещения 16. Отсоединение чистое: байт восстанавливается,
незавершённый single-step доводится до конца (снимается trap-флаг), и перед
`DebugActiveProcessStop` ставится `DebugSetProcessKillOnExit(FALSE)`.

## Границы достоверности

Инструмент отдаёт **указатель существа и индексы моделей**, а не позы. Соответствие
«индекс → часть тела» и «слот модели → матрица части → канал» выводится из статики
`GameController.cpp` и зафиксировано в отчёте [ALPHA_POSE_EXTRACTION.md](../../../docs/research/alpha2013/ALPHA_POSE_EXTRACTION.md).
