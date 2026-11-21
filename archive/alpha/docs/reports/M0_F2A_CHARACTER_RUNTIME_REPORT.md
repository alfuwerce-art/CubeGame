# M0-F2A CHARACTER RUNTIME

10 ноября 2026 · `claude/m0-f2a-character-runtime` · база `748d6505ef8620f4b6b7c4474f72c006b3c6dcc0` (M0-F1 review)

**`READY_FOR_REVIEW`** — не `ACCEPTED`.

## 0. Состояние GitHub на входе

| Ветка | HEAD | Содержит M0-F1 `748d650`? |
|---|---|---|
| `origin/integration/m0-all` | `1c6560c` | **нет** — F1 ещё не сведён в canonical |
| `origin/codex/m0-f1-world-runtime` | `748d650` | да |

`1c6560c` — предок `748d650`, поэтому F2A начат **от `748d650`**, а не от старой canonical базы: без F1 тестовая база (EditMode 664/0/2, PlayMode 60/60) не воспроизводится. После приёмки F1 ветку F2A можно сводить в `integration/m0-all` без rebase (F1 — её предок). `main` не трогался.

## 1. Состояние до (аудит)

Колонки: **CODE** — код существует; **RUNTIME** — подключён в игровой сцене; **TESTED** — есть тест; **ALPHA** — происхождение.

| Система | CODE | RUNTIME | TESTED | ALPHA PROVENANCE | Дефект до F2A |
|---|---|---|---|---|---|
| Создание персонажа (редактор C1) | да | да (IMGUI) | да | `ALPHA_CODE_STATIC` (C1) | — |
| race / sex → вид | да | **нет** | данные | `ALPHA_CODE_STATIC` | sink строил всегда `HumanMale 0x02` |
| face / haircut | индексы | **нет** | данные | диапазоны `ALPHA_CODE_STATIC`, модель по индексу не разрешалась | отсутствовал шаг registry index → модель |
| hair colour | да (`AlphaAppearance`) | **нет** для героя | лаб. тест | D61 `ALPHA_CODE_STATIC` | цвет из сейва не применялся |
| skin colour | — | — | — | в редакторе Alpha **нет** такого поля | см. §5 |
| body/model selection | лаб. builder | только 0x02 | лаб. тест | S05 `EMULATED_ALPHA_CODE` | у рас, кроме 0x02, нет трека оружия и боевых клипов |
| armour slots (грудь/кисти/стопы) | да | частично | да | `EMULATED_ALPHA_CODE` (item_layer) | модель по умолчанию, материал игнорировался |
| weapon visual | да | частично | да | `EMULATED_ALPHA_CODE` | модель «по подтипу»: Greatmace/Greataxe пустые → `MODEL_NOT_STAGED` у воина, оставался захваченный двуручник |
| weapon state (рука/спина) | да (клипы) | да | частично | `EMULATED_ALPHA_CODE` | — |
| dual wield / two-handed / shield / bow / staff / wand | слои есть | частично | частично | `EMULATED_ALPHA_CODE` якоря | материал-зависимые модели не выбирались |
| ground loot visual | **два** владельца | оба | частично | часть моделей | дубль, утечка `new Material` на предмет |
| floating origin (character/loot) | — | герой да, лут **нет** | нет | — | лут оставался на старых сцен-координатах после `Rebase` |
| save/load внешности и экипировки | да | данные да, визуал **нет** | данные | — | визуал после загрузки не зависел от сохранённой внешности |

Дополнительно найдено: перестройка героя не освобождала сгенерированные меши; неудачный `AlphaActorView` оставался под капсулой; накопительный сдвиг трупа героя (`+=` каждый кадр); рост героя пересчитывался при каждой смене оружия (меч за спиной менял `ModelScale` для всех существ).

## 2. Что изменено — один runtime-путь

```
HeroState (Appearance + Loadout, сейв без изменений)
  → GameBootstrap публикует EntitySpawned.Appearance и EquipmentVisualChanged (+ material/rarity/seed)
  → GameVisualSink хранит внешность и предметы героя (единственный владелец визуала героя)
  → HeroVisualComposer.Compose  — чистая детерминированная HeroVisualSpec (Signature)
  → HeroVisualSpec.ToRequest    — файлы моделей локального кэша
  → AlphaPlayerVisualBuilder    — риг + подмена частей + слои предметов
  → Unity: перестройка только при смене Signature; старый view и его меши уничтожаются
```

