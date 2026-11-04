# `tools/alpha_models/anim` — восстановление поз Alpha 2013 из машинного кода

Инструменты задачи `S05A2-D58-A1` ([D57](../../../docs/decisions/D57-continuous-animation-transfer-authorization.md), [D58](../../../docs/decisions/D58-animation-research-autonomy.md)). Отчёт с числами и ограничениями — [ALPHA_POSE_EXTRACTION.md](../../../docs/research/alpha2013/ALPHA_POSE_EXTRACTION.md).

| Файл | Назначение |
|---|---|
| `pe_bytes.py` | минимальный PE32-ридер (RVA ↔ файловое смещение) и поиск вызовов `E8 rel32`; только стандартная библиотека |
| `alpha_writes.py` | восстановление сайтов `*arrayElem_stride4(i) = value`: скрытая база в `ECX`, компонента из `push`, значение из `mov [eax],imm32` / `movss [eax],xmm0` |
| `pose_table.py` | CLI: таблица состояний `Creature_drawModel` (`switch` @ `0x413AF6`, таблица @ `0x423D4C`) и сверка с реконструкцией |

## Зачем это нужно

В Alpha анимаций как файлов нет: поза вычисляется кодом из `state + timer_ms`. Восстановленный декомпилятор теряет `this` у аксессора `arrayElem_stride4`, поэтому в псевдокоде не видно, **куда** записывается угол. Инструмент берёт эту информацию из байтов игры, а не из догадок, и затем сверяет результат с декомпиляцией.

## Запуск

```powershell
python -m tools.alpha_models.anim.pose_table --exe <путь к Cube.exe> --state 1
python -m tools.alpha_models.anim.pose_table --exe <путь к Cube.exe> --all --json pose_table.json
python -m unittest tools.alpha_models.tests.test_alpha_write_scan
```

`Cube.exe` открывается только на чтение; инструмент ничего не пишет рядом с игрой. Локальная копия игры и реконструкция в Git не входят.

## Границы достоверности

* Слоты и компоненты восстанавливаются точно (все 522 записи попадают в имена переменных декомпиляции).
* Статические значения восстанавливаются частично: вычисляемые значения (`movss [eax],xmm0`) статически недоступны и помечаются `не подтверждено`.
* Соответствие «стековый слот → часть тела» **не** устанавливается этим инструментом — см. §4 отчёта.
