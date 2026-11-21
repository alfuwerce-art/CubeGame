# M0-00 — матрица Alpha 2013 → CubeGame/Unity по семи системам

**Статус:** `PREPARATION / READY_FOR_REVIEW`, не `ACCEPTED`. Дополняет подробную
[матрицу 176 механик](M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX.md), не заменяет её.
**Дата:** 2026-11-09. **Ветка:** `deepseek/m0-00-full-systems-preparation`.
**Источники:** M0-00B/P0 evidence, behavior specs, S02–S04 код, S05B-2/D69 отчёты.
**Проверки:** `M0_PILOTS_RESULT=PASS checks=462 failed=0`; реестр 176 строк.

Обозначения:
`evidence` — `DIRECT` (код/данные Alpha), `PARTIAL` (каркас), `UNKNOWN`, `BLOCKED`;
`Unity/Core` — `CORE` (принятый Core), `PILOT` (изолированный стенд M0-00), `TEST_ONLY`,
`PARTIAL`, `MISSING`, `N/A`;
`pilot` — `W` = PILOT_WORLD, `M` = PILOT_MOVEMENT, `C` = PILOT_COMBAT, `N` = PILOT_NPC,
`I` = PILOT_ITEMS, `EQS` = CONTRACTS_ECONOMY_QUESTS_SAVES.

## Сводка

| # | Система | Строк в реестре | DIRECT | PARTIAL | UNKNOWN/BLOCKED | Есть в Core/пилоте | Готовность к массовому переносу |
|---|---|---:|---:|---:|---:|---|---|
| 1 | Мир и генерация | 20 | 12 | 5 | 3 | 0 Core / W | Формула региона и координаты — да; рельеф/биомы — нет |
| 2 | Передвижение | 20 | 12 | 4 | 4 | 3 Core / M | Константы и кривые — да; физика — после OQ-23 |
| 3 | Бой | 40 | 28 | 8 | 4 | 9 Core / C | Формулы и таблицы — да; эффекты/оружие NPC — нет |
| 4 | NPC/AI/спавн | 32 | 20 | 7 | 5 | 0 Core / N | Схема/состояние — да; поведение — по семействам |
| 5 | Предметы | 28 | 18 | 6 | 4 | 2 Core / I | Таблицы — да; 24 типа и сейв экипировки — блокеры |
| 6 | Экономика/задания | 9 (+Q-16…21, Q-26) | 4 | 2 | 3 | 0 / EQS | Модель задания — да; торговля — нет |
| 7 | Прогрессия/смерть/сейвы | 12 | 8 | 2 | 2 | 1 Core / EQS+W | Кривые/round-trip — да; правило смерти — нет |
| — | UI/контент (вне семи) | 15 | 6 | 3 | 6 | 1+1 / — | Не входит в production-системы этого пакета |

> Числа по строкам — оценка по реестру и подробной матрице; домены реестра:
> `world=20, movement=20, combat=40, npc=32, items=28, economy_quests=9,
> progression_persistence=12, content_ui=15` (176 всего).

---

## 1. Мир и процедурная генерация

