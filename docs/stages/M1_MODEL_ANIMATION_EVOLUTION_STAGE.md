# M1 — эволюция моделей, ригов и анимаций CubeGame

**Статус:** `APPROVED CONCEPT / FUTURE STAGE / NOT_AUTHORIZED`  
**Направление roadmap:** M1 — модели, анимации и визуал.  
**Концептуальные решения утверждены продюсером:** 2026-11-10.  
**Начало исполнения:** только после закрытия M0 и отдельного решения продюсера.  
**Назначение документа:** зафиксировать утверждённую архитектурную концепцию создания моделей, модульных персонажей/существ, ригов, анимаций, оружия и производственного конвейера CubeGame. Детальная техническая реализация, конкретные классы, форматы данных и инструменты будут дополнительно спроектированы при фактическом открытии M1.

---

# 1. Главная цель этапа

M1 не должен сводиться к ручному созданию большого каталога красивых моделей и последующей попытке отдельно анимировать каждую из них.

Главная цель — построить **CubeGame Character / Creature / Asset & Animation Factory**: единую систему, которая позволяет создавать, собирать, оснащать и анимировать большое число игровых персонажей и существ через повторно используемые анатомические архетипы, модульные части тела, риги, animation packages, оружейные наборы и procedural/IK-слои.

Alpha используется как исследовательская база:

- роли существ;
- категории контента;
- исходные пропорции;
- составные части моделей;
- поведение;
- движение;
- экипировка;
- игровые функции.

Финальный контент M1 должен быть собственным контентом CubeGame:

- сохраняется voxel/cubic-ДНК;
- повышается детализация и качество силуэта;
- перерабатываются пропорции;
- создаётся собственная система лиц;
- перерабатываются материалы и цвета;
- создаются собственные конструкции тел;
- вводятся собственные риги;
- создаются новые анимации под новую боевую систему;
- персонажи, NPC, противники и существа собираются общей фабрикой;
- новые модели и новые виды должны подключаться к существующей системе без отдельной технологии для каждого объекта.

Главная инженерная цель:

> **не создать одну модель орка, волка или призрака, а создать систему, способную собрать множество разных орков, волков, призраков и других существ и автоматически назначить им совместимые риги, анимации, оружие и procedural-слои.**

---

# 2. Утверждённые фундаментальные решения

Следующие решения считаются утверждёнными концептуально.

## 2.1. Не выбирать между двумя плохими крайностями

Запрещён подход:

```text
сначала сделать все модели
→ потом начинать думать, как их ригать и анимировать
```

Так же не является правильным подход:

```text
сделать одного персонажа
→ сначала вручную создать ему весь возможный каталог анимаций
→ только потом переходить к другим моделям
```

Правильная единица разработки — **вертикальный Archetype Package**:

```text
REFERENCE MODEL
↓
ANATOMY / RIG ARCHETYPE
↓
MODULAR PART CONTRACT
↓
MINIMUM COMPLETE ANIMATION PACKAGE
↓
WEAPON / ACTION INTERACTION
↓
IK / PROCEDURAL CORRECTIONS
↓
SECOND MODEL OF SAME ARCHETYPE
↓
REUSE TEST
↓
2 → 5 → 10
↓
CONTROLLED BATCH
```

## 2.2. Модель не владеет собственной полной библиотекой анимаций

Главный принцип:

> **анимация принадлежит skeleton/anatomy archetype, классу оружия, типу действия или уникальному поведению — а не конкретной модели.**

Следовательно:

- `Orc_Walk` не создаётся только потому, что персонаж — орк;
- `Human_Walk` не создаётся только потому, что персонаж — человек;
- `Wolf_Run` не должен появляться, если общий quadruped run подходит этому волку;
- уникальная анимация создаётся только когда действие действительно уникально.

## 2.3. Тело строится как LEGO, но риг не дробится на независимые риги каждой детали

Утверждён модульный принцип сборки:

- голова;
- торс;
- руки;
- ноги;
- волосы;
- борода;
- рога;
- клыки;
- хвост;
- крылья;
- дополнительные части;
- экипировка.

Но каждая часть не должна иметь полностью независимую несовместимую риг-систему.

Правильная схема:

```text
RIG ARCHETYPE / BONE CONTRACT
        ↓
STANDARD ATTACHMENT / BINDING POINTS
        ↓
MODULAR BODY PARTS
        ↓
ASSEMBLED ENTITY
```

Модульная деталь знает:

- к какому archetype она относится;
- к каким костям привязана;
- какие body profiles поддерживает;
- какие другие части совместимы;
- какие материалы использует;
- какие sockets и equipment zones затрагивает.

## 2.4. Character Creator, NPC Generator и Enemy Generator не должны быть тремя разными системами

Они являются разными клиентами одной фабрики.

```text
PLAYER CHARACTER CREATOR ─┐
NPC GENERATOR             ├→ ENTITY RECIPE → CHARACTER / CREATURE FACTORY
ENEMY GENERATOR           ┘
```

Игрок выбирает параметры вручную.

NPC и противники получают те же параметры из:

- seed;
- правил вида;
- биома;
- фракции;
- роли;
- уровня;
- экипировки;
- генератора мира.

## 2.5. Производственная единица — Archetype Package

