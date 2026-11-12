# M1 — матрица семейств существ Alpha (voxel-каталог, 156 видов)

**Статус:** `RESEARCH ONLY` / `READY_FOR_REVIEW` — M1 не запущен, производство не начато.  
**Контур:** M1-R0 (исследование идентичности и семейств), ТЗ — [M1_CREATURE_FAMILY_RESEARCH_TASK](../planning/M1_CREATURE_FAMILY_RESEARCH_TASK.md).  
**Реестр построчно:** [M1_ALPHA_CREATURE_IDENTITY_REGISTRY.csv](M1_ALPHA_CREATURE_IDENTITY_REGISTRY.csv) (156 строк).  
**Отчёт:** [M1_R0_CREATURE_FAMILY_RESEARCH_REPORT](../reports/M1_R0_CREATURE_FAMILY_RESEARCH_REPORT.md).  
**Этап:** [M1 MODELS / RIGS / ANIMATION](../stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md) — `APPROVED CONCEPT / NOT_AUTHORIZED`.

---

## 1. Как читать матрицу

Четыре классификации независимы и намеренно не смешиваются (требование ТЗ §2):

| Классификация | Что отвечает | Откуда берётся |
|---|---|---|
| **Anatomy** | технический скелет/форма тела | `structural_category` по блокам отрисовки и флагу locomotion (`ALPHA_CODE_STATIC`), не по имени |
| **Alpha identity** | кто это в игре | карта имён `World.cpp` (`ALPHA_DECOMPILED`), модели внешности, роль в Alpha |
| **Intelligence / behaviour** | культура, зверь, монстр, объект | предложение по доказанной базе (имя + части + действия ИИ + живое наблюдение); **не** Alpha-атрибут |
| **Motion individuality** | осанка, походка, темп, атака | предложение; опирается на доказанный набор действий и анатомию |

Метки происхождения: `ALPHA_CODE_STATIC`, `EMULATED_ALPHA_CODE`, `ALPHA_DECOMPILED`,
`UNITY_ASSET_VERIFIED`, `LOCAL_ONLY`, `INFERRED_REVIEW_REQUIRED`, `UNKNOWN`.

Важно: **интеллект не является доказательством отдельного скелета.** Если топология совпадает,
это identity/behaviour-подсемейство, а не новый rig.

---

## 2. Сводка каталога

| Показатель | Значение | Источник |
|---|---|---|
| Видов существ/объектов (`creature+0x64`) | **156** | `ALPHA_CODE_STATIC` (таблица видов) |
| Из них существ | 130 | роль в Alpha (`spawn_role_evidence`) |
| Из них пассивных объектов | 26 | `ALPHA_CODE_SPAWN` (фракция 6) + `PRODUCER_RULE_D67` |
| Видов с именем в карте `World.cpp` | **154** | `ALPHA_DECOMPILED` |
| Видов без имени | 2 (`0x5f`, `0x81`) | `UNKNOWN` |
| Различных `.cub` в блоках существ | **450** | `ALPHA_CODE_STATIC` (реестр моделей) |
| Моделей, используемых несколькими видами | 18 | `ALPHA_CODE_STATIC` |
| Семейств движения (`family_id`) | **106** | эмуляция `drawModel` |
| Готовых вариантов в Unity-реестре | **156** | `UNITY_ASSET_VERIFIED` (`AlphaStagedVisuals.g.cs`) |
| Клипов в подготовленных вариантах | **927** | `LOCAL_ONLY` (локальный кэш) |
| `walk/idle` оракул `ORACLE_VERIFIED` | 156 / 156 | `EMULATED_ALPHA_CODE` |

Количество моделей **не** равно количеству существ: 450 ресурсов обслуживают 156 видов,
а анимационная система параметрическая (106 семейств, не 156 библиотек).

---

## 3. Анатомические семейства

Счёт по фактическим блокам отрисовки (не по именам, не по интеллекту).

