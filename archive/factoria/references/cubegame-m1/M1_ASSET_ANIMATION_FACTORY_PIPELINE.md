# M1 — технология конвейера моделей, ригов и анимаций

**Статус:** PROPOSED TECHNICAL PIPELINE / REVIEW ONLY / M1 NOT_AUTHORIZED  
**Связан с:** [M1_MODEL_ANIMATION_EVOLUTION_STAGE](../stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md)  
**Исследовательский контур:** [M1_CREATURE_FAMILY_RESEARCH_TASK](M1_CREATURE_FAMILY_RESEARCH_TASK.md)  
**Цель:** производственный процесс с уменьшающейся потребностью в ручных технических правках на одну утверждённую модель. Ни числовые условия перехода между партиями, ни целевые проценты автоматизации сейчас **не утверждаются**.

## 1. Источник и очередность

1. Сначала **существующие, уже импортированные в Unity модели CubeGame**, используемые как исследовательская основа для разработки оригинальных моделей CubeGame. Не повторять S05-конвертацию без доказанной необходимости и не считать физическое наличие файлов в локальном Unity Editor доказательством включения в Windows Player.
2. Переработка не означает простую перекраску, автоматический remesh или механическое копирование Alpha. Конечные ассеты — оригинальные по творческому выражению и пригодные к распространению только после проверки прав.
3. После доказанного конвейера для каждого семейства — **совершенно новые** виды/модели через тот же pipeline.
4. Анимации создаются под новую боевую механику; Alpha-анимации не являются обязательным конечным решением.
5. Для производства модель сначала проходит переработку / создание геометрии; **потом** анатомический анализ готовой формы, fitting skeleton, управляющий rig, skinning, анимацию и Unity. Требования к движениям и технические ограничения скелета могут проектироваться заранее, чтобы не переделывать геометрию.
6. Складывать действующие исходные Alpha assets в публичный GitHub запрещено. Только схемы, индексы, отчёты, метаданные, разрешённые оригинальные ассеты.

## 2. Две входные линии, один общий pipeline

**A / EXISTING_REDRAW:** уже импортированная геометрия Unity → подготовка редактируемой рабочей копии → исследование существа/его формы по отдельному реестру → оригинальная переработка в стиль CubeGame → clean geometry / modular parts → общий процесс C.

**B / NEW_ORIGINAL:** дизайн и назначение нового существа → оригинальные референсы → Blender / процедурная генерация / экспериментальная локальная AI-генерация → художественно утверждённая геометрия → clean geometry / modular parts → общий процесс C.

**C / SHARED:** анатомические ориентиры готовой модели → назначение совместимого `RigArchetype` → подгонка расположения костей → управляющий rig → skinning / rigid attachments → deform/attachment validation → новый или переиспользуемый animation package → retargeting / IK → Unity prefab/EntityRecipe → combat/playmode/visual review → отчёт и APPROVED либо REVIEW.

Отсутствующая или неудобная DCC-модель не является основанием объявлять, что существующий каталог CubeGame «ещё не импортирован»: требуется локальный адаптер или export working copy, а не повторная массовая конвертация.

## 3. Технические этапы и владельцы

| Шаг | Исполнитель | Что выполняет | Что разрабатываем для CubeGame | Выход |
|---|---|---|---|---|
| 0. Family/Identity research | исследователь / исходная Alpha + Unity catalog | Разделяет анатомическое семейство, форму, идентичность и gameplay role | Реестр/схему данных, инструменты сопоставления | `CreatureIdentityRegistry`, `ModelMapping` |
| 1. Geometry acquisition | существующие Unity assets + Blender | Получает редактируемую рабочую копию | Минимальный адаптер, только если нужен | source mesh reference |
| 2A. Existing redesign | Blender / procedural + кандидат локального AI | Создаёт самостоятельную модель из исследованного существующего вида | Стилевые профили, варианты компонентов, неразрушающее хранение правок | approved redesigned mesh |
| 2B. New original geometry | Blender / процедурный инструмент / локальный AI | Генерирует новую геометрию без Alpha source geometry | Правила стиля/модульности, интеграция выбранного генератора | approved original mesh |
| 3. Clean geometry | Blender + Python API | Подготавливает topology, orientation, pivot, modular body parts, deform zones | Детерминированные скрипты и проверку | clean mesh + parts manifest |
| 4. Anatomy landmarking | Blender + правила, AI по результатам пилота | Определяет суставы и выбирает anatomy archetype | Landmark mapping / classification contracts | joint landmarks |
| 5. Skeleton fitting | Blender Armature + Rig Fitter | Подгоняет **существующий согласованный** skeleton contract к готовой модели, не меняя его топологию ради каждого персонажа | Rig Fitter/профили пропорций | fitted skeleton |
| 6. Control rig | Blender, Rigify или другое решение | Производит authoring controls для аниматора | Конфигурация экспортного skeleton и совместимости | control rig + export rig |
| 7. Skinning | Blender automatic weights, rigid parenting | Назначает веса и привязки деталей; проверяет локти, плечи, таз, хваты | Привязка по типам деталей, corrections и тест-позы | animation-ready mesh |
| 8. Animation authoring | Blender, Cascadeur, экспериментальный Motion AI | Делает **новые** ключевые движения/боёвку; базовые анимации принадлежат archetype/оружию/действию | Motion contracts, animation package metadata | animation clips/packages |
| 9. Animation runtime | Unity Mecanim/Avatar + Animation Rigging (или протестированный альтернативный плагин) | Переиспользует и подгоняет клипы, IK, grips, action timing | Небольшой слой mapping `action→package`, интеграция с боем | runnable animated entity |
| 10. Validation | Blender batch + Unity EditMode/PlayMode/Player | Проверяет bindings, деформацию, attachments, бой, производительность | Validator, регрессию, отчёты | report + review status |
| 11. Orchestration | Python subprocess / n8n по результатам пилота; ComfyUI для AI-подзадач | Очереди, статусы, вызов Blender/AI/Unity | Адаптеры + манифесты, без собственного общего scheduler по умолчанию | воспроизводимый запуск |