Вместо мышления:

> «сделать модель»

M1 должен мыслить:

> **«доказать Archetype Package»**

Archetype Package включает:

- reference model;
- anatomy contract;
- skeleton/rig;
- modular body parts;
- proportion rules;
- sockets;
- locomotion package;
- reactions;
- combat/action packages;
- weapon interactions;
- IK;
- procedural corrections;
- validation;
- editor preview;
- правила повторного использования.

---

# 3. Что уже имеется к началу M1

К фактическому открытию M1 проект должен использовать результаты M0 и исследовательского контура:

1. Каталог исходной геометрии и категорий контента.
2. Каталог существ и составных частей.
3. Исследованные планы тела и семейства движения.
4. Восстановленные сведения о поведении и анимациях Alpha.
5. Импорт и отображение моделей в Unity.
6. Runtime, где модель проверяется в настоящем gameplay.
7. Проверки масштаба, pivot, ориентации, крепления оружия и анимаций.
8. Каталог экипировки, оружия, предметов, растений и статических объектов.
9. Исследования по повторному использованию движения между родственными моделями.

Важно: каталог Alpha содержит тысячи геометрических ресурсов, но это не означает тысячи независимых существ и не требует тысячи отдельных animation pipelines.

---

# 4. Что должно существовать на выходе M1

На выходе должны существовать:

- утверждённый визуальный язык CubeGame;
- Style Anchors;
- библиотека anatomy/skeleton archetypes;
- библиотека модульных частей тела;
- `EntityRecipe / CharacterRecipe`-подобное описание сущности;
- общая Character / Creature Factory;
- система телосложений и пропорций;
- система лиц;
- Character Creator поверх той же фабрики;
- генерация NPC и противников через ту же фабрику;
- система sockets;
- система экипировки;
- Weapon Definitions;
- библиотека базовой locomotion;
- combat/action packages;
- Animation Resolver;
- procedural/IK/additive-слои;
- signature animations;
- техническая валидация;
- визуальная приёмка;
- editor preview;
- воспроизводимый batch pipeline;
- возможность создавать новые виды существ без переписывания всей системы.

---

# ЧАСТЬ I — ENTITY RECIPE И МОДУЛЬНАЯ СБОРКА

# 5. EntityRecipe / CharacterRecipe

Конкретное имя структуры будет принято при открытии M1, но концепция утверждена.

Персонаж или существо не должны существовать в производственной системе только как заранее собранный prefab.

Они должны описываться данными.

Пример гуманоида:

```text
Race = Orc
Sex = Male

Anatomy = Humanoid
RigArchetype = CG_Humanoid_v1
BodyProfile = Heavy

Head = OrcHead_03
Torso = OrcTorso_02
Arms = OrcArms_04
Legs = OrcLegs_01

Height = 1.06
ShoulderScale = 1.18
ArmScale = 1.08

Hair = Hair_07
Beard = Beard_02
Tusks = Tusks_04

ChestArmor = IronChest
Weapon = Greatsword

CombatStyle = Warrior
```

Рецепт должен быть пригоден для:

- игрока;
- NPC;
- противника;
- процедурной генерации;
- сохранения/загрузки;
- editor preview;
- тестовой генерации.

---

# 6. BodyPartDefinition

Каждая модульная часть тела должна иметь машинно-читаемое назначение.

Рабочая структура концептуально включает:

```text
ID
Category

Species / Race compatibility
Anatomy compatibility
RigArchetype compatibility
BodyProfile compatibility

Bone bindings
Attachment points
Material profile

Equipment compatibility
Exclusion rules
Optional tags
```

Пример:

```text
Orc_Torso_03

Category: Torso
Species: Orc
RigArchetype: CG_Humanoid_v1

BoneBindings:
- Spine
- Chest
- Shoulder_L
- Shoulder_R

CompatibleBodyProfiles:
- Standard
- Heavy
```

Это превращает детали в настоящие LEGO-компоненты, а не просто в произвольные meshes.

---

# 7. Humanoid Factory

Для гуманоидов утверждена модульная фабрика.

Один humanoid может состоять из:

```text
Humanoid
│
├── Species / Race
├── Sex / variation rules
├── BodyProfile
│
├── Head
├── Face components
├── Torso
├── Arms
├── Legs
│
├── Hair
├── Beard
├── Horns / Tusks / Ears / other species parts
│
└── Equipment
    ├── Head
    ├── Chest
    ├── Hands
    ├── Legs
    ├── Feet
    └── Weapons
```

Разные виды получают собственные пропорции, геометрию и допустимые части, но по возможности используют один стабильный humanoid rig contract.

---

# 8. Телосложения и пропорции гуманоидов

Утверждён принцип:

> **одинаковая skeleton topology внутри совместимого семейства + разные bone/body proportions + общие animation packages.**

Минимальные профили для эксперимента:

- `LIGHT`;
- `STANDARD`;
- `HEAVY`.

Пример направления, а не финальных чисел:

```text
Human
Shoulders = 1.00
Chest = 1.00
ArmsLength = 1.00
LegLength = 1.00

Orc
Shoulders = wider
Chest = wider
ArmsLength = longer
LegLength = shorter

Elf
Shoulders = narrower
Chest = narrower
ArmsLength = slightly longer
LegLength = longer
```

