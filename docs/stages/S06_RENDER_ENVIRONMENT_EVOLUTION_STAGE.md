# S06 — эволюция материалов, рендера, окружения и погоды CubeGame

**Статус:** `APPROVED CONCEPT / FUTURE STAGE / NOT_AUTHORIZED`  
**Граница по D82/D91:** погодные эффекты и задел под Season System в S06, настоящая сезонная смена — в postlaunch-контуре; сроки и порядок будущих релизных этапов см. в [Master Roadmap (review)](../planning/ROADMAP_CONSOLIDATION_PROPOSAL.md).  
**Направление roadmap:** M1 — модели, анимации и визуал.  
**Концептуальные решения утверждены продюсером:** 2026-11-10.  
**Начало исполнения:** только после закрытия M0 и отдельного решения продюсера.  
**Назначение документа:** зафиксировать утверждённую концепцию визуальной эволюции мира CubeGame: семантические поверхности voxel-мира, детализированные материалы, shader layers, трава/листва/деревья, вода, освещение, ветер, погода, реакция мира на окружающие условия и архитектурный задел под будущие времена года. Конкретные shader graphs/HLSL, URP/HDRP-решения, форматы данных, классы, GPU budgets и production tools проектируются непосредственно перед фактическим открытием S06.

---

# 1. Главная цель

S06 не должен сводиться к замене однотонных voxel-цветов на набор картинок.

Цель — построить **CubeGame Environment / Surface / Weather Rendering System**, где:

- генератор мира определяет, **что находится в мире**;
- semantic surface/material layer определяет, **из какого материала состоит поверхность**;
- renderer и shaders определяют, **как эта поверхность выглядит**;
- environment/weather state определяет, **как она выглядит прямо сейчас**;
- vegetation/water/procedural layers добавляют объём и движение;
- будущая система сезонов сможет менять окружающий мир через уже существующие контракты, не заставляя переписывать всю графику.

Главный принцип:

> **геометрия и генерация мира не должны владеть окончательным визуальным видом поверхности.**

Один и тот же камень, дерево или участок земли должен уметь визуально реагировать на освещение, дождь, снег, влажность, ветер и в будущем сезон без замены самой геометрии мира.

---

# 2. Что уже существует в текущем M0

На момент утверждения концепции текущий Alpha-compatible voxel уже несёт полезную семантику.

`VoxelColumn.cs` хранит для voxel:

- RGB;
- flags;
- material id;
- признаки surface/covered/changed.

Восстановленные Alpha material ids уже различают, в частности:

- air;
- default solid;
- water;
- lake bed;
- grass;
- stone;
- snow/sand-like surface;
- shore;
- rare ground;
- отдельные специальные поверхности.

`AlphaColors.cs` уже вычисляет цвет поверхности с учётом:

- температуры;
- влажности;
- высоты;
- noise;
- rare/climate terms;
- типа местности.

Следовательно, фундамент для будущей Material/Surface System уже имеется: мир не является полностью «безымянной массой цветных кубов».

Одновременно текущий presentation path остаётся упрощённым:

- `ChunkMesher` в основном передаёт vertex color;
- `MeshData` хранит positions, normals, colors и indices;
- полноценного surface UV/material-layer contract сейчас нет;
- terrain визуально строится через общий pilot material;
- water path пока отдельный и частично placeholder;
- vegetation/content planning существует отдельно от terrain voxel mesh.

Это состояние является входом для S06, а не финальной архитектурой.

---

# 3. Базовая архитектура S06

Утверждённая концептуальная цепочка:

```text
WORLD GENERATION
      ↓
SEMANTIC WORLD DATA
terrain / water / vegetation / structures
      ↓
SURFACE RESOLVER
      ↓
CUBEGAME SURFACE CLASS
grass / dirt / stone / sand / snow / shore / bark / leaf / etc.
      ↓
MATERIAL DEFINITION
albedo / normal / roughness / height / detail / masks
      ↓
SURFACE SHADER
      ↓
DETAIL / PROCEDURAL DECORATORS
grass blades / stones / moss / roots / snow / debris
      ↓
ENVIRONMENT STATE
light / wind / rain / snow / humidity / temperature / wetness
      ↓
FINAL APPEARANCE
```

