# S05A2-CONTINUE-02 — Dynamic Parts и Action Lifecycle

2026-11-06, Codex. Ветка `codex/s05a2-action-lifecycle`, база `222e700d1d6f071bdb45d76fdb6d069462431e29`. Статус результата **READY_FOR_REVIEW**; этап **S05A-2 IN_PROGRESS / REVIEW OPEN**, S05B **NOT_AUTHORIZED**.

## Результат

Пять прежних блокировок 22/23/26/71/72 закрыты обобщённой видимостью частей: исходная матрица хранится только когда Alpha действительно рисует часть. Появление и скрытие проверены в настоящем Unity PlayMode. Все прежние **93 клипа побайтово сохранены**; реализация принятого продюсером хвата второй руки не перерабатывалась.

В текущем реестре **98 движущихся action ID + один HELD_STATE (79)**. 51 и 80 имеют конкретные контекстные блокеры. Три дополнительных `native_057/058/067` — варианты тех же ID с исходным input-clock, а не ещё три способности. Поэтому локальный manifest содержит 102 успешных clip records и две BLOCKED записи; это **не** утверждение «101/101 игровых действий восстановлены».

Восстановлен ограниченный контур primary hold/комбо/позднего restart/idle timeout; проверены 784 native golden случая в двух профилях. Дополнительно исследованы helper/input-tail функции всех 101 ID × два контекста (202), а также 18 вариантов class/spec/weapon selector. Полный запуск способностей, ресурсы, world effects и общий контур отмены остаются `PARTIAL / UNKNOWN`. Таблица доказанности и классификация 51/79/80: [ALPHA_ACTION_LIFECYCLE](../research/alpha2013/ALPHA_ACTION_LIFECYCLE.md).

## Проверки

Открытый реестр **только метаданных/статусов** всех 101 ID: [HUMANOID_ACTION_STATUS.csv](../research/alpha2013/HUMANOID_ACTION_STATUS.csv). `input_helper_evidence` относится к обследованным class-1/4, spec-0, subtype-15, offset70=0 контекстам, не к полной игре. Локальный manifest дополнительно содержит закрытые клипы и их хеши, поэтому в Git не публикуется.

| Проверка | Фактический результат |
|---|---|
| Python `unittest discover -s tools/alpha_models/tests -v` | **192 PASS**, включая сохранность 93 payloads, sparse masks, повреждения, native helper boundaries и consumable context |
| CLI verdict/isolation negative tests | **10/10 PASS**; ненулевой exit и старый PASS не принимаются |
| Unity 6000.3.27f2 EditMode | Базовые **30/30** (29 прежних + регрессия сброса native lifetime); дополнительные **919/919**, в том числе 784 решения исходных helper/input-tail функций, отрицательные маски/тайминги/категории, настоящие Renderer и все подготовленные клипы |
| Geometry / все native visible keys / loop / grip | `TRANSFER_RESULT=PASS`, геометрия восьми частей и исходные пороги сохранены |
| Formula/reference | `PARAMWALK_RESULT=PASS`: 900 golden, 446 Alpha walk frames; отдельный weapon oracle, max_abs≈0.0000013 |
| Live Alpha replay текущим эмулятором | **3115/3115 PASS, max_abs=0**, порог 0.0001; наблюдались 0/57/59/60. Для новых пяти динамических ID живого capture нет, статус `EMULATED_ALPHA_CODE` |

| Гц | Каталог 98 действий: реальные кадры | Native lifecycle: реальные кадры | Результат процесса и артефактов |
|---|---|---|---|
| 30 | 18023 | 1484 | PASS, все 8 шагов exit 0 |
| 60 | 35927 | 2962 | PASS, все 8 шагов exit 0 |
| 120 | 71585 | 5919 | PASS, все 8 шагов exit 0 |