| Механика | Alpha evidence | Unity/Core сейчас | Пробел | Пилот | M0 |
|---|---|---|---|---|---|
| Мастер-сид → три RNG-потока (`W-01/W-02`) | DIRECT | MISSING | нет генератора мира | — | 02A |
| Регион: seed/тип/вариант/ориентация (`W-05`) | DIRECT (формула) / PARTIAL (семантика) | MISSING | нет кода | W (проверен) | 02A |
| Сетка 1024 регионов × 64 блока, мир 65536² (`W-10`) | DIRECT | PARTIAL (`ChunkSize=32`, пилот) | размер чанка CubeGame не утверждён | W (координаты) | 02A/B |
| Шум и порядок рельефа (`W-03/W-04`) | PARTIAL | PILOT `PilotHeightField` = `PLACEHOLDER_NOT_ALPHA` | нет Alpha-порта | — | 02A |
| Климат-клетка и таблица климат→объекты (`W-06/W-18`) | PARTIAL/UNKNOWN | MISSING | таблица ~660 строк не выгружена | — | 02A/C, 06B |
| Вода/пещеры/реки (`W-07…W-09`) | PARTIAL/UNKNOWN | MISSING | уровень воды, границы, ширина рек | — | 02C |
| Стриминг/unload (`W-11/W-12`) | PARTIAL | MISSING | конфликт порогов 3/5 vs 8/10 | W (дельты) | 02B/D |
| Изменения вокселей/дельты (`W-13`) | DIRECT (флаг) / PARTIAL (формат) | MISSING | формат Alpha не нужен; контракт свой | W (round-trip) | 02D |
| Карта `reg/tile/land` (`W-14`) | UNKNOWN (формат) | MISSING | L-U1 | — | 02D/08A |
| Дома/структуры (`W-16/W-17`) | DIRECT (габарит/алгоритм) | MISSING | правила расстановки | — | 07A |

## 2. Передвижение и мобильность

| Механика | Alpha evidence | Unity/Core сейчас | Пробел | Пилот | M0 |
|---|---|---|---|---|---|
| Гравитация 30 / планирование 3 (`P-22`) | DIRECT | `PlayerMotor` gravity −24 (`PLACEHOLDER`) | заменить константу | M | 03B |
| Шкала скоростей 6/8/12/20, спринт ×2 (`P-21`) | DIRECT / UNKNOWN (масштаб) | 5/8 (`PLACEHOLDER`) | OQ-23 | M (без м/с) | 03B |
| Стамина −0.0002/+0.0001, рывок 0.25/600 мс (`P-18/P-19`) | DIRECT | MISSING | нет модели | M | 03B |
| Прыжок (`P-20`) | UNKNOWN | `JumpSpeed=7` (`PLACEHOLDER`) | функции/константы нет | M (явно отсутствует) | 03B |
| Плавание/лазание/планер (`P-24…P-26`) | DIRECT (формулы) / PARTIAL (физика) | MISSING | вход/выход, срыв, применение в физике | M (кривые, не физика) | 03B |
| Лодка/езда (`P-27`) | UNKNOWN | MISSING | управления нет | — | 03B/06E |
| Ввод/камера (`P-16/P-17`) | DIRECT (адреса/параметры) | PILOT Orbit/WASD | Input System не подключён; клавиши U6 | — | 03B |
| Коллизия (`P-23`) | DIRECT (структура) / PARTIAL | CharacterController | swept-воксель не переносится | — | 03B |
| Анимации состояний (`C-30/C-31`) | PARTIAL | S05 runtime вне `Assets` | переходы `UNITY_TRANSITION` | — | 03C |

## 3. Бой, оружие, способности, снаряды

| Механика | Alpha evidence | Unity/Core сейчас | Пробел | Пилот | M0 |
|---|---|---|---|---|---|
| Урон/HP/levelFactor/разброс (`C-03…C-08`) | DIRECT | CORE (`CharacterStats`, `CombatFormulas`, `DamageResolver`) | нет игрового цикла | C (интеграция) | 05A |
| Броня/митигация (`C-09/C-10`) | DIRECT/PARTIAL | CORE (`DamageMitigation`) | OQ-02/OQ-24 | C | 05A |
| Шанс попадания (`C-12`) | DIRECT (механизм) / UNKNOWN (база) | CORE `HitResolver`, chance — вход | OQ-01 | C (границы) | 05B |
| Блок (`C-13`) | PARTIAL | CORE threshold/roll | смысл rarityScaled | C | 05B |
| 16 способностей (`P-15`) | DIRECT | MISSING | нет дерева/классов | C (таблицы) | 05B |
| Кулдаун/мана/стамина/канал (`AI-11…AI-14`) | DIRECT | MISSING | единицы стамины PARTIAL | C | 05B |
| Оружие → действие | UNKNOWN | MISSING | не выдумывать | C (отказ) | 05B |
| Комбо/стан/poise (`C-21/C-22`) | PARTIAL | MISSING | OQ-24, флаги | — | 05B |
| Эффекты/снаряды (`C-34`) | UNKNOWN | MISSING | R-3 | — | 05C |
| Оружейные действия NPC (`C-37`) | DEFERRED_TO_M0 | MISSING | R-1/R-2 | — | 06B |
| Динамические части/RuneGiant (`C-35/C-36`) | BLOCKED | MISSING | R-8/R-9 | — | 06E |
| Смерть/награды (`C-15/C-16`, `C-25`) | PARTIAL/UNKNOWN | HealthPool только HP | правило смерти | C (XP) | 05D |