Генератор не должен напрямую знать конкретные Unity materials, shader graphs или texture arrays.

---

# 4. Semantic Surface Layer

Ключевое решение: будущая графика **не привязывается напрямую навсегда к Alpha material id**.

Alpha material id используется как один из входов.

Пример:

```text
Alpha voxel material
+
surface flag
+
temperature
+
humidity
+
biome / micro-biome
+
height
+
neighbourhood
+
world context
        ↓
CubeGame SurfaceResolver
        ↓
CubeGame SurfaceClass
```

Почему это необходимо:

один Alpha-compatible material id может использоваться в разных климатических ситуациях, но CubeGame должен визуально различать реальные категории поверхности.

Пример концептуально:

```text
Alpha material 9 + cold/wet
→ Snow

Alpha material 9 + hot/dry
→ Sand
```

Финальные правила должны быть проверены при открытии этапа; этот пример фиксирует архитектурный принцип, а не окончательную таблицу.

---

# 5. SurfaceClass

CubeGame должен иметь собственный смысловой слой поверх рендера.

Предварительные категории:

- Grass;
- Dirt;
- Mud;
- Stone;
- Sand;
- Snow;
- Ice;
- Shore;
- Gravel;
- Clay;
- MossyStone;
- Bark;
- WoodCut;
- Leaf;
- Water;
- BuildingStone;
- WoodPlank;
- Roof;
- Metal;
- прочие необходимые классы.

Это не окончательный каталог.

Главное правило:

> **SurfaceClass описывает смысл поверхности, а не конкретную texture asset.**

---

# 6. Material Definition

Каждый SurfaceClass должен разрешаться в материал/набор материалов, содержащий необходимые каналы.

Концептуально:

```text
SurfaceMaterial
├─ Albedo
├─ Normal
├─ Roughness
├─ Height / Detail
├─ AO при необходимости
├─ Detail masks
├─ tint rules
├─ wetness response
├─ snow response
├─ biome variation
└─ shader family
```

Нельзя создавать отдельный Unity Material на каждый voxel.

Terrain должен собираться крупными chunk meshes и использовать batched/GPU-friendly material selection.

---

# 7. Texture arrays / world-space mapping / triplanar

При проектировании S06 необходимо проверить подходы, позволяющие процедурному voxel-миру использовать большое количество поверхностей без ручной UV-развёртки каждого блока.

Предпочтительные направления исследования:

- `Texture2DArray` или эквивалентный grouped texture storage;
- world-space mapping;
- triplanar mapping для камня/земли/скал;
- deterministic texture variation;
- macro + micro detail;
- biome tint / climate tint;
- anti-tiling techniques.

Финальный метод выбирается экспериментом по качеству и производительности.

---

# 8. Детализированная трава

«Объёмная трава» не должна означать только зелёную normal map.

Grass surface состоит минимум из двух слоёв:

```text
GRASS SURFACE
├─ terrain material
│  ├─ albedo
│  ├─ normal
│  ├─ roughness
│  └─ micro detail
│
└─ vegetation/detail layer
   ├─ grass blades
   ├─ tufts
   ├─ small plants
   ├─ flowers where appropriate
   └─ biome variants
```

Геометрия voxel-мира остаётся кубической, но верхняя поверхность получает объёмный detail layer.

Не каждая грань grass voxel должна автоматически выращивать травинки. Placement учитывает:

- exposed top surface;
- slope/orientation;
- biome;
- moisture;
- temperature;
- object footprint;
- roads/structures;
- local density;
- distance/LOD.

---

# 9. Камень, земля, песок и снег

## Камень

Stone должен поддерживать:

- детализированный albedo;
- normal с мелкими сколами;
- roughness;
- height/detail;
- macro variation;
- optional moss/dirt overlay;
- wetness;
- snow accumulation.

## Земля

Dirt/Mud:

- мелкая фактура;
- комки/камешки/корни через detail layer;
- переход dry ↔ wet;
- возможные следы/decals в будущем;
- реакция на дождь.

## Песок

Sand:

