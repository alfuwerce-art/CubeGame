# D90 — завершение M0 пятью практическими этапами

**Дата:** 2026-11-10  
**Решение продюсера:** после завершения D1/D2 review-fix и независимого аудита D3–D6 перейти от старого распределения D71–D89 к пяти последовательным этапам завершения M0.  
**Статус:** APPROVED PLAN. Это не ACCEPTED результата M0.

## Что superseded

D90 заменяет как **активную схему исполнения** старые распределения D71, D85, D87, D88 и D89. Эти решения и их отчёты остаются исторической доказательной базой, но больше не определяют текущие роли агентов, рабочие ветки или очередность.

Каноническая интеграционная ветка: `integration/m0-all`.

## Входное состояние

- Codex D3–D6 independent audit опубликован: `762ac5d`, `READY_FOR_REVIEW`.
- Claude D1/D2 spawn/patrol review-fix: код `47c5b1a`, затем сведение с Codex; общий HEAD до D90-cleanup — `fce38e0`.
- D1: прежняя «spawn table» оказалась ассортиментом торговцев, а не таблицей существ. Восстановлены `cube::Spawn`, 35 мест создания в 7 функциях, дикая сетка 3×3, lifecycle/respawn и D2 NPC schedule/patrol. Runtime-подключение в `GameBootstrap` ещё отсутствует.
- D3–D6 audit: города/данжи не подключены, loot visual имеет двух владельцев, projectiles не имеют runtime pipeline, audio playback отсутствует, WAV metadata parser ошибается на 32 записях.
- Unity после сведения: EditMode 639 total / 637 passed / 0 failed / 2 skipped / error CS 0. Полный PlayMode после merge не повторён; до merge Claude получил 57 total / 54 passed / 2 failed / 1 skipped. Два fail — прежний world-collider defect.

## Пять этапов

### M0-F1 — WORLD FIX
1. Подключить `AlphaRecoveredPopulation` и восстановленный D1/D2 runtime path.
2. Исправить stale collider после voxel edit/save/load.
3. Подключить города, данжи, сундуки и единственный world-content route.
4. Закрыть стабильные ID, unload/reload, spawn lifecycle и необходимые persistence seams.
5. Полный EditMode/PlayMode для мира.

### M0-F2 — CHARACTER & COMBAT
1. Реальная внешность героя: race/sex/appearance.
2. Все требуемые визуальные слоты экипировки и один владелец ground loot.
3. Floating-origin для loot/visuals и lifecycle runtime mesh/material.
4. Движение Alpha, оставшиеся movement modes.
5. Полный combat path: melee/ranged/projectile/collision/hit, способности и доказанные VFX.

### M0-F3 — GAMEPLAY LOOP
1. XP/level/specialization/skills.
2. Полный item/equipment loop, wallet, chests, crafting по доказанному.
3. NPC interaction/services/trade.
4. Quest loop и persistence.
5. Проверка save/load без дублей и потери состояния.

### M0-F4 — PRESENTATION
1. Исправить RIFF/WAV parser и подключить runtime audio.
2. HUD, inventory, skills, map, quest journal вместо временного IMGUI.
3. Карта/feedback/иконки и оставшиеся presentation seams.
4. Адресные визуальные проверки.

### M0-F5 — FINAL
Сквозной Windows build:
`new world → character → travel → combat → loot → city → trade → dungeon → boss → quest → save → exit → load → continue`.

Обязательны полные EditMode/PlayMode, отсутствие критических регрессий, отчёт по PARTIAL/UNKNOWN/PLACEHOLDER и личная приёмка продюсером.

## Правила

- Не присваивать `ACCEPTED` без решения продюсера.
- GitHub — источник истины.
- Один Unity slot.
- Не редактировать одни и те же файлы параллельно.
- Исторические документы не удалять: они доказательная база. Старые активные схемы считать superseded.
- Исходные/конвертированные Alpha assets не публиковать.