Конкретные коэффициенты будут определяться экспериментально в M1.

Новый body profile не должен автоматически создавать новый полный набор animations.

Сначала используются:

- общий clip;
- retargeting;
- IK;
- procedural corrections;
- body correction profile.

Только принципиально несовместимое движение получает специализированный вариант.

---

# 9. Character Creator

Редактор персонажа не создаёт отдельную систему сборки.

Он является UI поверх EntityRecipe/CharacterRecipe и общей Factory.

Пример:

```text
Race → Orc
Body → Heavy
Head → 4
Hair → 8
Height → 107%
Tusks → 3
```

После изменения параметра:

```text
Character Creator UI
↓
Recipe update
↓
Humanoid Builder
↓
Character rebuilt / updated
```

Таким образом визуальный редактор игрока автоматически проверяет ту же систему, которой потом пользуется мир.

---

# 10. Генерация гуманоидных NPC и противников

NPC Generator и Enemy Generator должны формировать тот же recipe.

Пример:

```text
WORLD SEED
↓
SETTLEMENT / FACTION RULES
↓
ROLE
↓
SPECIES RULES
↓
BODY / FACE / PART VARIATION
↓
EQUIPMENT
↓
ENTITY RECIPE
↓
HUMANOID FACTORY
```

Пример результата:

```text
Orc Warrior #1849

Species = Orc
Body = Heavy
Height = 1.07
Torso = 03
Arms = 02
Head = 09
Tusks = 04
Hair = None

Weapon = Axe
Armor = Medium
```

Именно эта система должна позволить создавать в мире множество визуально отличающихся гуманоидов без хранения отдельной полной модели и анимационной библиотеки для каждого.

---

# ЧАСТЬ II — НЕГУМАНОИДНЫЕ СУЩЕСТВА

# 11. Общая Creature Factory

Humanoid Factory является частным случаем более общей Character / Creature Factory.

Утверждённый принцип:

```text
ENTITY RECIPE
↓
ANATOMY CLASS
↓
RIG ARCHETYPE
↓
BODY PARTS
↓
PROPORTION PROFILE
↓
CREATURE BUILDER
↓
ANIMATION RESOLVER
↓
ACTION RESOLVER
↓
IK / PROCEDURAL
↓
GAME ENTITY
```

Нельзя пытаться привести всех существ к одному humanoid skeleton.

Система должна поддерживать несколько anatomy/rig archetypes.

Рабочая карта:

- Humanoid;
- Heavy Humanoid / Giant;
- Quadruped;
- Avian;
- Flying;
- Floating / Spectral;
- Insect;
- Arachnid;
- Serpent / Segmented;
- Amorphous;
- Custom Boss.

Точный набор уточняется при открытии M1 по реальному каталогу существ.

---

# 12. Quadruped

Для четвероногих нужен собственный rig contract.

Пример:

```text
CG_Quadruped_v1

Root
Pelvis
Spine
Chest
Neck
Head

FrontLeg_L
FrontLeg_R

BackLeg_L
BackLeg_R

Tail...
```

Волк, кабан, кошкоподобное существо и другие совместимые существа могут иметь разные:

- головы;
- корпуса;
- лапы;
- хвосты;
- размеры;
- материалы;

но использовать общий locomotion package, если анатомия совместима.

Пример базового package:

- Idle;
- Walk;
- Run / Trot;
- Sprint;
- Turn;
- Leap;
- Hit;
- Knockback;
- Death.

Действия сверху:

- Bite;
- Claw;
- Charge;
- Howl;
- другие species/signature actions.

---

# 13. Floating / Spectral

Призраки и другие существа без ног не должны получать ложный humanoid locomotion.

Пример rig:

```text
CG_Floating_v1

Root
Body
Chest
Head
Arm_L
Arm_R
optional tail / spectral chain
```

Вместо walk/run:

- FloatIdle;
- FloatMove;
- FloatFast;
- HoverTurn;
- Rise;
- Descend.

Procedural layer может отвечать за:

- vertical bob;
- body sway;
- trailing motion;
- look-at;
- secondary motion.

Для такого archetype:

```text
FootIK = OFF
GroundFootPlacement = OFF
FloatingProcedural = ON
```

---

# 14. Avian

Для обычных птиц предполагается отдельный avian contract.

Пример:

```text
CG_Avian_v1

Root
Body
Neck
Head

Wing_L
Wing_R

Leg_L
Leg_R

Tail
```

Возможные animation packages:

### Ground

- GroundIdle;
- Walk;
- Run;
- Peck;
- GroundTurn.

### Flight

- TakeOff;
- FlightIdle;
- FlightForward;
- FlightTurn;
- Dive;
- Landing.

### Actions

- Peck;
- WingAttack;
- Hit;
- Death.

---

# 15. Необычные птицеподобные / Head-Body creatures

Существа, у которых большая голова фактически заменяет обычное туловище, не должны насильно подгоняться под стандартную птицу.

Для повторяющегося семейства допускается отдельный archetype, например концептуально:

```text
CG_AvianHeadBody_v1

Root
HeadBody
Beak

Leg_L
Leg_R

Wing_L optional
Wing_R optional
```

Если такой anatomy type представлен несколькими существами, он становится полноценным reusable archetype.