**Разграничение:** Rigify — готовый генератор управляющего рига, но не универсальный AI-анализатор анатомии и не собственно `Rig Fitter`. Python — язык для Blender automation, операций над геометрией и передачи данных, а не только файловый транспорт. Mecanim — движок анимации, но не источник новых художественных клипов. n8n/ComfyUI — управление/AI-узлы, а не риггер.

### Анимационная индивидуальность

**Совместимый archetype вправе переиспользовать skeleton и базовую locomotion**, но это не означает, что все существа одного семейства выглядят и двигаются одинаково. Реестр идентичности задаёт характерную осанку, скорость, силуэт, повадки, нападение, реакцию на угрозу, вес, signature actions, вариации timing и additive/procedural profile. При реальной необходимости отдельный action clip; не создавать полную библиотеку для каждого creature ID без причины.

## 4. Входные/выходные артефакты

На каждую сущность: исходный Unity asset GUID / путь (либо документированный runtime content reference), Unity mesh IDs/parts; Alpha identity research reference отдельно; anatomy/family metadata; edited mesh; `RigArchetypeVersion`; skeleton bind pose, skin weights, sockets; `AnimationPackageVersion`; collision/combat metadata; Unity prefab; validation report; provenance/license metadata. Все пути только проверенные; не придумывать файл или GUID.

Пример абстрактного manifest (поля не означают уже существующий C# API):

```yaml
entity_id: TBD
unity_model_path: VERIFIED_ONLY
original_alpha_identity: RESEARCH_PENDING
anatomy_family: humanoid
identity_group: orc
behavior_profile: RESEARCH_PENDING
rig_contract: CG_Humanoid_v1
body_profile: heavy
animation_package:
  locomotion: humanoid_base
  combat: humanoid_2h_sword
  identity_additive: TBD
status: RESEARCH_PENDING
```

## 5. Усовершенствование Factory: итерации 1 → 2 → 5 → 10 → 25 → 50 → batch

**Это порядок эксперимента, а НЕ заранее установленный пропускной порог.** Для каждого семейства сначала эталонная модель, затем вторая модель на том же rig/action contract; далее партии 5, 10, 25, 50 и контролируемый batch при последующих решениях.

После каждой партии:
- фиксировать количество и классы ручных правок, человеко-минуты **на один утверждённый ассет**, причины сбоев, повторы и визуальные замечания;
- исправлять повторяющуюся причину **в общих правилах Factory**, а не вручную только на отдельной модели;
- повторно тестировать эталон и уже обработанные модели, чтобы обновление правил не портило старые результаты;
- хранить индивидуальные `manual delta` отдельно от автоматически генерируемой основы и не стирать ручные изменения;
- сохранять человеческую визуальную приёмку, а также различия внешности и поведения каждого существа;
- анализировать тренд на **уменьшение ручных технических правок и времени на один одобренный ассет** между сопоставимыми партиями; сложные и простые существа не смешивать без оговорок.

**Сейчас НЕ назначены:** минимальные проценты автоматизации, нормы минут, допустимое количество правок, строгие pass/fail границы перехода, сроки и обязательное число моделей внутри каждой реальной группы. Такие условия — только предмет отдельного будущего решения.

## 6. Research-first: семейства существ

До масштабирования исследователь создаёт по **всем фактически доступным существам** независимые признаки:

1. `anatomy_family` — техническая форма тела/скелет: humanlike biped, humanoid creature, quadruped, avian, ostrich/head-body avian, floating, insect 6, arachnid 8, serpent, segmented, giant/custom и др.; окончательные семейства подтверждаются каталогом, а не угадываются.
2. `creature_identity` — **кто это конкретно в Alpha** (название, разновидность, чем отличается, характерная внешность) с доказательством из игры/каталога/кода.
3. `behavior_intelligence` — самостоятельная ось: разумность/социальность/агрессивность/повадки, **не** определяет скелет. Например, человек и примитивный человекоподобный монстр могут быть одним анатомическим archetype, но разными identity/behavior groups.
4. `unity_model_mapping` — точный уже импортированный Unity asset: путь, GUID/ID/mesh locator, если доказан; отсутствие доказательства = `UNMAPPED`, не выдумывать.
5. `motion_personality` — индивидуальная осанка, походка, ритм, манера боя, особые действия и потенциальные signature animations; отдельно от переиспользуемой базы.

Цель классификации — не вывести из вида «автоматически тупой/умный», а разделить анатомию, визуальную личность, анимационное поведение и связь с реальным Unity asset.

ТЗ исследователя: [M1_CREATURE_FAMILY_RESEARCH_TASK.md](M1_CREATURE_FAMILY_RESEARCH_TASK.md).

## 7. Статус и границы

Документ — техническое описание для ревью, **не является разрешением запустить M1 production** и не меняет M0/канонические решения. Конкретные локальные AI, версия Blender, источник анимаций, rigging tools, workflow engine, экспортные форматы и финальный C# API ещё тестируются при открытии M1.