- grain detail;
- normal/height;
- procedural/macroscopic wave variation;
- wind/weather tint where appropriate;
- переходы к камню/берегу.

## Снег

Snow:

- отдельная surface response;
- накопление преимущественно на направленных вверх поверхностях;
- визуальная глубина/softness по выбранному rendering path;
- future melt/coverage state;
- смешивание с underlying material.

---

# 10. Деревья как объектная Factory, а не просто угадывание цвета voxel

Дерево и другая сложная растительность должны рассматриваться как content object/recipe, когда это возможно.

Концептуально:

```text
TREE RECIPE
↓
species
↓
trunk / branches / crown / roots
↓
part semantics
↓
material resolver
↓
tree builder
```

Пример:

```text
Oak
├─ Trunk → Bark/Oak
├─ Branch → Bark/Oak
├─ CutFace → Wood/Oak
└─ LeafCluster → Leaf/Oak
```

Это позволяет:

- дать стволу узор настоящей коры;
- ориентировать рисунок вдоль ствола;
- использовать отдельный материал среза древесины;
- независимо управлять листвой;
- применять ветер к листьям и веткам;
- в будущем менять состояние дерева по сезону.

S06 должен использовать существующее разделение terrain и vegetation/content planning, а не превращать каждое дерево в случайный набор распознанных по цвету voxel-ов.

---

# 11. Shader Layer

Текстуры и shaders — разные уровни.

Текстуры задают материал поверхности.

Shaders добавляют динамическое поведение и физически правдоподобную реакцию на окружение.

Нужны не один монолитный «MegaShader», а ограниченное число shader families.

Концептуально:

- Terrain Shader;
- Foliage Shader;
- Bark/Wood Shader;
- Water Shader;
- Snow/Ice Shader;
- Building Shader;
- Character/Equipment integration shader;
- VFX Shader.

Все подходящие families получают общие Environment parameters.

---

# 12. Освещение и атмосфера

S06 должен включать визуальную систему мира:

- солнце;
- directional lighting;
- мягкие/динамические тени;
- ambient lighting;
- day/night-compatible architecture;
- exposure;
- fog;
- atmospheric scattering/приближённый аналог;
- sky/cloud integration;
- distance haze;
- graphical presets.

Цель — добиться визуального эффекта уровня «voxel world + современный shader pack», сохранив собственную кубическую эстетику CubeGame.

Конкретная технология выбирается после производительного prototype.

---

# 13. Ветер

Ветер является глобальным состоянием среды, а не отдельной анимацией каждого растения.

Environment передаёт как минимум концептуально:

```text
WindDirection
WindStrength
WindGust
Time
```

Разные shader/material profiles реагируют по-разному:

```text
grass
→ сильное сгибание

leaves
→ мелкое активное движение

small branches
→ умеренное движение

large branches
→ слабое движение

trunk
→ практически неподвижен
```

Нужны ограничения амплитуды, LOD и distance culling.

---

# 14. Вода

Вода является отдельной системой рендера.

Концептуальные составляющие:

```text
WATER
├─ wave motion
├─ multi-scale normals
├─ reflection
├─ refraction
├─ depth response
├─ transparency
├─ shoreline
├─ foam
├─ depth-based colour
├─ rain ripples
└─ weather/light response
```

Water renderer получает от мира факт наличия воды и геометрию/уровень воды, но её визуальное качество определяется отдельным shader/rendering layer.

---

# 15. Environment State

S06 должен ввести единый источник динамического состояния окружающей среды.

Рабочая концепция:

```text
EnvironmentState

TimeOfDay
SunAngle
Cloudiness

RainIntensity
SnowIntensity

WindDirection
WindStrength
WindGust

Temperature
Humidity

Wetness
SnowCoverage

FogDensity
StormIntensity
```

Точные поля и единицы измерения проектируются при открытии этапа.

Главный контракт:

> **погода и окружающая среда публикуют состояние; материалы, shaders, vegetation и water читают это состояние.**

---

# 16. Weather System

Погода должна быть системой состояния мира, а не только визуальными particle effects.

Базовые погодные состояния/параметры могут включать:

- clear;
- cloudy;
- rain;
- heavy rain;
- fog;
- snow;
- storm;
- wind;
- temperature;
- humidity.

Точный weather model будет спроектирован позже.

---

# 17. Реакция мира на дождь

Дождь должен вызывать согласованную реакцию мира.

Пример:

```text
RainIntensity ↑
        ↓
Wetness ↑
        ↓
terrain darkens
stone becomes wetter / more reflective
wood darkens
leaves change specular response
water receives rain ripples
fog/atmosphere may change
particles become visible
```

В будущем возможно расширение:

- puddles;
- streams;
- stronger runoff;
- muddy surfaces;
- footprints/splashes.

Эти расширения не считаются автоматически обязательными для первой реализации S06.

---

# 18. Снег и температура

Snow визуально связывается с:

- precipitation;
- temperature;
- surface orientation;
- snow coverage;
- material response.

Принцип:

```text
cold enough
+
snow precipitation
        ↓
SnowCoverage grows
```

Surface shader использует normal/orientation:

```text
surface facing upward
→ stronger accumulation

vertical surface
→ little accumulation
```

В первой реализации не требуется полноценная физическая симуляция снежного покрова.

---

# 19. Мир должен реагировать как одна система

Погода не должна состоять из независимых несвязанных эффектов.

Пример дождя:

```text
WEATHER
   ↓
RainIntensity / Wetness / Wind / Cloudiness
   ↓
┌───────────┬──────────┬───────────┬──────────┐
Terrain   Foliage     Water      Lighting    VFX
```

Один EnvironmentState должен синхронно влиять на все подходящие visual systems.

---

# 20. Отделение Weather от Season

Утверждён принцип:

> **Weather и Season являются разными системами.**

Weather — краткосрочное состояние:

```text
сейчас дождь
сейчас +18 °C
сейчас сильный ветер
сейчас высокая влажность
```

Season — долгосрочное состояние мира:

```text
Season = Autumn
SeasonProgress = 0.65
```

Сезон влияет на вероятностные/допустимые диапазоны погоды и долгосрочное состояние окружающей среды.

---

# 21. Времена года — future update, не S06 launch scope

Полноценные сезоны **не реализуются одновременно с первой системой текстур/графики/погоды**.

Порядок утверждён:

```text
1. Surface / Material System
↓
2. Environment / Shader System
↓
3. Weather + world reaction
↓
4. validate and ship stable visual foundation
↓
5. FUTURE UPDATE: Season System
```

Но S06 обязан оставить архитектурные seams для будущего подключения Season System.

---

# 22. Архитектурный задел под Season System

Environment contracts должны позволять в будущем добавить концептуально:

```text
Season
SeasonProgress

Temperature
Humidity
Wetness
SnowCoverage

VegetationGreenness
LeafRetention
GroundLeafCoverage
```

До появления сезонов значения могут быть зафиксированы в summer-like profile или в выбранном базовом climate profile.

Важно:

> **S06 не должен притворяться, что сезоны уже реализованы. Он должен только не закрыть дорогу к ним.**

---

# 23. Будущее поведение сезонов

Будущий Season System должен менять мир постепенно, а не переключать четыре готовых сцены.

## Spring

- температура постепенно растёт;
- снег тает;
- влажность может увеличиваться;
- появляется новая зелень;
- лиственные растения набирают foliage density.

## Summer

- преимущественно плюсовая/высокая температура;
- зелёная листва;
- дожди и ветер остаются возможны;
- vegetation находится в полном состоянии.

## Autumn

- температура снижается;
- foliage colour постепенно:
  `green → yellow → orange → brown`;
- `LeafRetention` уменьшается;
- часть foliage clusters исчезает;
- появляются falling leaves;
- возможно накопление ground leaves.

## Winter

- низкая температура;
- снег;
- низкая foliage density у лиственных деревьев;
- snow accumulation;
- потенциальное замерзание части воды как отдельное будущее решение.

Это conceptual behaviour, а не окончательные климатические числа.

---

# 24. Не четыре модели одного дерева

Запрещён как основной pipeline подход:

```text
Oak_Spring
Oak_Summer
Oak_Autumn
Oak_Winter
```

Предпочтительная архитектура:

```text
OakTree
+
SeasonProfile
+
EnvironmentState
+
Foliage Shader
+
Vegetation State
```

Часть сезонных изменений может быть shader-driven, часть — data/geometry-driven.

Пример осени:

```text
SeasonProgress
↓
foliage tint changes
+
LeafRetention decreases
+
some leaf clusters disabled
+
falling leaf VFX
+
ground leaf coverage increases
```

Таким образом сезон действительно меняет состояние мира, а не просто накладывает цветовой фильтр.

---

# 25. Граница Shader vs Gameplay World State

Не вся реакция мира должна происходить только в shader.

Shader подходит для:

- colour;
- wetness appearance;
- snow blend;
- wind deformation;
- water waves;
- foliage tint;
- surface micro detail.

World/game state требуется, когда изменение должно иметь устойчивое или игровое значение:

- вырос/исчез foliage cluster;
- накопились ground leaves;
- замёрзла вода и изменилась проходимость;
- появилась лужа как gameplay object;
- snow depth влияет на gameplay;
- объект повреждён погодой.

Такие функции вводятся отдельными решениями, чтобы графическая система не начала скрыто менять gameplay.

---

# 26. Связь с M1 Character / Creature Factory

S06 и [M1 Character / Creature Factory](M1_MODEL_ANIMATION_EVOLUTION_STAGE.md) являются соседними системами, а не одной системой.

M1 отвечает за:

- модели;
- modular body parts;
- rigs;
- animation packages;
- equipment;
- IK/procedural animation.

S06 отвечает за:

- surface/material rendering;
- environment;
- world shaders;
- vegetation rendering;
- water;
- weather visuals;
- world reaction;
- future season seams.

При этом они должны иметь совместимый общий material/environment contract, чтобы персонаж, броня и оружие тоже могли корректно реагировать на:

- дождь;
- снег;
- освещение;
- wetness;
- fog;
- environment effects.

---

# 27. Performance principle

Красивый shader pack бессмыслен, если игра превращается в слайд-шоу.

S06 обязан иметь несколько quality presets и бюджеты.

Проверяются:

- GPU frame time;
- CPU frame time;
- draw calls;
- shader variants;
- memory;
- texture memory;
- vegetation density;
- shadow distance;
- water cost;
- weather cost;
- LOD transitions;
- chunk streaming interaction.

Нельзя создавать GameObject/Material на каждый voxel или каждую травинку.

Массовые детали должны использовать GPU-friendly/batched/instanced/procedural подходы, выбранные после prototype.

---

# 28. Этапность эксперимента

## E0 — test scene

Подготовить reproducible visual benchmark:

- grass;
- dirt;
- stone;
- sand;
- snow;
- tree;
- water;
- building surface;
- персонаж;
- разные времена суток;
- controlled weather.

## E1 — Surface prototype

Доказать:

- semantic SurfaceClass;
- material selection;
- detailed textures;
- macro/micro variation;
- отсутствие критического tiling;
- chunk compatibility.

## E2 — Grass / vegetation

Доказать:

- detailed ground;
- grass blades;
- foliage shader;
- wind;
- LOD;
- acceptable performance.

## E3 — Water

Доказать:

- waves;
- transparency/depth;
- shoreline;
- reflections/refraction по выбранному пути;
- rain response.

## E4 — Lighting / atmosphere

Доказать:

- sunlight;
- shadows;
- fog/atmosphere;
- graphical presets;
- day/night-compatible architecture.

## E5 — Weather vertical slice

Минимум один дождевой сценарий должен одновременно менять:

- particles;
- lighting/atmosphere;
- terrain wetness;
- stone/wood response;
- foliage;
- water;
- wind.

## E6 — Snow/weather response

Доказать snow coverage/orientation response без полноценной Season System.

## E7 — performance scaling

Проверить не одну beauty scene, а настоящий streamed world.

---

# 29. Критерии успеха S06

S06 visual foundation считается доказанным, если:

1. Мир больше не зависит только от vertex RGB для финального вида.
2. Есть CubeGame semantic surface/material layer.
3. Grass/stone/sand/snow визуально отличаются материалом, а не только цветом.
4. Grass получает объёмный detail layer.
5. Деревья используют осмысленные bark/leaf materials.
6. Foliage и grass реагируют на общий wind state.
7. Вода имеет отдельный современный rendering path.
8. Освещение/тени/атмосфера дают утверждённый визуальный уровень.
9. Rain влияет не только на particles, но и на мир.
10. Snow coverage умеет реагировать на поверхность.
11. EnvironmentState является единым источником динамических visual conditions.
12. Система не требует отдельного material/GameObject на каждый voxel.
13. Графика работает в streamed world с утверждённым performance budget.
14. Архитектура допускает подключение сезонов без переписывания Material/Weather core.
15. Полноценная Season System остаётся future update, пока отдельно не авторизована.

---

# 30. Что не фиксируется до открытия S06

Концепция утверждена, но сейчас намеренно не фиксируются:

- URP vs другой final rendering path;
- Shader Graph vs handwritten HLSL vs hybrid;
- конкретные shader names;
- точные SurfaceClass ids;
- точная mapping table Alpha → CubeGame;
- texture resolution;
- packing channels;
- Texture2DArray layout;
- exact triplanar implementation;
- exact grass rendering technology;
- exact foliage animation method;
- exact reflection technique;
- exact water implementation;
- exact cloud solution;
- exact fog/atmosphere solution;
- final day/night system;
- exact weather probabilities;
- exact temperature units/ranges;
- exact snow accumulation model;
- puddle system;
- gameplay effects of weather;
- final Season calendar;
- season duration;
- climate-specific seasons;
- biome exceptions;
- freezing/thawing gameplay;
- performance budgets;
- final quality presets.

Эти решения принимаются непосредственно перед исполнением через prototypes и profiling.

---

# 31. Итоговая последовательность

```text
M0 CLOSED
↓
VISUAL DESIGN REVIEW
↓
S05/M1 MODEL & ASSET FOUNDATION AS REQUIRED
↓
S06 DESIGN REVIEW
↓
SEMANTIC SURFACE CONTRACT
↓
MATERIAL SYSTEM
↓
DETAILED TERRAIN TEXTURES
↓
GRASS / VEGETATION DETAIL
↓
TREE MATERIAL PIPELINE
↓
SHADER FAMILIES
↓
LIGHTING / SHADOWS / ATMOSPHERE
↓
WATER
↓
GLOBAL WIND
↓
ENVIRONMENT STATE
↓
WEATHER
↓
RAIN WORLD RESPONSE
↓
SNOW WORLD RESPONSE
↓
STREAMED-WORLD PERFORMANCE VALIDATION
↓
S06 VISUAL FOUNDATION
↓
FUTURE UPDATE
↓
SEASON SYSTEM
↓
SPRING / SUMMER / AUTUMN / WINTER
```

Главный принцип материалов:

> **мир хранит смысл поверхности, а renderer выбирает её визуальное представление.**

Главный принцип shaders:

> **shader улучшает и оживляет материал, но не должен становиться скрытым владельцем gameplay state.**

Главный принцип погоды:

> **погода — это общее состояние окружения, на которое согласованно реагируют terrain, foliage, water, lighting и VFX.**

Главный принцип сезонов:

> **сейчас закладываем архитектуру, полноценные времена года вводим позже отдельным обновлением.**

---

# 32. Связанные документы

- [Master Roadmap (review)](../planning/ROADMAP_CONSOLIDATION_PROPOSAL.md)
- [M1 — модели, риги и анимации](M1_MODEL_ANIMATION_EVOLUTION_STAGE.md)
- [ACTIVE_STAGE](../ACTIVE_STAGE.md)
- [PROJECT_MEMORY](../../PROJECT_MEMORY.md)

---

_Концепция Surface/Material System, detail layers, shader families, modern water, lighting/shadows, global wind, EnvironmentState, Weather System, реакции мира на дождь/снег и архитектурного задела под будущую Season System утверждена продюсером 2026-11-10. Документ не разрешает начинать S06 до закрытия M0 и отдельного решения продюсера. Полноценные времена года являются будущим обновлением, а не обязательным scope первой реализации S06._