Если существует только один уникальный представитель, допускается специализированный archetype, но не отдельная новая производственная система.

---

# 16. Insect

Для шестиногих существ предполагается отдельный anatomy contract.

Пример:

```text
CG_Insect6_v1

Root
Thorax
Abdomen
Head

Leg1_L / Leg1_R
Leg2_L / Leg2_R
Leg3_L / Leg3_R

Wing_L / Wing_R optional
Mandible_L / Mandible_R optional
```

Модульность остаётся:

```text
Head_04
Thorax_02
Abdomen_07
LegSet_Long_03
Mandibles_02
Wings_None
```

другое существо:

```text
Head_01
Thorax_05
Abdomen_02
LegSet_Short_01
Mandibles_05
Wings_Transparent_02
```

Оба могут использовать один `CG_Insect6_v1` и общий базовый locomotion package.

Пример action package:

- InsectIdle;
- InsectWalk;
- InsectRun;
- Turn;
- Bite;
- Sting;
- Hit;
- Death.

---

# 17. Arachnid и другие количества конечностей

Паук не должен искусственно использовать шестиногий insect rig.

Для восьминогого семейства создаётся отдельный contract, например:

`CG_Arachnid8_v1`.

То же правило распространяется на:

- четырёхруких существ;
- существ с дополнительными конечностями;
- необычные крылатые конструкции;
- segmented creatures;
- другие устойчиво повторяющиеся anatomy patterns.

Новый archetype дешевле долгосрочного набора костылей вокруг несовместимого skeleton.

---

# 18. Custom и боссы

Не каждое существо обязано войти в массовый archetype.

Если анатомия принципиально уникальна:

- custom boss;
- уникальная многосегментная форма;
- особая трансформация;
- существо с уникальной механикой тела;

допускается `CUSTOM` rig/archetype.

Но CUSTOM должен оставаться исключением.

Даже custom entity должна использовать общие части Factory там, где это возможно:

- EntityRecipe;
- validation;
- sockets;
- action contracts;
- event system;
- VFX/SFX hooks;
- review pipeline.

---

# ЧАСТЬ III — RIG ARCHETYPE LIBRARY

# 19. RigArchetypeDefinition

Конкретное имя структуры будет принято в M1.

Каждый archetype должен фиксировать контракт:

- hierarchy;
- стабильные bone names;
- обязательные bones;
- optional bones;
- sockets;
- weapon grips;
- FX sockets;
- head/eye targets;
- foot targets;
- wing targets;
- tail/segment rules;
- допустимые proportion ranges;
- binding rules;
- compatible animation sets;
- compatible procedural layers.

Главный принцип:

> **риг создаётся не для каждой модели, а для класса строения тела.**

---

# 20. Humanoid rig contract

Рабочая схема:

```text
CG_Humanoid_v1

Root
Pelvis

Spine
Chest
Head

UpperArm_L
Forearm_L
Hand_L

UpperArm_R
Forearm_R
Hand_R

Thigh_L
Leg_L
Foot_L

Thigh_R
Leg_R
Foot_R
```

Плюс стандартные sockets:

- RightHand;
- LeftHand;
- Back;
- HipLeft;
- HipRight;
- Head;
- Chest;
- FX_Weapon;
- FX_Hand_L;
- FX_Hand_R.

Финальная hierarchy определяется экспериментом, но принцип стабильного контракта утверждён.

---

# 21. Rigid + controlled deformation

CubeGame не обязан использовать один способ деформации везде.

Допускается гибрид:

- жёсткие voxel-части там, где это лучше сохраняет стиль;
- controlled skinning для суставов и нужной пластики;
- segmented chains для хвостов, змей и других секций;
- procedural/IK corrections поверх базовой анимации.

Запрещается мягкая деформация, если она делает кубические формы визуально «резиновыми».

---

# ЧАСТЬ IV — ANIMATION FACTORY

# 22. Animation Resolver

Персонажу или существу не нужно вручную назначать десятки конкретных clips.

Animation Resolver получает характеристики entity.

Пример гуманоида:

```text
Skeleton = Humanoid_v1
BodyProfile = Heavy
WeaponClass = TwoHandedSword
CombatStyle = Warrior
MovementStyle = GroundHumanoid
```

и собирает animation loadout:

```text
Humanoid_Locomotion
+
Humanoid_Reactions
+
Humanoid_Death
+
TwoHandSword_Combat
+
HeavyBody_Corrections
+
FootIK
+
TwoHandGripIK
+
LookAt
```

Пример призрака:

```text
Anatomy = Floating
Movement = Floating
Combat = Magic
```

получает:

```text
Floating_Locomotion
Floating_Reactions
FloatingCaster_Combat
HoverProcedural
LookAt

FootIK = OFF
```

Пример волка:

```text
Anatomy = Quadruped
Movement = GroundQuadruped
Combat = BiteClaw
```

получает:

```text
Quadruped_Locomotion
Quadruped_Reactions
BiteClaw_ActionSet
QuadrupedGroundCorrection
```

---

# 23. Иерархия animation packages

## Layer A — Base Locomotion

Принадлежит anatomy/skeleton archetype.

Humanoid:

- idle;
- walk;
- run;
- sprint;
- start;
- stop;
- turn;
- jump;
- fall;
- land;
- dodge;
- hurt;
- knockback;
- death variants.

Quadruped:

- idle;
- walk;
- trot/run;
- sprint;
- turn;
- leap;
- hurt;
- knockback;
- death.

Floating:

- hover idle;
- move;
- fast move;
- turn;
- rise;
- descend;
- hit;
- death.

Avian:

- ground locomotion;
- takeoff;
- flight;
- turn;
- dive;
- landing.

## Layer B — Weapon / Combat Set

Создаётся по формуле:

```text
SKELETON / ANATOMY ARCHETYPE
×
WEAPON / COMBAT CLASS
```

Humanoid examples:

- one-handed sword;
- two-handed sword;
- dual axes;
- staff;
- monk staff;
- bow;
- unarmed / claws;
- future classes.

Общий package может включать:

- combat idle;
- light chain;
- heavy;
- charged heavy;
- special;
- block;
- parry;
- dodge attack;
- recovery;
- weapon-specific transitions.

## Layer C — Creature Action Set

Пример:

```text
QuadrupedMedium:
bite
claw
leap
charge
```

## Layer D — Signature Animations

Только для действительно уникального поведения:

- boss slam;
- phase transition;
- unique roar;
- grab;
- magic attack;
- unusual body action;
- unique species action.

## Layer E — Procedural / Additive

Не должно плодить отдельные clips без необходимости:

- FootIK;
- HandIK;
- secondary hand grip;
- look-at;
- aim;
- ground alignment;
- body lean;
- recoil;
- breathing;
- head tracking;
- wing/tail secondary adjustments;
- weapon alignment.

---

# 24. Минимальный полный animation slice для первого гуманоида

До массового производства первый humanoid archetype должен доказать полный рабочий срез.

Locomotion / reactions:

- Idle;
- Walk;
- Run;
- Sprint;
- Turn;
- Stop;
- Dodge;
- Jump/Land;
- Hit;
- Death.

Первое оружие должно доказать боевой контур.

Рабочий первый кандидат — двуручный меч.

Минимальный combat slice:

- CombatIdle;
- Light 1;
- Light 2;
- Light 3;
- Heavy;
- Charged Heavy;
- Block;
- Parry, если применимо;
- Special;
- Recovery.

Точное финальное число clips будет определено при M1.

Смысл эксперимента — доказать всю архитектуру от model/rig до gameplay interaction, а не заранее создать сотни clips.

---

# 25. WeaponDefinition и взаимодействие с оружием

Оружие является частью общей data-driven системы.

Концептуально WeaponDefinition должен описывать:

```text
WeaponClass
CompatibleRigArchetypes

MainGrip
SecondaryGrip

CombatAnimationSet
IKProfile

HitGeometry
Reach

TrailSocket
VFX / SFX hooks
```

Пример двуручного меча:

```text
WeaponClass = TwoHandedSword

MainGrip = RightHand
SecondaryGrip = LeftHand

CombatSet = Humanoid_2HSword
IKProfile = TwoHandGrip_Long
```

Это позволяет одному weapon/combat package работать на нескольких compatible humanoids.

Не должны автоматически появляться:

- `OrcGreatswordAnimation`;
- `HumanGreatswordAnimation`;
- `ElfGreatswordAnimation`;

если один `Humanoid_2HSword` с коррекциями работает на всех.

---

# 26. IK / Procedural как обязательный слой масштабирования

IK и procedural corrections не являются декоративным дополнением.

Они являются одним из основных механизмов повторного использования анимаций между разными пропорциями.

Приоритетные задачи:

- стопы на рельефе;
- main hand weapon alignment;
- secondary hand grip;
- точный хват двуручного оружия;
- look-at;
- aim;
- ground alignment;
- body lean;
- небольшие body proportion corrections;
- secondary motion;
- recoil.

Пример проблемы:

```text
один GreatswordHeavy clip
↓
Human Standard
Orc Heavy
Elf Light
↓
разные длины рук и ширина плеч
↓
IK / correction profiles
↓
единый читаемый удар без трёх новых clips
```

---

# 27. Gameplay contract отделён от animation clip

Animation clip не является хозяином боевой механики.

Для атаки хранится отдельный combat/action contract.

Он может содержать:

- total duration;
- windup;
- active window;
- recovery;
- input buffer;
- combo open;
- combo close;
- dodge cancel;
- movement cancel;
- rotation allowance;
- turn speed;
- root movement;
- hitbox start;
- hitbox end;
- i-frames, если применимо;
- parry window, если применимо;
- hit stop;
- animation playback rate;
- blend in/out;
- weapon trail;
- sound/VFX events.

Концептуальный пример:

```text
TwoHandSword_Heavy_01

Duration
Windup
Active
Recovery

Rotation windows
Dodge cancel windows
Hitbox window
Trail window
```

Конкретные числа не фиксируются данным документом.

Они будут определяться на основании gameplay design и измеримых референсов при открытии анимационного подэтапа.

---

# 28. Источники и референсы движения

Для создания собственных анимаций допускается гибрид:

- ручная keyframe-анимация;
- mocap как черновая база;
- video mocap;
- AI/text-to-motion как черновая база;
- procedural animation;
- изучение Alpha как reference;
- технические референсы других игр;
- комбинация методов.

