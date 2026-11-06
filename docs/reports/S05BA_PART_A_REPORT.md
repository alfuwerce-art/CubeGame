# S05B-A, часть A — пилот действий NPC трёх семейств (отчёт)

**Статус: `READY_FOR_REVIEW`** (не ACCEPTED). **Решение:** [D60](../decisions/D60-s05ba-npc-actions-authorization.md). **План:** [S05B_NPC_ACTIONS_PLAN_DRAFT](../stages/S05B_NPC_ACTIONS_PLAN_DRAFT.md). **Исследование:** [NPC_ACTION_TRIGGERS](../research/alpha2013/NPC_ACTION_TRIGGERS.md).
**Дата:** 2026-11-06. **Исполнитель:** Claude Code. **Ветка:** `claude/s05ba-npc-actions`.
**Итог:** три профиля действий `creature_ai` (Cat 0x1e, HumanFemale 0x03, PlainRunner 0x3f), запуск каждого действия доказан исполнением байтов ИИ Alpha, позы — оракулом `drawModel`; Unity-пилоты 3/3 PASS (по 8/8 шагов, 60 Гц); живой захват — `NOT_CAPTURED` (решение продюсера: «к A2 без захвата»). **Шлюз A→B: условия выполнены — ждёт рассмотрения продюсером.** Часть B не начата.

## 1. Что сделано

| Файл | Изменение |
|---|---|
| [ai_trigger.py](../../tools/alpha_models/universal/ai_trigger.py) | новый: исполнение участка выбора действия тика ИИ `CombatBehavior::vfunc_0` (`0x42D87F…0x42EAEF`) на внешности вида; перебор входов мира (дистанция, ресурс, `rand`, счётчик серии, перезарядки способностей, предыдущее действие); `ability_isUsable` исполняется по-настоящему; анализ на состоянии, снятом с живого NPC |
| [contract.py](../../tools/alpha_models/universal/contract.py) | 3 профиля `creature_ai` с `launched_actions` (действие → адрес записи), `live_check`, без экипировки |
| [action_baker.py](../../tools/alpha_models/universal/action_baker.py) | профиль без оружия; `ACTION_TRIGGER_NOT_PROVEN` для действия вне `launched_actions`; `EQUIPMENT_UNVERIFIED` для неподтверждённого оружия; в клипе — триггер и `live_check` (provenance остаётся `EMULATED_ALPHA_CODE`) |
| [pipeline.py](../../tools/alpha_models/universal/pipeline.py) | импорт: профиль библиотеки должен совпасть, действие без доказанного запуска не импортируется; слой оружия только при оружии; клипы семейства рядом с библиотекой действий |
| [transfer_planner.py](../../tools/alpha_models/universal/transfer_planner.py) | библиотеки `.scratch/s05ba`; задания без оружия; 0x03 в списке пилота (D60, A2) |
| [test_npc_actions.py](../../tools/alpha_models/tests/test_npc_actions.py) | 15 тестов: правила профилей, отказы, исполнение байтов ИИ |

`BATCH_AUTHORIZATIONS` не менялся: `(("D59", "s05b1"),)`.

## 2. Профили

| Вид | Флаги `+0x7e` | Запускаемые действия (адрес записи) | Состояние drawModel / длительность | Unity |
|---|---|---|---|---|
| Cat 0x1e | `0x0031` | 75 (`0x42E2BF`) | 44 / 1085 мс | 8/8 PASS |
| HumanFemale 0x03 (NPC, без оружия — наблюдалось) | `0x0000` | 54 (`0x42E627`), 3/4 (`0x4458A0`), 6 (`0x4455D7`) | 1/900, 18/492, 17/492, 20/690 мс | 8/8 PASS, 4/4 действия |
| PlainRunner 0x3f | `0x0028` | 6 (`0x4455D7`) | 20 / 690 мс | 8/8 PASS |