| `anatomy_family` | Кол-во | Виды (примеры) | Набор блоков | Rig-кандидат (гипотеза) |
|---|---|---|---|---|
| `BIPED` | **53** | HumanMale `0x02`, ElfMale `0x00`, OrcMale `0x0b`, DwarfMale `0x09`, GoblinMale `0x04`, Skeleton `0x11`, Zombie `0x60` | body, head, (hair), hand×2, foot×2 | `CG_Humanoid_v1` |
| `BIPED_SPLIT_ARMS` | **9** | Minotaur `0x52`, Troll `0x6c`, DarkTroll `0x6d`, Golem `0x6f`, EmberGolem `0x70`, SnowGolem `0x71`, Yeti `0x72`, Cyclops `0x73`, HellDemon `0x6e` | то же + `slot98_a/b` (локти) | `CG_Humanoid_v1` + опциональные elbow-кости |
| `QUADRUPED` | **23** | Wolf `0x1c`, Panther `0x1d`, Cat `0x1e`, Horse `0x62`, Cow `0x64`, Dragon `0x65`, Mammoth `0x74` | body, head, (tail), hand×2, foot×2, флаг `bit0=1` | `CG_Quadruped_v1` |
| `WINGED_LEGS` | **9** | Crow `0x37`, Chicken `0x38`, Seagull `0x39`, Parrot `0x3a`, Bat `0x3b`, Fly `0x3c`, Midge `0x3d`, Mosquito `0x3e`, Hornet `0x35` | body, head, `slot9a`×2 (крылья), foot×2 | `CG_Avian_v1` |
| `WINGED_HEAD_BODY` | **2** | Owl `0x5c`, Bumblebee `0x97` | head, `slot9a`×2, foot×2 (блока `body` нет) | `CG_AvianHeadBody_v1` |
| `WINGED_BIPED` | **2** | Vampire `0x61`, InsectGuard `0x36` | body, head, hand×2, foot×2, крылья | `CG_Humanoid_v1` + крепление крыльев |
| `HEAD_BODY_LEGS` | **4** | BarkBeetle `0x66`, FireBeetle `0x67`, SnoutBeetle `0x68`, LemonBeetle `0x69` | head (как тело), foot×2 | `CG_AvianHeadBody_v1` |
| `LEGS_ONLY` | **11** | Bunny `0x23`, Porcupine `0x24`, PlainRunner `0x3f`, Peacock `0x43`, Spitter `0x56`, Onionling `0x47`, Devourer `0x49` | body, (head), foot×2 | `CG_Biped_LegsOnly_v1` (кандидат) |
| `FORE_LIMBS_ONLY` | **4** | SandHorror `0x2a`, Lich `0x75`, Mermaid `0x94`, Merman `0x95` | body, head, (hair), hand×2 (+ elbow у Lich) | `CG_TorsoArms_v1` |
| `LIMBLESS_BODY` | **37** | Slime×4, все рыбы, кусты/залежи/ваза/бомба, Egg | один блок (body) | `CG_Limbless_v1`, для статики — без рига |
| `LIMBLESS_HEAD` | **2** | Frightener `0x29`, LanternFish `0x98` | head (+ «eyes» в слоте hair) | `CG_HeadOnly_Floating_v1` |

\* У `Lich 0x75` блоки локтей тоже есть, но он попадает в `FORE_LIMBS_ONLY` (нет ног).

Итого: 53+9+23+9+2+2+4+11+4+37+2 = **156**.

### 3.1 Гуманоидное семейство: 68 видов — один контракт

`BIPED 53` + `BIPED_SPLIT_ARMS 9` + `WINGED_BIPED 2` + `FORE_LIMBS_ONLY 4` имеют одинаковую
топологию «торс + голова + две руки (+ две ноги)». Различия внутри:

- **пропорции** (размер вида 0.8–5.6 блока; части 4–24 вокселя по грани);
- **наличие/отсутствие ног** (merfolk и Lich без ног);
- **наличие крыльев** (Vampire, InsectGuard);
- **отдельные блоки локтей** (9 видов крупных гуманоидов).

Это подтверждает главный принцип M1: один humanoid-контракт + body profile + IK, а не
отдельный скелет на каждый вид.

### 3.2 Четвероногие: 23 вида

Один блоковый набор `body/head/(tail)/hand×2/foot×2` + флаг `flags&1 = 1`
(походка «четвероногое», подтверждено формулой ходьбы: человек `0`, кот `1`).
Различия — модель, размер (`Wolf 1.52×1.52×3.23`, `Mammoth 7×7×7.84`) и хвост.

Особый случай: **Dragon `0x65`** — четвероногое **без нарисованных крыльев**:
`dragon-wing.cub` загружается, но не рисуется ни одним вызовом; голова имеет отдельный
якорь `(sx/2, sy·0.2, sz/2)`.

### 3.3 Птицы и летающие: 13 видов в трёх разных анатомиях