Финальная анимация CubeGame должна контролироваться нашей системой.

Особенно вручную проверяются:

- key poses;
- anticipation;
- impact;
- follow-through;
- recovery;
- weapon trajectory;
- foot placement;
- center of mass;
- gameplay readability;
- timing.

Ранее выбранный принцип технических референсов сохраняется:

- Souls / Elden Ring / Sekiro — combat timing, attack phases, windows;
- Hades / Hades II — responsiveness, combo/charge/cancel flow;
- For Honor — production animation/event philosophy;
- Unity sample code / Animation Jobs — Unity animation architecture и IK;
- Unreal Game Animation Sample — advanced locomotion, warping, pose selection ideas.

Референсы используются для понимания технических принципов, кода, событий и числовых профилей, а не для копирования чужих ассетов.

---

# ЧАСТЬ V — FACTORY ARCHITECTURE

# 29. Общая схема

Утверждённая концептуальная архитектура:

```text
                    ENTITY DATABASE
                          │
       ┌──────────────────┼──────────────────┐
       ↓                  ↓                  ↓
   BODY PARTS        RIG ARCHETYPES      EQUIPMENT
       │                  │                  │
       └──────────────────┼──────────────────┘
                          ↓
                      ENTITY RECIPE
                          ↓
                CHARACTER / CREATURE BUILDER
                          ↓
                   ASSEMBLED ENTITY
                          │
             ┌────────────┴────────────┐
             ↓                         ↓
      ANIMATION RESOLVER         EQUIPMENT RESOLVER
             ↓                         ↓
   locomotion / combat              weapon
   actions / reactions              armor
             │                         │
             └────────────┬────────────┘
                          ↓
                    IK / PROCEDURAL
                          ↓
                     GAME ENTITY
```

---

# 30. Основные логические компоненты

Имена здесь рабочие и не фиксируют финальную C# API.

M1 должен спроектировать эквиваленты следующих ролей:

- `EntityRecipe / CharacterRecipe`;
- `BodyPartDefinition`;
- `RigArchetypeDefinition`;
- `BodyProfileDefinition`;
- `WeaponDefinition`;
- `EquipmentDefinition`;
- `AnimationPackageDefinition`;
- `CombatActionDefinition`;
- `CharacterBuilder / CreatureBuilder`;
- `AnimationResolver`;
- `EquipmentResolver`;
- `IKProfile`;
- `ProceduralProfile`;
- `EntityValidator`.

При открытии M1 эти роли будут разложены на реальные Unity/C# структуры после отдельного проектирования.

---

# 31. CubeGame Factory Editor

Нужен editor-инструмент для ручной проверки Factory.

Минимальные возможности концептуально:

- выбрать species/race;
- выбрать anatomy;
- выбрать body profile;
- менять голову;
- менять торс;
- менять руки;
- менять ноги;
- менять волосы/бороду/особые детали;
- менять пропорции;
- ставить экипировку;
- ставить оружие;
- выбирать animation package;
- randomize;
- save recipe;
- spawn entity.

Preview должен позволять быстро проверять:

- Idle;
- Walk;
- Run;
- Dodge;
- Attack;
- Heavy;
- Block;
- Hit;
- Death;
- хват оружия;
- clipping;
- IK.

Для летающих и floating archetypes preview должен переключаться на их соответствующий movement package.

---

# 32. Автоматическая валидация

До batch Factory обязана уметь автоматически выявлять хотя бы основные классы ошибок:

- missing bone;
- invalid bone name;
- wrong rig archetype;
- unsupported body part;
- missing socket;
- invalid weapon grip;
- missing IK target;
- incompatible equipment;
- missing required animation package;
- duplicate ID;
- invalid attachment;
- invalid bounds;
- invalid scale;
- invalid pivot/orientation;
- likely clipping candidates;
- broken import;
- performance budget violation.

На сотнях моделей ручной поиск таких ошибок недопустим.

---

# 33. Неразрушающий pipeline

Автоматизация не должна стирать утверждённые ручные правки.

Предпочтительная модель:

```text
SOURCE / GENERATED BASE
↓
DETERMINISTIC STYLE OPS
↓
AUTO RIG / ASSIGNMENT
↓
MANUAL DELTA
↓
APPROVED ASSET
```

При изменении автоматических правил ручные правки не перезаписываются молча.

Конфликт отправляет asset на review.

---

# ЧАСТЬ VI — ПОРЯДОК ЭКСПЕРИМЕНТА

# 34. E0 — подготовка

До производства:

- выбрать test scene;
- определить gameplay camera distance;
- определить формат visual review;
- подготовить animation test harness;
- подготовить combat dummy;
- подготовить equipment/weapon test;
- подготовить debug отображение bones/sockets/IK;
- подготовить validation harness.

Результат: один воспроизводимый испытательный стенд.

---

# 35. E1 — первый humanoid vertical slice

Первый humanoid должен доказать:

- новый стиль;
- modular body parts;
- humanoid rig;
- body profile;
- CharacterRecipe;
- builder;
- locomotion;
- reactions;
- один weapon class;
- combat package;
- grips;
- IK;
- gameplay contract;
- Unity runtime.

Это первый полноценный Archetype Package.

---

