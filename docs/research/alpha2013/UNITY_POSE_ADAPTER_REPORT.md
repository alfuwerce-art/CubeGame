# Unity: отчётность PlayMode, первоначальный FAIL атаки и адаптер поз

**Задача:** `S05A2-D58-B1` (направление B задания от 2026-11-05). Это не продвижение исследования Alpha — исследование поз ведётся отдельно, см. [ALPHA_POSE_EXTRACTION.md](ALPHA_POSE_EXTRACTION.md).
**Среда:** изолированный пилот `.scratch/s05a1/unity/CharacterPilot`, Unity `6000.3.27f2`, Windows, batchmode.
**Статус исполнителя:** `READY_FOR_REVIEW` — три технические проверки пройдены (`PASS`), приёмка за продюсером.

---

## 1. Два дефекта, которые скрывали причину FAIL

### 1.1 Система завершения затирала диагностику сэмплера

`CharacterPilotPlayMode.cs`, `FinishEditorExit` (было ~строки 371–399): при понижении вердикта вызывался `WritePlayModeFailure("FAIL", …)`, который **перезаписывал** `PilotOutput/playmode.txt` и `playmode.json`, только что записанные сэмплером. От исходного прогона оставалось одно сообщение жизненного цикла.

Сохранившийся артефакт до исправления:

```
PLAYMODE_RESULT=FAIL
session=00f0fa4aa3b249e49e260ae372ce2090
frames=40/40
lifecycle: verdict downgraded to FAIL before exit (sampler=True, duplicateFinish=False, duplicateHandler=False, forbiddenExit=False)
```

`playmode_events.txt` того же прогона показывал `SAMPLER_FINISHED code=1`, то есть сэмплер **сам** вынес FAIL, но его причины были стёрты.

**Исправление (FIX-07):** отчёт сэмплера не перезаписывается. Жизненный цикл пишет свои файлы `playmode_lifecycle.txt/.json` и сводный `playmode_verdict.txt/.json`, в который **дословно** встраиваются проблемы сэмплера из `playmode.txt`. Если отчёта сэмплера нет, это фиксируется явно: `sampler_report=not-written` / `not-run`. Lifecycle-only `playmode.txt` больше не создаётся.

### 1.2 Сэмплер не собирал данные о хвате

`PlayModeSampler.cs`: поле `_gripErrors` было объявлено и проверялось (`attack: animator did not report the two-hand grip`), но **нигде не заполнялось**. В режиме атаки FAIL был гарантирован независимо от анимации.

**Исправление:** ошибка хвата снимается каждый кадр в `Update()` через `ProceduralCharacterAnimator.LeftPalmToSecondaryGrip()`; серия, худшее значение и число сэмплов попадают в отчёт.

## 2. Первоначальный FAIL атаки (восстановлен после исправления отчётности)

Первый прогон с сохранённой диагностикой дал три причины (архив `.scratch/s05a2/02_attack_prefix_playmode.txt`):

```
attack: left palm is не число away from the secondary grip in PlayMode
blade tip: does not move in PlayMode (travel 0,2581 < 0,35)
attack: animator did not report the two-hand grip
```

1. **NaN в хвате.** Вход в PlayMode перезагружает сцену (отключён только domain reload, не scene reload); приватные `_gripSword`/`_gripLeftHand` не сериализуются, а `AutoBind` восстанавливал только восемь частей и не восстанавливал хват. `AttachLeftHandToSecondaryGrip` выходил по `null`, `LeftPalmToSecondaryGrip()` возвращал NaN, левая кисть вообще не шла к `secondaryGrip`. **Исправление:** сериализуемый `GripBound` в `ProceduralCharacterAnimator.Grip.cs` и восстановление контракта хвата в `AutoBind` с предупреждением при неудаче.
2. **Метрика атаки зависела от FPS.** 40 реальных кадров batch-прогона покрывали 0.2581 из требуемых 0.35 пробега клинка, то есть измерялась только часть клипа. **Исправление:** в режиме атаки фаза клипа задаётся детерминированно, а не выводится из `Time.time`; порог 0.35 **не менялся** — обоснование записано в комментарии к коду.
3. **Третий пункт** — дефект отчётности из §1.2.