| Подсемейство | Виды | Признак |
|---|---|---|
| `WINGED_LEGS` (9) | Crow, Chicken, Seagull, Parrot, Bat, Fly, Midge, Mosquito, Hornet | тело + голова + 2 крыла + 2 ноги |
| `WINGED_HEAD_BODY` (2) | Owl, Bumblebee | **нет блока body**: голова = тело |
| `WINGED_BIPED` (2) | Vampire, InsectGuard | 2 руки + 2 ноги + крылья |

Модели крыльев у птиц называются `*-hand.cub` (crow-hand, owl-hand, bat-hand) — это
историческая схема Alpha, а не признак того, что крыло является рукой.

`HEAD_BODY_LEGS` (4 жука) — отдельная безкрылая анатомия «голова-тело + 2 ноги»;
та же форма тела, что у Owl, но без крыльев.

### 3.4 «Длинноногие» и бегуны: НЕ доказано как птицы

`PlainRunner 0x3f`, `LeafRunner 0x40`, `SnowRunner 0x41`, `DesertRunner 0x42` — анатомия
`LEGS_ONLY` (body + 2 ноги, без рук и крыльев). Гипотеза «длинноногая птица» **не доказана**:
имя вида не является анатомическим доказательством, крыльев нет, в карте имён нет слова «bird».
`Peacock 0x43` — единственный именованный «длинноногий» вид с блоками body+head+2 ноги.

### 3.5 Бестелесные/парящие формы

| Вид | Блоки | Что доказано |
|---|---|---|
| Frightener `0x29` | head + eyes (в слоте hair) | нет ног и торса; размер 1.4×1.4×2.5 |
| LanternFish `0x98` | head + eyes | то же, водная среда (`flags` бит 0x100) |
| SandHorror `0x2a` | head + 2 руки | торс отсутствует, рук две |
| Djinn `0x51`, Imp `0x55`, Spectrino `0x50` | полный двуногое | **не** floating-анатомия: у них есть ноги |

То есть `CG_Floating_v1` оправдан для **2–3** видов, а не для всех «духов» по имени.

### 3.6 Чего в каталоге НЕТ

| Гипотеза из ТЗ | Факт по 156 видам |
|---|---|
| `INSECT6` (шестиногие) | **отсутствует**: максимум 4 конечности + 2 крыла; «насекомые» — 2 ноги + крылья |
| `ARACHNID8` | **отсутствует**: восьминогих нет ни одного |
| `SERPENT / SEGMENTED` | **отсутствует** как сегментированное тело; merfolk/Lich — «торс + руки» без ног, но не цепь сегментов |
| `AMPHIBIOUS` (жабья анатомия) | **отсутствует** как отдельный план: `Frog 0x44`, `Frogman 0x0d/0x0e` — обычные двуногие |
| `AVIAN_LONG_LEGS_OSTRICHLIKE` | **не доказано**: есть `LEGS_ONLY`-бегуны и `Peacock`, но птичья природа не подтверждена |
| `AVIAN_HEADBODY` | **есть**: Owl `0x5c`, Bumblebee `0x97` (2 вида) |

Это важный результат: M1 не должен создавать архетипы `Insect6`, `Arachnid8`, `Serpent`
«под существующий каталог» — их нет. Если они понадобятся для нового контента, это отдельное
решение, а не перенос Alpha.

---

## 4. Identity-подсемейства внутри общей анатомии: 38 семейств

Внутри гуманоидного контракта различие — это identity и поведение, а не скелет.
Количества — по реестру (поле `identity_family`).