# 36. E2 — Human Standard + Orc Heavy

Критически важный экзамен.

Берутся два заметно разных гуманоида:

- Human Standard;
- Orc Heavy.

Они должны использовать:

- один humanoid rig contract;
- один базовый locomotion package;
- один reaction/death package;
- одно оружие — например Greatsword;
- один `Humanoid_2HSword` combat package;
- procedural/IK corrections вместо отдельной полной библиотеки орка.

Оба должны корректно:

- стоять;
- ходить;
- бегать;
- поворачиваться;
- уклоняться;
- держать одно и то же оружие;
- использовать secondary grip;
- выполнять light attacks;
- выполнять heavy;
- выполнять charged attack;
- получать hit reaction;
- умирать.

Если Orc Heavy требует заново создавать значительную часть общей библиотеки, architecture package возвращается на доработку.

---

# 37. E3 — доказательство общей Creature Factory на разных anatomy classes

После доказательства базового humanoid package Factory должна быть проверена на принципиально разных строениях тела.

Минимальный исследовательский набор:

1. Humanoid — Human/Orc.
2. Quadruped — например wolf-type.
3. Floating — ghost/spectral-type.
4. Insect либо unusual Avian.

Цель не в том, чтобы сразу закончить все их animation libraries.

Цель — доказать, что одна общая Factory умеет:

```text
Recipe
→ Anatomy classification
→ Rig assignment
→ Body assembly
→ Movement package assignment
→ Action package assignment
→ Procedural profile
→ Validation
```

для принципиально разных типов тела.

---

# 38. E4 — масштабирование внутри каждого archetype

После доказательства archetype:

```text
1
→ 2
→ 5
→ 10
→ 25
→ 50
→ controlled batch
```

Каждый новый размер партии проверяет:

- reuse;
- compatibility;
- manual correction cost;
- animation reuse;
- equipment fit;
- clipping;
- import stability;
- performance;
- visual consistency.

Нельзя компенсировать плохую архитектуру ручной работой на каждой новой модели.

---

# 39. E5 — все humanoid family anchors

После доказанной humanoid Factory создаётся по одному эталону каждого необходимого humanoid species/family.

Для каждого фиксируются:

- anatomy compatibility;
- allowed parts;
- proportion ranges;
- face rules;
- body profiles;
- equipment rules;
- animation reuse;
- corrections;
- signature actions.

После принятия family anchor можно выпускать варианты.

---

# 40. E6 — creature archetypes

Для каждого необходимого creature archetype повторяется:

```text
REFERENCE CREATURE
↓
RIG / ANATOMY CONTRACT
↓
BASE MOVEMENT
↓
BASE ACTIONS
↓
SECOND CREATURE
↓
REUSE
↓
5
↓
10
↓
CONTROLLED BATCH
```

Не запускать сразу все families.

---

# 41. E7 — production batches

Только после доказанного Factory pipeline:

- 25;
- 50;
- следующая утверждённая партия.

Batch не отменяет human visual review.

Автоматика отвечает за технический отсев.

Человек отвечает за:

- стиль;
- силуэт;
- читаемость;
- качество движения;
- художественную целостность;
- сложные clipping/interaction cases.

---

# ЧАСТЬ VII — МЕТРИКИ

# 42. Главная производственная метрика

Не количество «созданных моделей в час».

Главная метрика:

> **человеко-минуты на один APPROVED игровой asset/entity.**

Для animation packages:

> **человеко-минуты на один APPROVED reusable animation package / signature clip.**

---

# 43. Метрики моделей и Factory

- процент assets, прошедших validation с первого раза;
- среднее число ручных исправлений;
- время до approved;
- процент reusable rig assignment;
- процент автоматически назначенных sockets;
- процент compatible equipment без ручной подгонки;
- процент entities, собираемых Recipe → Factory без bespoke code;
- количество custom exceptions.

---

# 44. Метрики анимаций

- процент моделей archetype, использующих общий locomotion;
- процент body profiles, работающих на одном clip set;
- процент weapon users, использующих общий combat package;
- количество specialized variants;
- количество signature animations;
- количество procedural fixes;
- foot sliding;
- grip errors;
- clipping;
- timing regressions;
- число случаев, где новая модель потребовала новую полную animation library.

Последняя метрика должна стремиться к нулю внутри совместимого archetype.

---

# ЧАСТЬ VIII — КРИТЕРИИ УСПЕХА

# 45. Humanoid Factory считается доказанной, если

1. Human Standard и Orc Heavy собираются одной Factory.
2. Они используют один humanoid rig contract.
3. Они используют общую locomotion.
4. Они используют один weapon combat package.
5. Один Greatsword корректно работает на обоих.
6. Различия хвата компенсируются IK/correction profiles.
7. Character Creator работает через тот же recipe/builder.
8. Случайный NPC может быть собран тем же builder.
9. Переход 2 → 5 → 10 не требует отдельных ручных pipelines.
10. Различные humanoid species сохраняют визуальную индивидуальность.

---

# 46. Creature Factory считается доказанной, если