В обоих сценариях на каждой частоте проверяются actual transforms и Renderer, все фазы, finite values и исходные пороги. Для каталога ошибки поз/перехода recovery — 0. Для native-сценария ошибки поз и handover — 0; проверяются отдельный strike/recovery при активном native action, timer carry, удержание 79 более 10 с, прерывание primary и видимость 26. Границы решений проверены на целых миллисекундах golden-набором; кадровая смена input-state происходит на первом подходящем кадре частоты. Проверка source clock допускает 0.5 мс накопления float, фактический максимум указан в каждом отчёте.

30/60/120 — фиксированный шаг `Time.captureDeltaTime` в реально исполняемом PlayMode, не замер производительности GPU. Кадры и проверки выполнены редактором, без подмены Unity офлайн-рендером.

Максимальная ошибка source clock: 30 Hz=0.015525 ms; 60 Hz=0.032654 ms; 120 Hz=0.063375 ms.

Пороги хвата прежние: main ≤0.05 вокселя, second radial ≤2.5, выход за конечную рукоять ≤0.001. В новых native-вариантах проверка ударного хвата ограничена **исходным strike window**, а state может оставаться активным до native timeout. Это разделение фаз; прежние 93 проверки не ослаблены.

Первый новый сценарий обнаружил преждевременное переключение стадий теста — исправлено. Также выявлен runtime-only scene script reference: новый `MonoBehaviour` находился в файле с другим именем, восстановление сцены вызывало reload и потерю exit callback. Компонент вынесен в `AlphaNativeDriver.cs`; незавершённые прогоны с report PASS и exit −1 записаны как **FAIL**. Дополнительная регрессия гарантирует, что standalone action после native-владельца сбрасывает его lifetime и завершается штатно. Финальные результаты требуют отдельного свежего session и exit 0.

## Локальные материалы

Все пути ниже относительно единственного `D:\Projects\CubeGame`:

- `.scratch/lifecycle/library/actions.json` — полный реестр, SHA256 клипов, две контекстные блокировки; `lifecycle.json` — профили и golden.
- `.scratch/lifecycle/library_integrity.json` — 93 прежних файла без изменений и события пяти масок.
- `.scratch/lifecycle/research/input_survey.json` — 202 helper-контекста и 18 вариантов selector; не полный gameplay lifecycle.
- `.scratch/lifecycle/runs/{30,60,120}/` — отдельные свежие отчёты Unity; `.scratch/lifecycle/final_{30,60,120}.log` — вердикты с кодами выхода всех шагов.
- `.scratch/lifecycle/python_final.log`, `runner_negative.log`, `oracle_replay.json` — независимые проверки.
- `.scratch/lifecycle/visuals/native_preview.gif`, `native_contact_sheet.png` — **реальные Unity frames**, визуально просмотрены. GIF сокращает долгие периоды ожидания; timer и action подписаны, скрытое оружие показано явно. Полная длительность подтверждается журналом, а не длиной GIF.
- `.scratch/s05a2/unity/AlphaTransfer/TransferOutput/native_frames/` и `native_frames.tsv` — исходные кадры финального сценария; история старых выходов сохраняется штатным runner.

В Git публикуются собственные инструменты, синтетические тесты и описания. Бинарник, защищённые модели, клипы, матрицы, исходные native rule JSON и картинки остаются локально. Не создавались копии репозитория и новые персонажи. S04, main и чужие незакоммиченные файлы не изменены. Предыдущий исходный результат: [S05A2_CLAUDE_CONTINUATION_REPORT](S05A2_CLAUDE_CONTINUATION_REPORT.md).

## Оставшаяся работа

1. Проверить полные GUI/resource/world gates и дальнейшие charged/cancel пути на реальных входах; helper survey уже даёт точки для следующего исследования.
2. Для 79 — восстановить активацию целиком и движущуюся stealth-позу; test activation не считать готовой способностью.
3. Для 80 — подготовить подтверждённый активный consumable context и его модель в пределах отдельного разрешённого исследования; 51 не переносить как универсальную атаку человека.
4. При необходимости получить живые capture новых динамических состояний/комбо; до этого сохранять `EMULATED_ALPHA_CODE`.
5. Художественная и итоговая приёмка S05A-2 — за продюсером. S05B не начинать.