- **Body/head/hair/armour/weapon** создаёт один builder внутри одного view; обновление после экипировки — только через `GameVisualSink.FlushHero()` (вызов из `OnFrame` и перед выдачей view). Несколько событий одного кадра → одна перестройка.
- **Ground loot** — единственный владелец `GameVisualSink`. Путь `GameBootstrap.OnWorldItemDropped` (кубик) удалён. Один предмет → ровно один объект: модель по селектору или помеченный `GroundItem_PLACEHOLDER`, если модель не застейджена. Позиция пересчитывается из `WorldPosition` каждый кадр → нет дрейфа после origin shift. Меш уничтожается вместе с предметом; материал общий (`AlphaVisualResources.Base`), кубик-заглушка — один общий материал.
- **Persistence**: формат сейва не менялся. Внешность и Core-экипировка (`rarity/seed/modifier`) уже сохранялись (`GameSlotStore`, `CharacterSaveMapper`); визуал теперь детерминированно восстанавливается из них.

## 3. Alpha provenance

| Решение | Источник | Статус |
|---|---|---|
| раса + пол → вид | `CharCreation_refresh_model` 0x42C080 (C1) | `ALPHA_CODE_STATIC` |
| лицо/причёска → registry index | таблица переходов 0x42C41C (C1) | `ALPHA_CODE_STATIC` |
| registry index → модель (183 записи) и load flag (маркеры волос прозрачны) | вызовы загрузчика реестра в `asset_catalog` (D64) | `ALPHA_CODE_STATIC` |
| цвет волос = `hair_rgb/255`, только блок `hair` | D61 | `ALPHA_CODE_STATIC` |
| общий гуманоидный скелет: виды с равными `flags/offset_*/scale_body/hand/foot` рисуются позами 0x02 | блок внешности существа `+0x24…+0xA0` (`alpha_static`), сверка клипов: тело/ступни совпадают побитово, голова отличается только `scale_head` | `ALPHA_CODE_STATIC` константы + `EMULATED_ALPHA_CODE` позы |
| масштаб головы/волос `scale_head(вид)/scale_head(0x02)` (1.0 / 0.891 / 0.792) | `appearance+0x24`, `mat4_scaleAxes` в `Creature_drawModel` | `ALPHA_CODE_STATIC` |
| масштаб оружия `scale_weapon` (гоблины/дварфы 1.2 против 0.95) | `appearance+0x38` | `ALPHA_CODE_STATIC` |
| тело/кисти/ступни расы | модели видов из S05 акторов | `EMULATED_ALPHA_CODE` |
| модель предмета по type/subtype/material/rarity/seed (686 правил, 1565 имён) | исполненный `GameController_item_icon_id` 0x4EC400, 310 421 прогон, 0 ошибок; 0 неоднозначных ячеек | `EMULATED_ALPHA_CODE` |
| якоря оружия/левой руки, рука/спина по клипу | item_layer, S05 клипы | `EMULATED_ALPHA_CODE` (как до F2A) |
| кольца не рисуются | `drawModel` читает `+0xBC0/+0xCD8`, блока нет | `EMULATED_ALPHA_CODE` |

Таблицы сгенерированы `tools/alpha_content/gen_character_models.py` в `AlphaCharacterModels.g.cs` (только имена и числа). Модели (`.cubdec`) стейджатся тем же скриптом (`--stage`) в git-игнорируемый `.scratch/content/{appearance,items}`.

## 4. CUBEGAME_CONTRACT

- Поза предмета на земле: центр на точке дропа, низ на земле, поворот 45°, масштаб = фактор героя. Вращение/покачивание Alpha не восстановлены.
- `ModelScale` (воксель Alpha → блок) подгоняется один раз на мир по телу героя без оружия и левой руки (`PLACEHOLDER_NOT_ALPHA`, как и раньше по смыслу). Экипировка больше не меняет масштаб.
- Отрицательный seed трактуется как беззнаковый 32-бит (все seed CubeGame ≥ 0).
- Заглушка `GroundItem_PLACEHOLDER` для незастейдженной модели (`PLACEHOLDER_NOT_ALPHA`).

## 5. PARTIAL / UNKNOWN

