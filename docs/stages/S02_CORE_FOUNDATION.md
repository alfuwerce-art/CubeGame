# S02 — CORE FOUNDATION (первая партия базовых систем CubeGame)

**Проект:** CubeGame (рабочее название игры не утверждено).
**Этап:** `S02-CORE-FOUNDATION`. **Статус:** `AUTHORIZED / IN_PROGRESS` (решение продюсера 2026-11-03, [D43](../decisions/D43-close-s00-s02-core-foundation.md)); после выполнения партии — `READY_FOR_REVIEW`, **не** `ACCEPTED`.
**Ветка:** `stage/s02-core-foundation` (от проверенного `stage/s01-unity-pilot` @ `a8842783378bf2621e6a39a50e1f9ee2398cf245`).
**Unity:** `6000.3.27f2` (Unity 6.3 LTS), Built-in RP. URP/Pipeline в этой партии не подключаются.
**Архитектура:** [ADR-0001](../architecture/ADR-0001-modular-monolith.md) — модульный монолит.
**Числа:** [D42](../decisions/D42-alpha-numbers-as-references.md) — числа Alpha как референсы; тесты сверяются с эталонными скриптами S00-R02.

## 1. Цель и границы

**Цель:** начать реализацию базовых игровых систем CubeGame на Unity 6.3 LTS ограниченной партией из четырёх модулей, проверяемых автоматическими тестами, не расширяя объём.

**Входит в партию:**

| Модуль | Содержание |
|---|---|
| A — Progression | уровень, текущий XP, стоимость следующего уровня, `levelFactor`, `starFactor`, подтверждённые кривые |
| B — Character Stats | модель базовых характеристик, `calcMaxHealth`, подтверждённая часть `calcWeaponDamage`, влияние класса и специализации на HP, явные подтверждённые бонусы экипировки |
| C — Items Foundation | тип, подтип, уровень, редкость, seed, модификаторы, базовые вычисляемые статы, идентичность предмета |
| D — Persistence Foundation | версия формата, персонаж, тестовые предметы, save/load в `Application.persistentDataPath`, защита от повреждённых данных, проверка версий |

**Не входит (запрещено в S02):** генерация мира заново; полноценная боевая система (урон по цели, попадания, блок, комбо, способности); AI/NPC; питомцы; подземелья; квесты; модели и анимации; URP; управление Alpha через DSH; объявление неизвестных чисел Alpha восстановленными; merge в `main`.

**Открытые вопросы S00-R02 не блокируют партию:** OQ-22 `PARTIAL`, OQ-23 `OPEN/BLOCKED`, OQ-24 `PARTIAL` остаются как есть. В S02 нет движения и скоростей, поэтому `PLACEHOLDER_NOT_ALPHA` применяется только к будущим скоростным параметрам.

## 2. Архитектура

```
Assets/CubeGame/Core/            (asmdef CubeGame.Core, noEngineReferences = true)
  Progression/   AlphaMath, ProgressionState
  Characters/    CharacterClass, CharacterStats, CharacterSheet
  Items/         ItemEnums, ItemInstance, ItemStatsCalculator
  Persistence/   SaveGame, SaveContracts, SaveService, CharacterSaveMapper
Assets/CubeGame/Persistence/     (asmdef CubeGame.Persistence) — FileSaveStore, JsonSaveSerializer
Assets/CubeGame/Presentation/    (asmdef CubeGame.Presentation) — CharacterSheetPresenter
Assets/CubeGame/Tests/EditMode/  — табличные тесты A/B/C/D
Assets/CubeGame/Tests/PlayMode/  — smoke-тесты в запущенной сцене
```

Соблюдено разделение:

1. **Чистая логика и формулы** — `CubeGame.Core`, без ссылок на UnityEngine (`noEngineReferences: true`).
2. **Игровые данные и модели** — `ItemInstance`, `CharacterSheet`, `SaveGame` (плоские, переносимые).
3. **Сохранения** — `SaveService` (чистая логика кадра/проверок) + `FileSaveStore`/`JsonSaveSerializer` (Unity).
4. **Unity-представление** — `CharacterSheetPresenter` (минимальная связка, без геймплея).
5. **Тесты** — EditMode (таблицы формул) и PlayMode (работа в сцене).

Штатные возможности Unity используются там, где они есть: `JsonUtility` для сериализации, `Application.persistentDataPath` для пути, `File.Replace` для атомарной замены, Test Framework для тестов. Собственный код — только специфические правила CubeGame (кривые Alpha, статы, формат) и контроль целостности файла.

Новых пакетов не добавлено.

## 3. Источники чисел (D42)

