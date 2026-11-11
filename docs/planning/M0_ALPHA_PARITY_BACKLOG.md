# M0 — ALPHA PARITY BACKLOG

**Status:** ACTIVE REFERENCE  
**Purpose:** не потерять системы и детали Cube World Alpha 2013, которые в принятой M0-базе остаются `PARTIAL`, `UNKNOWN`, `PLACEHOLDER_NOT_ALPHA` или реализованы как `CUBEGAME_CONTRACT`.

## Правило проекта — D92

Этот документ является **blocking registry** полного M0 Alpha→Unity port.

Для обязательной одиночной Alpha-системы долг должен получить одно из решений:

- `RESTORE_IN_M0` — восстановить оригинальную логику до закрытия системного блока;
- `UNKNOWN_NEEDS_RESEARCH` — продолжить reverse engineering; блок остаётся открыт;
- `EXPLICIT_PRODUCER_DEVIATION` — только прямое решение продюсера сознательно разрешает отличие от Alpha;
- `OUT_OF_M0_SCOPE` — только функция, уже утверждённая вне одиночного M0 (например, multiplayer M3).

`DEFER_TO_EVOLUTION` больше нельзя использовать как способ закрыть обязательную Alpha-функцию. M1/M2 начинаются после принятого M0.

Наличие рабочего fallback или `CUBEGAME_CONTRACT` не снимает долг. Если он находится на основном runtime-path, системный блок остаётся `PARTIAL`.

Если позже будет создан единый GDD, этот backlog является обязательным источником для раздела legacy/Alpha foundation и должен быть связан с соответствующими системами GDD.

## F1 — WORLD / POPULATION

- Точный поток `rand()` Alpha для генерации населения — `UNKNOWN`.
- Часть gate-условий wild cells — `PARTIAL`.
- 22 места создания вне восстановленной grid-схемы — `PARTIAL`.
- Полные расписания естественных городских NPC — `PARTIAL`.
- Полная экипировка NPC (R-1) — `PARTIAL`.
- Persistence таймера respawn при unload/save использует `CUBEGAME_CONTRACT`.
- Точный вход, геометрия и содержимое dungeon — `PARTIAL/UNKNOWN`.
- Мебель dungeon — `PLACEHOLDER_NOT_ALPHA`.

## F2 — CHARACTER / COMBAT

- OrcFemale `0x0C` имеет отдельный частичный visual path — `PARTIAL`.
- Наплечники, амулет и лампа — неполный visual coverage — `PARTIAL`.
- Точные формулы урона Alpha для части дальнего боя — `PARTIAL`.
- Подруливание boomerang — `PARTIAL`.
- Полный input/runtime для способностей и bomb `0x68` — `PARTIAL`.
- Dodge — `UNKNOWN`.
- Визуал hitscan/луча — `PLACEHOLDER_NOT_ALPHA`.
- Для части существ неизвестны точные class/weapon/action данные; контактный fallback остаётся ограничением до доказательства маршрута Alpha.

## F3 — GAMEPLAY LOOP

- Полное происхождение и эффекты mana cubes / часть классовых эффектов — `PARTIAL`.
- Полный набор рецептов, station requirements и customization crafting — `PARTIAL`.
- Состав предметов сундука использует временную loot table — `PLACEHOLDER_NOT_ALPHA`.
- Размещение городских NPC, полный набор ролей и генерация профессий — `CUBEGAME_CONTRACT/PARTIAL`.
- Полный ассортимент торговцев и buyback — `PARTIAL`.
- Полная генерация миссий и все типы заданий Alpha — `PARTIAL/UNKNOWN`.
- Точные формулы quest rewards — `UNKNOWN`.
- Выбор цели охоты, радиус 32 и награда одним mana cube — `CUBEGAME_CONTRACT`.
- Новые поля кошелька/журнала/календаря в `.session` v1 — рабочий persistence bridge CubeGame, а не доказанный формат Alpha.
- Наземный loot остаётся runtime-only по принятому правилу F2.

## F4 — PRESENTATION

