# M0 — ALPHA PARITY BACKLOG

**Status:** ACTIVE REFERENCE  
**Purpose:** не потерять системы и детали Cube World Alpha 2013, которые в принятой M0-базе остаются `PARTIAL`, `UNKNOWN`, `PLACEHOLDER_NOT_ALPHA` или реализованы как `CUBEGAME_CONTRACT`.

## Правило проекта

M0 используется как **рабочий системный фундамент**, который затем будет эволюционно перерабатываться в CubeGame. Принятие этапа не означает 1:1-паритет со всеми деталями Alpha.

Ни один пункт из этого документа нельзя молча удалить. При дальнейшей работе каждый долг получает одно из решений:

- `RESTORE_BEFORE_F5` — восстановить до финальной приёмки M0;
- `DEFER_TO_EVOLUTION` — оставить рабочий контракт и переработать на M1/M2;
- `SUPERSEDED_BY_APPROVED_REDESIGN` — исходное правило Alpha сознательно заменено утверждённым дизайном CubeGame;
- `UNKNOWN_NEEDS_RESEARCH` — требуется дополнительное исследование оригинала.

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

## F4 — PRESENTATION, ЕЩЁ НЕ ЗАКРЫТО

- Каталог 93 WAV есть, но runtime playback должен быть завершён.
- RIFF/WAV metadata parser ранее имел ошибки на 32 файлах — требуется исправление и полная проверка.
- HUD, inventory, skills, map и quest journal ещё должны перейти с временного/debug представления на игровой UI.
- Иконки, feedback и event mapping аудио требуют runtime-интеграции и проверки.
- Художественный редизайн UI не относится к M0-F4; это будущая эволюция интерфейса.

## Контроль перед F5

Перед M0-F5 этот документ просматривается целиком. Для каждого остающегося пункта должно быть явно записано одно из четырёх решений: восстановить, отложить в эволюцию, заменить утверждённым дизайном или оставить на дополнительное исследование.