Условия (ИСПОЛНЕНО): кот — когда таймер действия превысил `combat_getAttackTiming`; гуманоид — 54, если способность 0x36 не на перезарядке и ресурс ≥ 0.8, иначе 3/4 по чётности счётчика серии, при ресурсе < 0.8 — 6; бегун — 6 через `combat_advanceAttackTimer`. Выбор цели, агрессия и дистанция до участка — `PARTIAL`.

Все клипы: `EMULATED_ALPHA_CODE`, полный таймлайн, неизменный набор частей, возврат в покой ≤ 3e-7 вокселя. В Unity каждое действие проходит entry → action → recovery → exit → idle с отклонением от клипа 0 / 0°, ходьба = формула (0 / 0°), дрейф покоя 0.

## 3. Правила D60 — как выполнены

1. **Provenance:** все клипы `EMULATED_ALPHA_CODE`; `live_check = NOT_CAPTURED` отдельным полем; тест запрещает `TRANSFERRED_FROM_ALPHA` у эмулированных профилей.
2. **Запуск по виду:** профиль только у образца; BrownCat/WhiteCat, LeafRunner/SnowRunner/DesertRunner и 6 других гуманоидов остаются `BLOCKED: POSES_FAMILY_COMPATIBLE_TRIGGER_UNPROVEN` (тесты), хотя П1 уже показывает у них тот же результат — подтверждаются отдельно в части B.
3. **Экипировка:** у городских NPC-гуманоидов оружия нет (полный дамп 6 NPC); оружие NPC не наблюдалось — любое оружие в задании → `EQUIPMENT_UNVERIFIED` (тест).
4. **Шлюз A→B:** см. §5.

## 4. Живой захват (П3)

Копия Alpha из `.scratch` запускалась (D60 п.1). Найден только дружелюбный Cat 0x1e (фракция 5) и NPC-гуманоиды фракции 3; по `CombatBehavior_isHostileTo` (`0x596CA0`) они не враждебны игроку и боя не начинают. Враждебные существа — за городом. По решению продюсера A2/A3 выполнены без захвата. Фракцию в памяти не меняли. Полезный результат запуска: класс и экипировка реальных NPC-гуманоидов (вход П1 для A2).

## 5. Шлюз A → B

| Условие | Результат |
|---|---|
| Три профиля подтверждены (П1 + П2, Unity PASS) | PASS (3/3) |
| Python | 251/251 OK |
| Регрессия HumanMale 98/98, прежние числа | 60 Гц: 98/98, 35 927 кадров, native 2 962; 30 Гц: 98/98, 18 023 / 1 484; 120 Гц: 98/98, 71 585 / 5 919 — совпадает |
| Отчёт части A продюсеру | этот документ |

Запись `BATCH_AUTHORIZATIONS` для части B не добавлена: по D60 — после рассмотрения этого отчёта.

## 6. Ограничения

* `live_check = NOT_CAPTURED` у всех трёх профилей.
* Ветки ИИ особых видов (0x19, 0x65, 0x68, 0x74, 0x76, 0x77, 0x2f…) и водных существ не исследовались.
* Оружие NPC не наблюдалось; профили без экипировки.
* Технический PASS ≠ художественное одобрение; переходы — `UNITY_TRANSITION`.
* Данные Alpha (захваты, библиотеки, кадры) — только в `.scratch`.

## 7. Воспроизведение

```
python -m tools.alpha_models.universal.ai_trigger --kind 0x1e
python -m tools.alpha_models.universal.action_baker --job <job kind 0x1e> --actions 75 --out .scratch/s05ba/cat_0x1e/library
python -m tools.alpha_models.universal.action_baker --job <job kind 0x03> --actions 3,4,6,54 --out .scratch/s05ba/humanfemale_0x03/library
python -m tools.alpha_models.universal.action_baker --job <job kind 0x3f> --actions 6 --out .scratch/s05ba/plainrunner_0x3f/library
python -m tools.alpha_models.universal.transfer_planner pilot --kinds 0x1e,0x03,0x3f --out-dir .scratch/s05ba/pilot --execute
python -m unittest discover -s tools/alpha_models/tests -t .
```
