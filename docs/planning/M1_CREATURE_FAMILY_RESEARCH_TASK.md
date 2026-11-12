# TASK FOR DEEPSEEK — M1-R0: Alpha creature identity ↔ Unity model family registry

**Статус:** TASK PREPARED / NOT DISPATCHED / RESEARCH ONLY / M1 NOT_AUTHORIZED  
**Source of truth:** `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `AGENTS.md`, [M1 stage](../stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md), [Factory technology](M1_ASSET_ANIMATION_FACTORY_PIPELINE.md).  
**Область работы:** чтение существующего Unity-каталога и локально доступной оригинальной Alpha, исследование и отчёты, **не реализация M1**.  
**Исполнитель (предлагаемый):** DeepSeek.  
**Ключевой принцип:** точная модель из **уже перенесённого каталога Unity** плюс достоверное определение «кто это» **из оригинальной Alpha**.

## 1. Цель

Выполнить исследовательскую инвентаризацию **всех реально доступных существ/персонажей**, разделить по анатомическим и визуально-поведенческим семействам; установить индивидуальную идентичность каждого вида (что за существо в Alpha), назначение, визуальные отличия, особенности поведения/движения; сопоставить с конкретным импортированным Unity asset. Подготовить основание для Style Anchors, RigArchetypes и signature animation profiles. **Не выдавать гипотезы за доказанные Alpha-факты.**

## 2. Четыре независимых классификации

1. **Anatomy / rig candidate** — общий технический скелет / форма тела. Примеры гипотез: `HUMANOID_BIPED`, `HUMANOID_CREATURE` (если действительно другая анатомия), `QUADRUPED`, `AVIAN`, `AVIAN_LONG_LEGS_OSTRICHLIKE`, `AVIAN_HEADBODY`, `FLOATING/SPECTRAL`, `INSECT6`, `ARACHNID8`, `SERPENT/SEGMENTED`, `AMPHIBIOUS`, `CUSTOM`. Не утверждать список до полного аудита.
2. **Alpha identity** — фактическое внутриигровое название, вид/разновидность, роль, форма, уникальные внешние признаки; строго подтверждать источниками.
3. **Intelligence / gameplay / behavior** — разумные культуры (human/orc/elf/dwarf), примитивные человекоподобные, звери, враждебные и мирные, NPC роли, способы перемещения и атаки. Не использовать интеллект как доказательство отдельного скелета: если topology совпадает, это **identity/behavior subfamily**, не обязательный новый rig.
4. **Motion individuality** — shared locomotion base + distinct stance/gait cadence/turning/attack/reaction/signature actions. **Визуальные повторы движений устраняются через вариации и уникальные мотивированные движения, не через дублирование каждой полной animation library.**

## 3. Правила получения доказательств

- Найти существующий каталог **уже импортированных Unity-моделей** в проекте (`Assets`, реестры, generated metadata, runtime asset references, `.scratch` если локально доступен). Записывать реальные `UnityAssetPath`, `GUID`, mesh/model ID и связанный runtime reference **только после проверки**. Если модель есть лишь в игнорируемом локальном каталоге, честно пометить `LOCAL_ONLY`; проверить, что она реально импортируется/появляется в Unity, но не выдавать это за Player packaged.
- Из оригинальной Alpha (реальная игра, локальный каталог, проверенные таблицы и кодовые доказательства) восстановить **кто именно** изображён. Нужны внутриигровое название, отличительные признаки, статус NPC/врага/животного и поведение, если доказано. Не подменять Unity-имя файла догадкой из интернет-списков.
- Сохранять происхождение: `ALPHA_GAME_OBSERVED`, `ALPHA_CODE_STATIC`, `ALPHA_DECOMPILED`, `UNITY_ASSET_VERIFIED`, `INFERRED_REVIEW_REQUIRED`, `UNKNOWN`. Система статусов должна быть совместима с AGENTS.md; выводы, не подтверждённые Alpha, отмечать как `UNKNOWN` / `PARTIAL`.
- Если Unity asset содержит несколько creatures или один creature использует несколько mesh частей, записывать связь многие-ко-многим и не считать геометрические ресурсы количеством видов.
- **Никаких Alpha assets/скриншотов/конвертированной геометрии в публичные коммиты**; только текстовые metadata/index/отчёт, где публикация разрешена. Не редактировать Unity сцену/системы, M0 canonical, не менять имена или структуру ассетов.
- Наличие исходного контента на диске не доказывает законность его публикации или выпуска: все конечные M1 assets должны быть оригинальными и пройти правовую проверку.

## 4. Реестр: обязательные поля одной записи

| Поле | Описание |
|---|---|
| `entry_id` | стабильный ID строки реестра |
| `unity_model_path` | конкретный **проверенный** путь в Unity, или `UNMAPPED` |
| `unity_guid / mesh_id / runtime_ref` | реальные локаторы, если подтверждены |
| `mapping_status` | `VERIFIED` / `LOCAL_ONLY` / `PARTIAL` / `UNMAPPED` |
| `alpha_creature_name` | внутриигровое название по доказательству; `UNKNOWN` если нет |
| `alpha_species_variant` | вид/подвид, если подтверждён |
| `alpha_identity_evidence` | источник, таблица, наблюдение, кодовое основание с locator |
| `anatomy_family` | техническая анатомия |
| `identity_family` | понятная визуальная группа (orc, elf, dwarf, human, primitive humanoid, long-leg avian и т.д.) |
| `intelligence_behavior_group` | отдельный признак, а не основание для выбора skeleton |
| `locomotion_evidence` | подтверждённое движение; `UNKNOWN` при отсутствии |
| `combat_behavior_evidence` | подтверждённый тип атаки; `UNKNOWN` при отсутствии |
| `visual_features` | отличительные анатомические и художественные признаки |
| `motion_personality_proposal` | **предложение** индивидуальности: осанка, походка, реакция, атака и т.п. |
| `rig_archetype_candidate` | **гипотеза**, пока не проверено в M1 |
| `signature_animation_proposal` | необходимые уникальные действия, если базовые недостаточны |
| `confidence / unknowns` | подтверждение и пробелы |
| `source_provenance` | `ALPHA` / `UNITY` / `INFERRED` для каждого вывода |

Пример **шаблона**, НЕ реальная запись:

```yaml
entry_id: EXAMPLE_ONLY
unity_model_path: UNMAPPED
alpha_creature_name: UNKNOWN
anatomy_family: HUMANOID_BIPED
identity_family: PRIMITIVE_HUMANOID
intelligence_behavior_group: RESEARCH_PENDING
rig_archetype_candidate: CG_Humanoid_v1 # proposal, not fact
motion_personality_proposal: UNKNOWN
mapping_status: UNMAPPED
confidence: UNKNOWN
```

## 5. Формат результата

1. `docs/research/M1_ALPHA_CREATURE_IDENTITY_REGISTRY.csv` — строка на подтверждённую связь вида / модели, отдельные поля и источники; или JSONL если many-to-many удобнее.
2. `docs/research/M1_CREATURE_FAMILY_MATRIX.md` — анатомические семейства и подсемейства; объяснение, где общий скелет, а где лишь различия разума/визуала/действий; не смешивать похожих на страуса и обычных птиц без анатомического обоснования.
3. `docs/reports/M1_R0_CREATURE_FAMILY_RESEARCH_REPORT.md` — объём каталога, количество сматченных/неизвестных, примеры доказательств, coverage по видам/моделям, неразрешённые конфликты, найденные дубли, подозрительные mapping и статус `PARTIAL` при пробелах.
4. Список **минимального представительского набора** для дальнейших пилотов: Human Standard, Orc Heavy, примитивный человекоподобный, quadruped, обычная птица, необычная длинноногая птица/HeadBody (если существуют), floating, insect/arachnid. Подбор только из реально доказанных доступных моделей.
5. Предложения по individuality: уникальная осанка, походка, темп, манера атаки и signature actions, но **не** производство анимаций и не утверждение новых механик.

## 6. Порядок работ DeepSeek

- Прочитать обязательные проектные документы и проверить локальный каталог. Составить список **всех реально найденных** creature IDs, actor names и Unity model references.
- Выстроить кандидатов анатомических семейств на основе фактических частей тела. Отдельно определить identity/behavior группы: human/orc/elf/dwarf; creature-humanoid; птицы; длинноногие птицеподобные; четырёхлапые; летающие/безногие; шестиногие; паукообразные; другие.
- Для каждой модели восстановить identity **по Alpha**. При недоказуемости оставить `UNKNOWN` без фантазирования.
- Составить registry + family matrix + report, проверить ссылки/дубли/coverage; **не** менять существующие Unity assets и не начинать M1 production.
- Перед commit/push соблюдать AGENTS.md и актуальную координацию общей папки/Unity slot. Публиковать только безопасные текстовые результаты в отдельной review ветке, если разрешено; максимум `READY_FOR_REVIEW`, не `ACCEPTED`.

## 7. Не делать

- Не писать новый конвертер и не копировать тысячи моделей заново.
- Не создавать риги, анимации, визуальную переработку или Production Factory на исследовательском контуре.
- Не придумывать собственные Alpha-названия, модели, доказательства и не подменять анатомию интеллектом.
- Не обещать, что все creature IDs совпадают 1:1 с 3D mesh files.
- Не устанавливать пороги по минутам/количеству исправлений и критерии допуска партий — это отдельно решается позже.

## 8. Критерий выполнения задания

Исследователь представляет **прослеживаемое** соответствие `Alpha identity ↔ фактически доступная Unity model ↔ anatomy family ↔ motion identity proposal`, полный список неизвестного и воспроизводимое описание метода. Массовое число «строк» без доказательных ссылок не считается выполнением.