| Identity-семейство | Кол-во | Виды |
|---|---|---|
| HUMAN | 9 | HumanMale/Female, OldMan, Bandit, Wizard, Witch, NomadMale/Female, Jester |
| ELF / DWARF / ORC | 2 / 2 / 2 | пары M/F |
| GOBLIN_KIN | 5 | GoblinMale/Female, Gnoll, PolarGnoll, Gnobold |
| LIZARDMAN / FROGMAN | 2 / 2 | пары M/F |
| UNDEAD_KIN | 6 | UndeadMale/Female, Zombie, Skeleton, Vampire, Lich |
| OGREKIN | 7 | Ogre, Troll, DarkTroll, Yeti, Cyclops, Minotaur, RuneGiant |
| CONSTRUCT | 4 | Golem, EmberGolem, SnowGolem, Rockling |
| DEMON / SPECTRAL | 3 / 3 | HellDemon, Imp, Djinn / Frightener, SandHorror, Spectrino |
| DESERT_DEITY | 2 | Anubis, Horus |
| MERFOLK | 2 | Mermaid, Merman |
| CANINE / FELINE | 7 / 4 | собаки, волк, оборотень, бультерьер / кошки, пантера |
| HOOFED_BEAST | 9 | альпаки, свинья, овца, корова, лошадь, верблюд, мамонт, SkullBull |
| SMALL_BEAST | 9 | Bunny, Porcupine, Squirrel, Raccoon, Koala, Monkey, Mole, Biter, Duckbill |
| REPTILE / CRUSTACEAN | 3 / 2 | Crocodile, Turtle, Saurian / Crab, SeaCrab |
| MONSTER / AMPHIBIAN / DRAGON | 2 / 1 / 1 | SpikeCreature, Insectoid / Frog / Dragon |
| BIRD / BAT / RUNNER_BIRD | 7 / 1 / 4 | см. §3.3–3.4 |
| INSECT | 10 | Hornet, InsectGuard, Bumblebee, Fly, Midge, Mosquito, 4 жука |
| SLIME / PLANT_MONSTER / FISH | 4 / 5 / 9 | слизи, растения-монстры, рыбы |
| VEGETATION / TREE / ORE_DEPOSIT | 9 / 2 / 9 | пассивные объекты |
| TRAINING_TARGET / BREAKABLE / EXPLOSIVE / EGG | 3 / 1 / 1 / 1 | пассивные объекты |
| UNKNOWN_NAME_HUMAN_LIKE | 1 | `0x5f` (модели `santa-*`, имени в карте нет) |

Всего 38 групп. Два вида без имени в Alpha (см. отчёт §7) — единственные, где identity
не установлена документально.

---

## 5. Motion individuality: общий base + различия поверх него

Правило M1 (утверждено концепцией): базовая locomotion принадлежит архетипу, индивидуальность
вида — это осанка, темп, повороты, реакции и signature-действия, а не отдельная полная библиотека.

| Группа | Что должно отличаться (предложение) | База (общая) |
|---|---|---|
| HUMAN | нейтральная осанка, размеренный шаг | Humanoid locomotion |
| ELF | лёгкий каркас, длинный шаг, высокая посадка головы | Humanoid |
| DWARF | короткий тяжёлый шаг, широкая стойка, низкий центр масс | Humanoid |
| ORC | наклон вперёд, широкий мах рук, «вбитые» повороты | Humanoid |
| GOBLIN_KIN | быстрый семенящий ритм, сгорбленность | Humanoid (Light) |
| UNDEAD_KIN | асимметричная волочащая походка, медленное восстановление | Humanoid |
| OGREKIN | медленный масс-ритм с ударом стопы | Humanoid (Heavy) + IK |
| CONSTRUCT | «блочные» механические шаги, без раскачки торса | Humanoid (Heavy) |
| CANINE / FELINE | рысь / тихий низкий крадущийся шаг | Quadruped |
| HOOFED_BEAST | четырёхтактный шаг, тяжёлый толчок задних | Quadruped |
| BIRD | покачивание головы, раскрытие крыльев на повороте | Avian (ground) |
| WINGED_HEAD_BODY | покачивание «головы-тела», короткие ноги | AvianHeadBody |
| RUNNER_BIRD | длинный шаг, стабилизированная шея | LegsOnly |
| INSECT | высокочастотный цикл ног, рывковые повороты | Avian/LegsOnly |
| SLIME | squash-and-stretch, без конечностей | Limbless |
| FISH / MERFOLK | нет наземной походки: плавание/парение | Limbless / TorsoArms |
| SPECTRAL | парение, «шлейф» конечностей, без foot contact | HeadOnly / TorsoArms |

Запрещено (следствие): создавать `Orc_Walk`, `Elf_Walk`, `Dwarf_Walk` как отдельные
полные библиотеки, если общий humanoid-клип с IK/профилем коррекции работает.

Signature-действия берутся из **доказанных** кодов действий Alpha (поле
`combat_behavior_evidence` реестра), например: `Mammoth 0x74` — 69/73/74; `Golem 0x6f` — 76/108;
`Saurian 0x77` — 70/75; `Turtle 0x19` — 72; крылатые — 51; гуманоиды — 3/4/6/54.
Где запуск не доказан (`RuneGiant 0x76`), signature не предлагается.

---

## 6. Переиспользование моделей (many-to-many)

450 различных `.cub` на 156 видов; 18 моделей используются несколькими видами:

| Модель | Видов | Примеры |
|---|---|---|
| `foot.cub` | 22 | почти все гуманоиды и часть монстров |
| `hand2.cub` | 11 | люди, эльфы, дварфы, бандит, волшебник |
| `body2.cub` | 10 | гоблины, орки, скелеты, undead, OldMan,Bandit, Bullterrier |
| `body4.cub` | 5 | люди, эльфы, frogman |
| `zombie-body.cub` | 2 | Zombie `0x60` и **Gnobold `0x33`** |
| `bat-hand.cub` | 2 | Bat `0x3b`, **Vampire `0x61`** (крылья) |
| `mermaid-hand.cub` | 2 | Mermaid `0x94`, Merman `0x95` |

Вывод: количество геометрических ресурсов нельзя считать количеством видов; один
подготовленный вариант (variant) — это вид, а не модель.

---

## 7. Спорные и подозрительные места (для ревью)

1. **`Gnobold 0x33`** собирается из `zombie-body.cub` + `gnobold-*` — визуально это зомби-тело.
   Не ошибка реестра (модель существует, совпадает с Alpha), но identity и силуэт стоит
   подтвердить наблюдением в игре.
2. **`Vampire 0x61`**: блок `body` = `wool-chest5.cub` (модель одежды). Так задано в Alpha;
   при M1-переработке тело вампира нельзя наследовать от «шерстяного нагрудника».
3. **`Onionling 0x47` / `DesertOnionling 0x48` / `Devourer 0x49`**: модель головы лежит в
   слоте `body`, отдельного блока головы нет → формально `LEGS_ONLY`, фактически
   «голова-тело + ноги» (как жуки). Учитывать при выборе архетипа.
4. **`OrcMale 0x0b`**: индекс 1300 загружается дважды (`orc-head-m01.cub` → `orc-head.cub`);
   побеждает последняя загрузка. Оба ресурса есть в `data1.db`, то есть старый вывод D64
   «MODEL_NOT_IN_DB head» больше не воспроизводится — старую строку disposition стоит закрыть.
5. **`DarkTroll 0x6d`**: в слоте `hair` лежит `dark-troll-eyes.cub` с прозрачными маркерами
   (load flag 0) — это глаза, а не волосы. Так же у `Skeleton 0x11`, `Imp 0x55`, `Frightener 0x29`.
6. **Рыбы**: у 8 из 9 рыб `flags&1 = 1` (бит «четвероногое»), но анатомия — без конечностей.
   Флаг описывает ветку формулы отрисовки, а не «четыре лапы»; нельзя выводить анатомию из флага.
7. **`0x5f`** (модели `santa-body/head`, имя отсутствует) и **`0x81`** (`fir-tree.cub`, имя
   отсутствует) — единственные виды без записи в карте имён `World.cpp`. Названия не выдуманы.
8. **`ElfMale 0x00`** подготовлен как `elf_male_0x00_no_alpha_run` (без живого прогона) и имеет
   только 5 клипов — самый слабый по покрытию анимации среди гуманоидов.

---

## 8. Что это даёт M1 (без запуска M1)

Кандидаты архетипов, выведенные из фактической анатомии, а не из списка гипотез:

| Архетип-кандидат | Видов | Приоритет для M1-пилота |
|---|---|---|
| `CG_Humanoid_v1` (+ опциональные локти, крылья) | 68 | **первый** (E1–E2: Human Standard + Orc Heavy) |
| `CG_Quadruped_v1` | 23 | второй (E3) |
| `CG_Avian_v1` (2 ноги + крылья) | 9 | третий (E3) |
| `CG_AvianHeadBody_v1` (голова-тело; 2 из них с крыльями) | 6 | вместе с avian |
| `CG_Biped_LegsOnly_v1` | 11 | по потребности |
| `CG_TorsoArms_v1` (без ног; входит в humanoid-контракт) | 4 | по потребности |
| `CG_Limbless_v1` | 37 | существ — 11 (4 слизи + 7 рыб), остальные 26 — статические объекты |
| `CG_HeadOnly_Floating_v1` | 2 | по потребности |
| `CG_StaticObject_v1` | 26 | вне character factory (объекты мира) |
| `Insect6` / `Arachnid8` / `Serpent` | 0 | **не создавать** под текущий каталог |

Сумма без двойного счёта: 68 + 23 + 9 + 6 + 11 + 37 + 2 = **156**.

Минимальный представительский набор и обоснование выбора — в
[отчёте M1-R0](../reports/M1_R0_CREATURE_FAMILY_RESEARCH_REPORT.md#8-минимальный-представительский-набор).