| Пункт | Статус | Причина |
|---|---|---|
| OrcFemale 0x0C | `PARTIAL` | `offset_head (0,1.5,4)` и `scale_body 0.95` отличаются от 0x02. Сдвиг головы в клипах не сводится к постоянному смещению. Герой строится собственным актором 0x0C: внешность верна, трека оружия и боевых клипов нет (`WEAPON_TRACK_MISSING`). |
| Относительный рост видов (`size` 0.8 / 0.96 / 1.04) | `PARTIAL` | В Unity размер вида не применяется ни к одному существу. Герой подгоняется к контроллеру 1.8. Решение должно быть общим для героя и NPC. |
| Наплечники, амулет, лампа, планер, питомец | `PARTIAL` | Блоков `shoulder_item/neck_item/lamp_item/special_item/pet_item` нет в подготовленном гуманоидном риге; слот сообщается `SLOT_NO_RIG_BLOCK`. Дополнительные слоты (шея/лампа/транспорт) не публикуются как визуальные события. |
| Левая рука «за спиной» (`offhand_a`) | `PARTIAL` | Слой рисует левую руку только в руке; блок за спиной не перенесён. |
| Двуручное + левая рука одновременно | `UNKNOWN` | Правило экипировки Alpha не установлено; визуал рисует ровно экипированное. Core не запрещает комбинацию (gameplay, не F2A). |
| Цвет кожи | `UNKNOWN` | Редактор Alpha (0x428E40) не имеет такого поля; `tintMultipliesCreatureColour` у частей — источник цвета существа не восстановлен. Не реализовано. |
| Модели, отсутствующие в `data1.db` (6 имён, напр. `bone-greataxe.cub`, `wood-mace05.cub`) | `PARTIAL` | Alpha ссылается на них, но файла нет; визуал сообщает `MODEL_FILE_MISSING`, лут получает заглушку. |
| Лут на земле не сохраняется | существующее правило | `WorldItems` — runtime-only (`PLACEHOLDER_NOT_ALPHA`). Сохранение лута изменило бы схему сейва — не делалось. |

## 6. Изменённые файлы