- Каталог 93 WAV и 32 ошибки метаданных — закрыты технически. RIFF-проверка и Unity-декодер покрывают 62 PCM16 и 31 Float32. Отдельный runtime-владелец проигрывает локальные клипы и освобождает их на выходе из мира.
- Звуковая привязка — `PARTIAL`. Подтверждены завершение миссии, подбор, повышение уровня и отдельные UI/крафт-события. Обобщённый hit/block и звук успешного использования зелья — `CUBEGAME_CONTRACT`. Выбор по оружию, действию, виду и контексту смерти, пространственный микс и остальные event mappings — `UNKNOWN_NEEDS_RESEARCH`. [Доказательства F4](../reports/M0_F4_PRESENTATION_REPORT.md#привязки-звука)
- HUD, инвентарь, навыки, услуги, карта и журнал переведены на игровой UI Toolkit. Компоновка, таймауты feedback и 3×3 карта — `CUBEGAME_CONTRACT`. Оригинальные Alpha UI state/виджеты, layout semantics и расширенный обзор мира — `PARTIAL` · `RESTORE_IN_M0`. Сам Plasma renderer можно заменить Unity-реализацией при сохранении поведения и baseline presentation.
- Иконки предметов используют локальный атлас и селектор модели. Совпадения вариантов оружия/брони с атласом покрыты частично. Остальные предметы сохраняют текстовое описание · `PARTIAL` / `UNKNOWN_NEEDS_RESEARCH`.
- Маркеры героя, городов, данжей, целей охоты и индикаторы HUD подключены к runtime. Символы и легенда F4 — `PLACEHOLDER_NOT_ALPHA`. Оригинальные иконки карты и статусов — `UNKNOWN_NEEDS_RESEARCH`.
- Журнал показывает активные и завершённые региональные охоты F3. Перенос генерации миссий, других типов заданий и наград остаётся `PARTIAL/UNKNOWN`, как в F3.
- Художественный редизайн UI — `DEFER_TO_EVOLUTION`.

## D92 — дополнительные blocking gaps, выявленные визуальной/кодовой проверкой

- Streaming/render distance: priority восстановлен частично, реальные Alpha radii/двухуровневый tile path — `PARTIAL`.
- Trees: selection частично исследован, density/placement/RNG всё ещё заменены hash + `MaxObjectsPerZone` — `CUBEGAME_CONTRACT/PARTIAL`.
- Grass/flowers/world-object placement — `UNKNOWN/PARTIAL`.
- World-object IDs ранее смешивались с actor KindId; требуется отдельный Alpha consumer/path — `PARTIAL`.
- Structures/houses: доказаны части размеров, материалы/полная geometry остаются reconstructed — `PARTIAL/PLACEHOLDER_NOT_ALPHA`.
- Creature/NPC AI: полный Alpha state machine не перенесён; текущее поведение упрощено — `PARTIAL`.
- Combat synchronization: gameplay action timing и visual animation могут расходиться; fallback timing `400/600/800` = `PLACEHOLDER_NOT_ALPHA`.
- HUD/menu/inventory/map presentation: текущий Unity UI является техническим baseline, не Alpha parity — `PARTIAL/CUBEGAME_CONTRACT`.
- Full playable Alpha Windows baseline отсутствует — `PARTIAL`.

## Контроль перед финальным M0

Перед `M0 ALPHA FULL UNITY PORT` acceptance реестр просматривается целиком. Любой blocking debt на обязательном runtime-path должен быть закрыт либо иметь прямое `EXPLICIT_PRODUCER_DEVIATION`.

## P0 — BOOT / MENU / CHARACTER / WORLD / INPUT (кандидат 2026-11-11)

Восстановлено и проверено в Windows Player: стартовый экран (`Start Game`/`Options`/`Exit`), независимые списки
персонажей и миров, редактор с моделью, создание мира (seed как `int` без валидации), загрузка мира с позицией
персонажа по миру, цепь Back, системное меню (Options/Start Menu/Exit Game), сохранение/выход/продолжение,
единый input context. Отчёт: [M0_ALPHA_FULL_PORT_P0_REPORT](../reports/M0_ALPHA_FULL_PORT_P0_REPORT.md).

Долги, которые остаются открытыми:

- Опции: точные домены значений `Anti-aliasing`, `Language`, `FPS Limit` в Alpha — `UNKNOWN_NEEDS_RESEARCH`;
  `Render Distance`, `Music Volume`, `Language` сохраняются, но потребителя в порте нет — `RESTORE_IN_M0`
  (streaming P1, audio P8, локализация).
- Маппинг `cameraSpeed`/`cameraSmoothness`/`invertY` на камеру Unity — `CUBEGAME_CONTRACT` (нейтральные значения
  Alpha воспроизводят прежнее поведение).
- Строки списков персонажей и миров — текст без 3D-превью/превью карты — `PARTIAL` · `RESTORE_IN_M0`.
- Вращение модели в редакторе мышью и точная камера превью — `UNKNOWN_NEEDS_RESEARCH`.
- Точка записи текущей позиции персонажа в карту миров внутри Alpha — `UNKNOWN`; в порте запись при сохранении,
  семантика проверена на данных.
- Логотип и «picroma» стартового экрана — закрытые ассеты, не публикуются: заголовок текстовый.
  `OUT_OF_M0_SCOPE` по действующему правилу «Alpha assets не публиковать».
- Сетевой список миров (`Multiplayer Worlds...`) — вне одиночного M0 (multiplayer M3).
- `GameSlotStore` и `StartNewWorld`/`LoadWorld` остаются как совместимость PlayMode-автоматизации; runtime-путь
  стартовых экранов их не использует. Удалить после перевода тестов на `AlphaProfileStore` — `RESTORE_IN_M0`
  (технический долг, не пользовательское поведение).
- Вход в мир блокирует главный поток (наблюдалось ~36 s на сборке P0) — экран `Please wait...` показывается,
  но причина (стриминг/инициализация контента) относится к P1/P5.