## 4. NPC, AI, существа, фракции, спавн

| Механика | Alpha evidence | Unity/Core сейчас | Пробел | Пилот | M0 |
|---|---|---|---|---|---|
| Роли/фракции (`N-02`) | DIRECT | MISSING | — | N | 06B |
| Дерево поведений/тик 31 мс/стан (`N-03/N-04`) | DIRECT | MISSING | — | N (часы/стан) | 06C |
| Угроза/агро/рескан (`N-06/N-07`) | DIRECT | MISSING | живой проверки нет | N | 06C |
| Додж (`N-08`) | DIRECT | MISSING | — | N | 06C |
| Зелье (`C-24`) | DIRECT (r3) | MISSING | старая запись 25 % неверна | N | 06C |
| Реген фракции 5 (`N-12`) | DIRECT | MISSING | — | N | 06C |
| Спавн 30 строк/звёзды (`N-18`) | DIRECT (вероятности) | MISSING | связь с биомом `N-19` | N (схема) | 06B |
| Лимит популяции/вне экрана (`N-20/N-21`) | UNKNOWN | MISSING | Alpha не симулирует; требование CubeGame | N (разделение) | 06D |
| 26 пассивных объектов (`N-23`) | DIRECT (22)/правило (4) | S05 contract вне `Assets` | STATIC_OBJECT в игре | — | 06A |
| 156 видов/клипы (`N-25`) | DIRECT (эмуляция) | S05 библиотека вне `Assets` | триггер `DEFERRED_TO_M0` | — | 06C |
| Боссы/питомцы/езда (`N-26…N-28`, `N-09…N-11`) | DIRECT/UNKNOWN | MISSING | приручение/езда | — | 06E |

## 5. Предметы, инвентарь, экипировка, крафт

| Механика | Alpha evidence | Unity/Core сейчас | Пробел | Пилот | M0 |
|---|---|---|---|---|---|
| 24 типа предметов (`I-04`) | DIRECT (r3) | 8 типов | `ITEMINSTANCE-TYPE-LIMIT` | I (таблица) | 04A |
| Поля item и seed (`I-01`) | DIRECT | CORE `ItemInstance` | 24 типа не поддержаны | I | 04A |
| Статы/редкость (`I-02/I-03`) | DIRECT | CORE `ItemStatsCalculator` | — | I (S04 reuse) | 04A |
| Подтипы оружия (`A.2`) | DIRECT | enum сдвинут на 1 | `CORE-ENUM-MISMATCH` | I (10 расхождений) | 04A |
| 13 слотов/7 отрисовок (`I-08/I-09`, `AI-05/07`) | DIRECT | 6 слотов S04 | сейв не хранит экипировку | I | 04B |
| Инвентарь 4×50, стек ≤49 (`I-06/I-07`) | DIRECT/PARTIAL | `StackCount` без правил | ёмкость/стаки | I | 04B |
| Расходники/кулдаун (`C-33`, `AI-01…AI-03`) | DIRECT/PARTIAL | MISSING | B-U1 (списание/лечение) | I | 04B |
| Модель в руке/якоря (`I-10`, `AI-09`) | DIRECT | S05 runtime вне `Assets` | игровая проводка | I (контракт) | 04B |
| Лут/сундуки (`I-12/I-13`) | PARTIAL/DIRECT | MISSING | OQ-11 | — | 04C |
| Крафт/модификаторы (`I-14/I-15`) | PARTIAL/UNKNOWN | MISSING | OQ-09/OQ-10 | — | 04C |
| Экипировка в сейве (`I-11`) | DIRECT (13 слотов) | MISSING | `SAVE-EQUIPMENT-GAP` | EQS (обнаружен) | 04D |