| Что | Значение | Статус | Источник |
|---|---|---|---|
| `cost(L) = int((1-1/((L-1)*0.05+1))*1000+50)` | L=1→50, 10→360, 100→881, асимптота 1050 | `REFERENCE_VERIFIED` | `D_character §D4.1`, `alpha_curves_reference.py` |
| `levelFactor(L) = 2^(curve05(L)·3)` | L=1→1, 5→1.4142, 11→2, предел 8 | `REFERENCE_VERIFIED` | `E_combat §E2`, тот же скрипт |
| `starFactor(S) = 2^(0.25·S)` | 0→1, 4→2, 8→4 | `REFERENCE_VERIFIED` | `E_combat §E2` |
| `maxHP = base·levelFactor·2.0·classMult·specMult + equipment` | Warrior L1: 268.0 в реальном сейве | `REFERENCE_VERIFIED` (формула/значение), `PARTIAL` (что именно даёт +8 экипировки — OQ-25) | `D_character §D4.2` |
| Множители HP класса | Warrior 1.3, Ranger 1.1, Mage 1.0, Rogue 1.2 | `REFERENCE_VERIFIED` | `D_character §D4.2` |
| `specMult` 1.25 для `class == 1 && spec == 1` | Warrior + Guardian | `PARTIAL` (вывод из `+0x140 = classIdx+1`) | `D_character §D4.2` |
| Урон оружия | `base(0x180)·levelFactor` + флаги `0x8`/`0x10` | `REFERENCE_VERIFIED` частично; множители типа атаки — `UNKNOWN` | `E_combat §E2` |
| Статы предмета | `levelFactor(level+0.1·powerBonus)·starFactor(rarity)·M_type·M_mod` | `REFERENCE_VERIFIED` | `F_items §F2`, `alpha_item_stats_reference.py` |
| Редкость | 1 %/0.1 %/0.01 %, кламп 4 | `REFERENCE_VERIFIED` | `F_items §F3` |

## 4. Обязательные проверки

| № | Проверка | Где |
|---|---|---|
| 1 | Компиляция Unity без ошибок | `-batchmode -quit` импорт, лог без `error CS` |
| 2 | EditMode-тесты прогрессии | `ProgressionTests` (таблицы XP/множителей + переходы уровней) |
| 3 | EditMode-тесты характеристик | `CharacterStatsTests` (268 HP, классы, специализация, урон) |
| 4 | Тесты структуры и статов предметов | `ItemStatsTests` (таблицы F2, идентичность, валидация) |
| 5 | Round-trip сохранения | `PersistenceTests.SaveAndLoadRoundTripPreservesState` + `SaveSurvivesRestartWithNewServiceInstance` |
| 6 | Тест повреждённого сохранения | `CorruptedTextIsReportedWithoutException`, `TruncatedSaveIsDetected`, `TamperedPayloadIsDetectedByChecksum`, `NewerFormatVersionIsRejected` |
| 7 | PlayMode smoke | `CoreFoundationPlayModeTests` + регрессия `PilotPlayModeTests` |
| 8 | Регрессия пилота | EditMode `ChunkMeshTests`, `TerrainDeterminismTests` + `PilotPlayModeTests` |

Фактические результаты прогона — в [S02_REPORT.md](S02_REPORT.md). Если проверка не выполнялась, в отчёте стоит `NOT_RUN`, а не `PASS`.

## 5. Критерии приёмки партии

- [ ] Unity-проект компилируется без ошибок.
- [ ] Базовые характеристики персонажа вычисляются по референсам (268 HP воспроизводится).
- [ ] XP и уровень изменяются корректно (включая несколько уровней за вызов).
- [ ] Предметы представлены типизированными данными с вычисляемыми статами.
- [ ] Сохранение переживает перезапуск (новый экземпляр сервиса/компонента).
- [ ] Все обязательные автоматизированные тесты выполнены, результаты зафиксированы.
- [ ] Существующая сцена Unity не сломана (регрессия PlayMode проходит).
- [ ] Нет неподтверждённых механик, выданных за точную Alpha.
- [ ] Код опубликован в отдельной рабочей ветке GitHub.

## 6. Новые вопросы, найденные в S02 (не закрывать догадкой)

| ID | Вопрос | Как закрыть | Статус |
|---|---|---|---|
| OQ-25 | Какая именно характеристика предмета даёт `+8` к `maxHP` в проверенном сейве (и даёт ли формула предмета HP вообще)? | сравнение сейвов с разной экипировкой, дизасм места суммирования `equipmentBonuses` | `OPEN` |
| OQ-26 | Семантика флагов `0x7e & 0x8` / `0x7e & 0x10` (апгрейды оружия) | дизасм установки флагов, прогон с улучшенным оружием | `OPEN` |

## 7. Ссылки

- [D43 — закрытие S00 и авторизация S02](../decisions/D43-close-s00-s02-core-foundation.md)
- [D42 — числа Alpha как референсы](../decisions/D42-alpha-numbers-as-references.md)
- [R02_REPORT](../research/alpha2013/R02_REPORT.md), [SUBSYSTEM_TO_UNITY_MAP](../research/alpha2013/SUBSYSTEM_TO_UNITY_MAP.md)
- [S02_REPORT](S02_REPORT.md) — фактические результаты прогона
