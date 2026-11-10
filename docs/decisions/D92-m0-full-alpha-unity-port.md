# D92 — M0: полный перенос Cube World Alpha 2013 в Unity

**Дата:** 2026-11-11  
**Решение продюсера:** текущий M0 полностью перерабатывается ради исходной цели — перенести Cube World Alpha 2013 как цельную игру на Unity, а не собрать набор похожих подсистем.  
**Статус:** APPROVED PLAN. Это не ACCEPTED результата M0.

## 1. Что изменяется

D92 заменяет D90 как активную схему исполнения M0. D90, D66 и старые F1–F4 документы остаются историей и доказательной базой, но больше не определяют критерий «система перенесена».

Принятые ранее F2/F3 и технически завершённые F1/F4 считаются **инженерными checkpoint'ами**: код, исследования, ассеты, тесты и рабочие куски сохраняются. Их старый статус не означает, что соответствующая система Cube World Alpha уже воспроизведена целиком.

## 2. Финальная цель M0

M0 заканчивается только когда одиночная Cube World Alpha 2013 работает в Unity как цельная игра:

`launch → main menu → character → world → movement → streaming → environment → NPC/creatures → combat → loot/items → city/services → dungeon/boss → quests/progression → HUD/inventory/map → audio/VFX → save/exit/load/continue`.

Мультиплеер остаётся отдельным будущим направлением M3 по ранее утверждённой дорожной карте.

После M0 разрешается эволюционная переработка CubeGame (M1/M2). До этого основной runtime не должен подменять Alpha придуманными механиками только ради «рабочего стенда».

## 3. Главный метод

Для каждой системы обязательна цепочка:

`FIND ORIGINAL CODE → TRACE CALL GRAPH → IDENTIFY DATA/STATE/RNG → WRITE BEHAVIOR SPEC → IMPLEMENT UNITY EQUIVALENT → CONNECT FULL RUNTIME → WINDOWS PLAYER → COMPARE WITH ORIGINAL → PRODUCER REVIEW`.

Скриншот оригинала — проверка результата, но источник правил — код и данные Alpha.

## 4. Что больше НЕ является доказательством переноса

Само по себе не закрывает систему:

- наличие класса;
- наличие модели или анимации;
- `resolve=PASS`;
- успешный unit/EditMode/PlayMode тест;
- наличие кнопки/UI-панели;
- существование CombatSystem/AI/vegetation planner;
- `CUBEGAME_CONTRACT`, fallback или placeholder, который просто делает стенд играбельным.

Пример: `AI движется + анимация атаки есть + урон проходит` не означает перенос боя, если выбор действия, animation state, hit window и damage не являются одной восстановленной Alpha-цепочкой.

## 5. Правило UNKNOWN / PARTIAL

Если правило Alpha необходимо для основного пользовательского пути и оно `UNKNOWN` или `PARTIAL`, система остаётся незакрытой.

Временный `CUBEGAME_CONTRACT` допустим только как явно маркированный исследовательский мост во время работы. Он **блокирует финальный PASS соответствующей системы**, пока не заменён доказанной Alpha-логикой либо отдельным прямым решением продюсера об осознанном отклонении.

Нельзя использовать `DEFER_TO_EVOLUTION` для того, чтобы пропустить обязательную Alpha-функцию и всё равно закрыть M0.

## 6. Работа системами, а не галочками

M0 делится на целостные системные контуры:

1. Boot / menu / character / world / input state.
2. World generation / scales / streaming / rendering distance.
3. Environment: trees / grass / flowers / world objects / structures / cities / dungeons.
4. Entity population / NPC / creature spawning / schedules / AI / movement.
5. Action & combat: decision → action state → animation → hit/projectile → damage → recovery/death.
6. Character / appearance / equipment / items / loot / crafting / progression.
7. UI presentation: HUD / inventory / skills / map / journal / services.
8. Audio / VFX / time-of-day and other Alpha presentation systems.
9. Persistence: world/player/NPC state, save/exit/load/continue.
10. Final full-game Windows parity run.

Один контур не закрывается «по частям», если пользовательский runtime остаётся собран из несвязанных заменителей.

## 7. Запрет на лишний tooling

Во время M0 агент не должен писать новые плагины, editor tools, frameworks и общие генераторы ради отчёта, если задачу можно решить прямым исследованием и игровым runtime-кодом.

Одноразовый диагностический скрипт допустим только когда он нужен для подтверждения конкретного факта.

## 8. Player-first acceptance

Editor недостаточен. Каждый системный контур должен быть проверен в Windows Player.

Обязательны:
- конкретный source SHA;
- чистая сборка;
- AlphaContent;
- runtime evidence;
- сравнение с оригиналом;
- ручная проверка продюсером для видимых/игровых систем.

Тесты защищают от регрессий, но не заменяют игровую приёмку.

## 9. Постоянная пользовательская сборка

Проект ведёт одну актуальную пользовательскую игру. Постоянный Desktop launcher не меняется; после успешной сборки/проверки новая версия заменяет установленную live-версию. Старые пользовательские builds не являются отдельными продуктами.

## 10. Exit criterion M0

M0 получает максимум `READY_FOR_PRODUCER_ACCEPTANCE`, когда:

- все обязательные одиночные Alpha-системы из stage matrix имеют `PASS`;
- в основных runtime-path нет `PLACEHOLDER_NOT_ALPHA` и непринятых `CUBEGAME_CONTRACT`;
- меню/мир/AI/бой/UI связаны в одну игру;
- animation/hit/damage синхронизированы через восстановленное action state;
- world objects не смешиваются с creature ID-space;
- environment и streaming формируются Alpha-правилами;
- полный Windows сценарий проходит от запуска до save/load;
- продюсер лично подтверждает, что это уже перенос Alpha в Unity, а не технический стенд.