1. Factory не привязана только к Humanoid.
2. Quadruped получает собственный reusable rig/action package.
3. Floating entity не получает ненужную foot locomotion/FootIK.
4. Avian получает ground/flight movement по своему archetype.
5. Insect/Arachnid используют правильное количество и назначение конечностей.
6. Необычные anatomy classes могут получить новый reusable archetype без новой отдельной Factory.
7. Custom остаётся исключением.
8. Общие validation/editor/review systems работают на всех archetypes.

---

# 47. Animation Factory считается доказанной, если

1. Один rig archetype обслуживает несколько моделей.
2. Один locomotion package обслуживает несколько compatible entities.
3. Один combat/action package обслуживает несколько compatible entities.
4. Разные body proportions исправляются преимущественно procedural/IK слоями.
5. Новая модель не требует полного набора новых animations.
6. Signature clips появляются только из-за реального уникального поведения.
7. Gameplay timing существует отдельно от visual clip.
8. Анимации проверены в реальном gameplay.
9. Рост количества моделей не вызывает линейный рост animation production.

---

# ЧАСТЬ IX — ЧТО НЕ ФИКСИРУЕТСЯ ДО ОТКРЫТИЯ M1

# 48. Детальная реализация будет спроектирована при начале этапа

Архитектурная концепция этого документа утверждена.

Но данный документ **намеренно не фиксирует окончательно**:

- точные названия C# классов;
- ScriptableObject vs другие форматы для каждого типа данных;
- точную hierarchy каждой кости;
- точное число rig archetypes;
- точное число body profiles;
- окончательные proportion coefficients;
- Generic vs смешанный путь для отдельных humanoid cases;
- точный animation runtime graph;
- Animator Controller vs Playables vs hybrid;
- точную систему retargeting;
- точные IK constraints;
- конкретный authoring tool;
- Blender/Cascadeur/mocap/AI доли production pipeline;
- точный формат animation/action contracts;
- точные combat timing numbers;
- точный Editor UI;
- конкретную автоматизацию batch;
- performance budgets;
- final folder/API architecture.

Это будет обсуждаться и проектироваться **непосредственно перед началом M1**, когда:

- M0 будет закрыт;
- текущий Unity runtime будет известен;
- будут доступны финальные требования к моделям;
- будет выбран первый реальный representative set;
- можно будет проверить решения экспериментом, а не предположением.

До этого момента текущий документ является **утверждённой архитектурной целью и экспериментальным планом**, а не разрешением преждевременно писать production-реализацию M1.

---

# 49. Итоговая последовательность M1

```text
M0 CLOSED
↓
M1 DESIGN REVIEW
↓
DETAILED FACTORY IMPLEMENTATION PLAN
↓
EXPERIMENT HARNESS
↓
FIRST HUMANOID MODULAR MODEL
↓
CG_HUMANOID RIG CONTRACT
↓
ENTITY RECIPE
↓
HUMANOID BUILDER
↓
BASE LOCOMOTION
↓
FIRST WEAPON / COMBAT PACKAGE
↓
IK / PROCEDURAL
↓
HUMAN STANDARD + ORC HEAVY
↓
REUSE PROVEN
↓
2 → 5 → 10 HUMANOIDS
↓
HUMANOID FAMILY ANCHORS
↓
QUADRUPED
↓
FLOATING
↓
AVIAN / UNUSUAL AVIAN
↓
INSECT / ARACHNID
↓
OTHER REQUIRED ARCHETYPES
↓
1 → 2 → 5 → 10 FOR EACH FAMILY
↓
25
↓
50
↓
CONTROLLED PRODUCTION BATCHES
↓
NEW ORIGINAL CONTENT THROUGH THE SAME FACTORY
```

Главный принцип моделей:

> **не создавать каталог несвязанных моделей — создавать модульные entities, совместимые с утверждёнными anatomy/rig contracts.**

Главный принцип анимаций:

> **не создавать полную animation library для каждой модели — создавать reusable animation packages для archetypes, оружия и действий.**

Главный принцип генерации мира:

> **Character Creator, NPC Generator и Enemy Generator должны использовать один и тот же EntityRecipe и одну и ту же Character / Creature Factory.**

Главный принцип существ:

> **не загонять всё в один skeleton — создавать ограниченное число хорошо доказанных anatomy/rig archetypes и использовать общую Factory поверх них.**

Главный принцип масштабирования:

> **сначала доказать качество на одном эталоне, затем доказать повторное использование, затем масштабировать 2 → 5 → 10 → 25 → 50 → batch.**

---

# 50. Связанные документы

- [ROADMAP_DRAFT](../planning/ROADMAP_DRAFT.md)
- [S05_ASSET_CONVERSION_PLAN](S05_ASSET_CONVERSION_PLAN.md)
- [D65 — full catalog run](../decisions/D65-d64-result-approved-deepseek-full-catalog-run.md)
- [D67 — original game first visual audit](../decisions/D67-original-game-first-visual-audit.md)
- [ACTIVE_STAGE](../ACTIVE_STAGE.md)

---

_Концепция Character / Creature Factory, модульной LEGO-сборки, Rig Archetypes, EntityRecipe, Animation Resolver, weapon/action packages, IK/procedural layers и масштабирования по archetypes утверждена продюсером 2026-11-10. Детальная техническая реализация будет отдельно спроектирована при фактическом открытии M1. Документ не разрешает начинать M1 до закрытия M0 и отдельного решения продюсера._