| Файл | Что |
|---|---|
| `Assets/CubeGame/Presentation/Visual/HeroVisual.cs` (новый) | `HeroVisualItem`, `HeroWeaponGrip`, `HeroVisualSpec` (Signature, ToRequest), `HeroVisualComposer` |
| `Assets/CubeGame/Content/AlphaCharacterModels.cs` (новый) | поиск head/hair по registry index, исполненный селектор моделей предметов |
| `Assets/CubeGame/Content/Generated/AlphaCharacterModels.g.cs` (новый, сгенерирован) | 183 модели лиц/причёсок, 16 блоков внешности видов, 686 правил селектора, 1565 имён |
| `tools/alpha_content/gen_character_models.py` (новый) | генератор таблицы + локальный staging `.cubdec` (в Git не попадает) |
| `Assets/CubeGame/AlphaModels/Runtime/AlphaPartSwap.cs` (новый) | подмена модели части рига (голова/волосы/тело/кисти/ступни) с масштабом вокруг якоря |
| `Assets/CubeGame/AlphaModels/Runtime/AlphaRig.cs` | `partScales` / `SetPartScale` / `RefreshAnchor`; масштаб части сохраняется при смене руки/спины |
| `Assets/CubeGame/AlphaModels/Runtime/AlphaWeaponLayer.cs` | `Clear` (пустая рука убирает захваченное оружие рига), `HasWeapon` |
| `Assets/CubeGame/Presentation/Visual/AlphaPlayerVisual.cs` | `RigKind`, `RacePartsKind`, модели головы/волос, `HeadScale`, `WeaponScale`, `HideCapturedWeaponWhenEmpty`, файлы брони |
| `Assets/CubeGame/Presentation/Gameplay/GameVisualSink.cs` | единственный владелец героя и лута; `FlushHero`; позиция лута каждый кадр; уничтожение мешей; фикс накопления сдвига трупа героя; масштаб без оружия; удалён `WeaponModelFile` |
| `Assets/CubeGame/Presentation/Gameplay/GameBootstrap.cs` | удалён второй визуал лута; события экипировки несут material/rarity/seed |
| `Assets/CubeGame/Features/Contracts/GameplayEvents.cs` | `Material/Rarity/Seed` в `EquipmentVisualChanged` и `WorldItemDropped` (контракт событий, не сейв) |
| `Assets/CubeGame/Features/Items/WorldItems.cs` | публикует байты предмета при дропе (правила лута не менялись) |
| `Assets/CubeGame/Presentation/Visual/HeroLoadout.cs` | пометка: больше не runtime-путь |
| `Assets/CubeGame/Tests/Creatures/EditMode/HeroVisualComposerTests.cs` (новый) | 13 EditMode тестов |
| `Assets/CubeGame/Tests/FullM0/PlayMode/FullM0CharacterRuntimePlayModeTests.cs` (новый) | 2 PlayMode сценария |
| `Assets/CubeGame/Tests/FullM0/PlayMode/FullM0CombatPlayModeTests.cs` | рост героя меряется без блока оружия (намеренное изменение: экипировка не меняет масштаб) |
| `Assets/CubeGame/Tests/FullM0/PlayMode/CubeGame.Tests.FullM0.PlayMode.asmdef` | ссылки на `Presentation.Visual`, `Content` |
| `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, этот отчёт | статус этапа |

Alpha assets не коммитились: `.cubdec` лежат в `.scratch/content/` (git-ignored).

## 7. EditMode

Unity 6000.3.27f2, полный прогон `-runTests -testPlatform EditMode` (по XML):

| Прогон | total | passed | failed | skipped | error CS |
|---|---|---|---|---|---|
| База F1 | 666 | 664 | 0 | 2 | 0 |
| F2A, `AlphaOutput/f2a_editmode2.xml` | **679** | **677** | **0** | 2 | **0** |

Skip — прежние 2 `FullM0ZoneOracleTests` (локальный дамп оракула). Новые 13: маппинг внешности (все 16 видов × все лица × все причёски), цвет волос, общий риг/OrcFemale, масштабы вида, селектор (материал/редкость/seed/пустой слот/домен), классы хвата, стартовые наборы 4 классов без проблем, броня+щит, слоты без блока и кольца, пустые руки, детерминизм (порядок событий, нормализация), модель лута тем же селектором.

## 8. PlayMode

| Прогон | total | passed | failed | error CS |
|---|---|---|---|---|
| База F1 | 60 | 60 | 0 | 0 |
| F2A первый, `f2a_playmode.xml` | 62 | 60 | 2 | 0 |
| F2A итог, `AlphaOutput/f2a_playmode2.xml` | **62** | **62** | **0** | **0** |

Первые 2 падения — утверждения тестов, не рантайм: (1) мой тест ждал имя модели в имени меша оружия (слой называет меш `weapon_<subtype>`); (2) `FullM0CombatPlayModeTests` мерил рост героя вместе с двуручником за спиной (2.23 против 1.8). Теперь рост подгоняется по телу без оружия — тест меряет тело.

`CharacterVisualSurvivesEquipmentOriginShiftAndSaveLoad` (реальный `GameBootstrap`): создание дварфийки (лицо 3, причёска 2, синие волосы, класс Rogue) → в риге `dwarf-head-f03`, `dwarf-hair-f02`, тинт волос, масштаб головы 0.891 и оружия 1.263 → два кинжала (dual wield, одна левая рука) → смена на деревянную великую булаву: ровно одна перестройка, один view → нагрудник надет/снят, левая рука снята → дроп меча seed 3: один объект `iron-sword1-random3` → телепорт на 3000 блоков (смена origin): лут и герой на мировых позициях → подбор: предмет и визуал исчезают → save → exit → load: внешность, `Signature`, экипировка, меши головы/оружия совпадают, один view, лута нет. Итог в логе: `kind=0a rig=02 head=dwarf-head-f03.cub hair=dwarf-hair-f02.cub … main=wood-greatmace02.cub#17 … builds 2 problems []`.

`EveryRaceAndSexBuildsItsOwnHeroVisual`: 16 видов через тот же builder — 15 на общем риге без проблем, с мечом и своими лицом/причёской (15 разных голов); 0x0C — собственный актор, `SPECIES_POSE_DIFFERS`.

Ограничение проверки: визуальный осмотр человеком не проводился — проверены меши, тинты, масштабы и иерархия.

## 9. Handoff для Codex / F2B

- **Projectiles/ranged** не трогались. Для лука/арбалета/посоха/жезла визуальный класс доступен как `HeroVisualSpec.WeaponGrip` (`Bow/Crossbow/Staff/Wand`) — источник для выбора ranged-ветки вместо melee fallback.
- Подключить общий масштаб видов (`size`) одновременно к герою и NPC (сейчас ни к кому).
- Блоки `shoulder/neck/lamp/special/pet` и `offhand_a` (за спиной) требуют расширения гуманоидного рига из S05 захвата.
- OrcFemale 0x0C: нужен захват/эмуляция полного набора действий с треком оружия для собственного актора.
- Правило «двуручное оружие освобождает левую руку» — проверка Alpha и gameplay (F3 equipment loop).