## 6. Торговля, экономика, задания, награды

| Механика | Alpha evidence | Unity/Core сейчас | Пробел | Пилот | M0 |
|---|---|---|---|---|---|
| Деньги только от продажи (`I-16`) | DIRECT | MISSING | — | EQS (wallet/sell) | 07C |
| Цена `item_valueByType` (`I-17`) | DIRECT (частично) | CORE `Value` | наценки/ассортимент F-U5 | EQS (buy отказ) | 07C |
| Задание на клетку: тип/состояние/прогресс (`Q-18`) | DIRECT (модель) | MISSING | условия зачёта PARTIAL | EQS | 07D |
| Типы 1…13 и завершение (`J4`) | DIRECT | MISSING | награда J-U6 | EQS | 07D |
| Гейт ±2 (`Q-26`) | DIRECT | MISSING | продуктовое решение | EQS | 07D |
| XP за убийство/босс/флаги (`C-16`, `J2/J7`) | PARTIAL/STRONGLY_SUPPORTED | `AlphaMath.KillExperience` только тест | точка вызова | C/EQS | 05D |
| `mission` 38 Б (`Q-19`) | PARTIAL | MISSING | J-U4/U5 | — | 07D |
| Invasion/gang (`Q-20`) | DIRECT (типы)/UNKNOWN (события) | MISSING | нет контента | — | 07D |
| Торговцы/ресток (`Q-17/Q-23`) | PARTIAL/UNKNOWN | MISSING | F-U5 | — | 07C |
| Крафт-цены (`I-14`) | PARTIAL | MISSING | таблицы рецептов | — | 07C |

## 7. Прогрессия, смерть, сохранения, персистентность

| Механика | Alpha evidence | Unity/Core сейчас | Пробел | Пилот | M0 |
|---|---|---|---|---|---|
| XP-кривая/уровни (`P-09`) | DIRECT | CORE `AlphaMath`/`ProgressionState` | кап уровня D-U1 | C/EQS | 03D |
| HP/статы/звёзды (`P-10/P-11`) | DIRECT | CORE `CharacterStats` | звёзды мобов отсутствуют | — | 03D/05A |
| 11 рангов навыков (`P-13`) | DIRECT | MISSING | покупка OQ-04 | — | 03D |
| Смерть/возрождение (`C-25`) | UNKNOWN | MISSING | правило штрафа/точки | — | 05D |
| Блоб персонажа v11 (`Q-11`) | DIRECT (23 поля) | Свой `SaveGame` v1 | экипировка/навыки отсутствуют | EQS | 01D/04D |
| Мир/карта (`Q-13`) | DIRECT (ключи)/UNKNOWN (форматы) | MISSING | свой формат + миграции | W/EQS | 01D/02D |
| Сущности по близости (`C.2`) | DIRECT | MISSING | блоб/восстановление | N | 06D |
| Дельты мира (`W-13`) | PARTIAL | MISSING | контракт свой | W | 02D |
| Атомарность/CRC/миграции | N/A в Alpha | `SaveService` frame/CRC, миграций нет | M0-01D | EQS | 01D |
| Позиция в сейве (`W-20`) | PARTIAL (OQ-22) | MISSING | x/z не доказаны | — | 01D |

---

## Что эта матрица **не** означает

- `PASS` пилота не означает перенос механики в игру: стенд изолирован от сцены.
- `DIRECT`-формула не означает готовность: у многих нет входных данных (биом, лут, цены).
- `MISSING` — не «не нужно», а «в текущем CubeGame нет».
- Матрица не является `ACCEPTED` и не открывает M0-01.