После исправлений: `attack grip contact ok over 40 frames (worst 0)`, `bladeTipTravel=18.7922`, `handTravel=4.3853`.

Дополнительно устранён дефект отчётности форматирования: на культуре `ru-RU` в отчёт попадало «не число» и `0,2581`. Теперь числа пишутся инвариантно (`Num`).

## 3. Адаптер поз

`CharacterPoseAdapter.cs` принимает позу в трёх явно разделённых формах:

| Форма | Метод | Назначение |
|---|---|---|
| матрицы частей (world 4×4) | `ApplyPartMatrices` | источник отдаёт матрицы костей/частей Alpha |
| локальные TRS | `ApplyLocalTrs` | источник уже в иерархии Unity; эталон для сверки |
| процедурные функции `t → TRS` | `ApplyProcedural` | собственные и тестовые клипы |

Заложенные свойства:

* **Порядок parent-first** при применении матриц, иначе `local = inverse(parentWorld) · partWorld` описывает разные позы для родителя и ребёнка.
* **Проверки вместо тишины:** NaN/∞, вырожденный родитель (необратимая матрица), неправдоподобный масштаб, срез (shear), зеркальный масштаб, отсутствующая часть — именованные проблемы; отклонённая поза не двигает `Transform`.
* **Провенанс обязателен:** `ALPHA_DERIVED`, `RECONSTRUCTED_FROM_EVIDENCE`, `ORIGINAL_NOT_ALPHA`, `TEST_DATA_NOT_ALPHA`, `PARTIAL_BINDING` (последняя добавлена под S05A2-D58-A1: числа из Alpha, привязка к части не доказана). `TEST_DATA_NOT_ALPHA` не может быть выдано за Alpha-происхождение, а заявка «это Alpha» без метки `ALPHA_DERIVED` отклоняется.
* **Владение:** адаптер пишет только в переданный набор частей.

## 4. Прогоны

| Прогон | Что проверяет | Результат |
|---|---|---|
| `RunSelfTests` до изменений | базовый набор | `SELFTEST_RESULT=PASS cases=82 failed=0`, exit 0 |
| `-pilotAttack` до исправлений | сохранение исходной диагностики | `PLAYMODE_VERDICT=FAIL`, причины сэмплера сохранены (§2) |
| PlayMode (ходьба) | регресс | `PLAYMODE_RESULT=PASS frames=40/40`, exit 0, lifecycle PASS |
| `-pilotAttack` после исправлений | хват + траектория клинка | `PLAYMODE_RESULT=PASS frames=40/40`, exit 0, `worstGripError=0` при `gripSamples=40`, lifecycle PASS |
| `Run` | сборка персонажа | `CHARACTER_RESULT=PASS`, 8 частей, problems=0, exit 0 |
| `RunSelfTests` после изменений | новые наборы | **`SELFTEST_RESULT=PASS cases=116 failed=0`**, exit 0 |
| `-pilotFrames 10` | негатив: мало кадров | `FAIL`, exit 1, `playmode.txt` сохранён: `only 10 frames were processed, 40 required` |
| `-pilotDoubleFinish` | негатив: двойное завершение | `FAIL`, exit 1; `playmode.txt` остался PASS (отчёт сэмплера цел), lifecycle FAIL (`duplicateFinish=True`) |
| `-pilotSkipAnimator` | негатив: нет аниматора | `FAIL`, exit 1; `playmode.txt` отсутствует, `sampler_report=not-written` + 2 причины |
| `RunAlphaPoseProbe` | проба адаптера на числах Alpha | `ALPHA_POSE_PROBE=PASS` (см. §6) |

## 5. Тесты

Добавлены два набора самопроверок внутри существующего раннера:

* `CharacterPilotPoseAdapterSelfTests` — **22** кейса: точное совпадение с ручным расчётом при переводе матрицы в локальную позу (перенос + равномерный масштаб, повёрнутый родитель), обратимость `матрица → TRS → матрица` в пределах `1e-4`, порядок parent-first, процедурный путь, отказ на NaN/∞/вырожденном родителе/срезе/зеркале/нефинитной фазе, отказ чужой части, запрет метки `TEST_DATA_NOT_ALPHA` как Alpha-происхождения.
* `CharacterPilotPlayModeReportSelfTests` — **12** кейсов отчётности, включая файловый round-trip: writer понижения вердикта не меняет `playmode.txt` байт-в-байт.

Итог: **116** кейсов (было 82, добавлено 34), `failed=0`.

## 6. Проба на настоящих числах Alpha (`PARTIAL_BINDING`)

Добавлен `CharacterPilotAlphaPose.cs` — проба, которая берёт **реальные** значения канала, извлечённые из `Cube.exe` (см. [ALPHA_POSE_EXTRACTION.md](ALPHA_POSE_EXTRACTION.md)), и проводит их через адаптер:

1. читает локальный экспорт `PilotOutput/alpha_pose_channels.json`, созданный `tools/alpha_models/anim/pose_table.py --probe-out …`; файла нет в Git, без него проба даёт `SKIP`, а не выдуманные данные;
2. собирает вращение в порядке Alpha и переводит в базис Unity заменой базиса `(x, y, z) → (x, z, y)` (отражение, поэтому знак угла меняется);
3. применяет к части через `ApplyLocalTrs` с меткой `PARTIAL_BINDING`;
4. сравнивает **кватернион, прочитанный обратно из `Transform`**, с ожиданием, построенным из числа Alpha и замены базиса;
5. отдельно проверяет, что та же поза не может быть объявлена `ALPHA_DERIVED`.

Результат прогона `23_alpha_pose_probe_report.txt`:

```
ALPHA_POSE_PROBE=PASS
provenance=PARTIAL_BINDING
probe: state 1, block 0x004174A0, channel local_15fc[0], moments 3
probe: write_0x0041763E value -100 deg -> applied differs from the Alpha expectation by 0 deg (adapter-vs-request 0)
probe: write_0x004179B1 value  -60 deg -> applied differs from the Alpha expectation by 0 deg (adapter-vs-request 0)
probe: write_0x00417A58 value -250 deg -> applied differs from the Alpha expectation by 0 deg (adapter-vs-request 0)
probe: provenance guard refused the Alpha claim (… requires the ALPHA_DERIVED label, got 'PARTIAL_BINDING')
```

**Проверка не тавтологична:** первый прогон этой пробы упал и нашёл настоящую ошибку — в матрице замены базиса были заданы не те индексы (`m23/m32` вместо `m12/m21`), из-за чего замена была вырожденной и вращение не применялось вовсе (`applied 0 deg`), а проверка осей давала расхождение 90/120°. Ошибка исправлена, повторный прогон — `PASS` с нулевым отклонением. История сохранена в `.scratch/s05a2/20_…21_…22_alpha_pose_probe.log`.

**Чего проба не доказывает:** что канал `local_15fc` относится именно к торсу. Привязка «канал → часть тела» не установлена (§4 [ALPHA_POSE_EXTRACTION.md](ALPHA_POSE_EXTRACTION.md)), поэтому проба применяет канал к торсу только как проверку механизма и помечает всё `PARTIAL_BINDING`.

## 7. Что осталось непроверенным

* Реальные матрицы Alpha (а не углы одного канала) через адаптер не прогонялись: матриц из оригинальной игры нет, пока не решена привязка костей.
* Визуальное сравнение с кадрами Alpha-атаки невозможно: кадров оригинальной атаки нет (см. журнал захвата).
* Остаток хвата = 0 «по построению»: решатель кисти и проверка используют одну формулу, поэтому проверка ловит потерю хвата, но не является независимым геометрическим доказательством.
* В режиме атаки фаза клипа задаётся сэмплером; реальное проигрывание по `Time.time` проверяется только на ходьбе.
* Хуки `-pilotDoubleRegister`, `-pilotExitBeforeEditMode`, `-pilotForceExitEarly`, `-pilotUnbindHair` в этой сессии не прогонялись.
* Все прогоны выполнены на этой машине; независимого прогона ревьюером не было.
