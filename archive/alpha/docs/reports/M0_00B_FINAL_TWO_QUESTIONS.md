# M0-00B FINAL — два вопроса для Claude M0-00C

**Дата:** 2026-11-09. **Статус:** READY_FOR_REVIEW, не ACCEPTED.
**Источник:** результаты статической трассировки, предоставленные DeepSeek, локальная папка D:\Projects\CubeGame-deepseek\.scratch\research\m0_00b_final; сокращённый SHA256 Cube.exe 84a7a132…30bf.
**Проверки:** исполнитель сообщил VERIFY_TWO_QUESTIONS_RESULT=PASS checks=43 failed=0. В рамках этого коммита исходный скрипт из .scratch не получен и не запускался; эти 43 проверки не утверждаются как повторно воспроизведённые.

## Q1. SUBTYPE_0 = VALID_WEAPON

**Доказанность DIRECT** для генерации NPC и боевого использования, **PARTIAL** для торговли и получения игроком.

- World_generateNpcSpawnList @ 0x5ca0a0 создаёт type=3, subtype=rand()%3, в том числе subtype 0, и передаёт 1–3 предмета через GameController_addItemToInventory в NPC stock.
- item_weaponDamage @ 0x4c7f60: type!=3 даёт нулевой урон, а subtype 0 попадает в обычную ветку с множителем ×4.
- weapon_isSpecialRunePtr @ 0x444820: subtype 0 не является special/двуручным/дальнобойным.
- combat_selectAttackVariant @ 0x4452a0: обычные атаки 3/4.
- Модель iron-sword1.cub, индекс 899; для subtype 0 нет отдельного case в item_icon_id, но модель выбирается посредством item_name_string_id.
- Пустой слот/type=0 значит «нет оружия». При type=3/subtype=0 это полноценный одноручный меч.
- Стартовая экипировка игрока не содержит subtype 0. Лут и сундуки: не найдено в просмотренных генераторах, что не доказывает общего отсутствия. Stock→торговля→игрок не прослежено. Алиасы 21–25 не доказаны.

**Рекомендация для M0-00D/M0-04A:** переименовать WeaponSubtype.None=0 в Sword=0 без изменения чисел; ItemInstance разрешает (type 3, subtype 0). S04 и его сейвы не менялись в этом исследовательском коммите.

## Q2. AI_POTION_DAMAGE_SOURCE=SELF, AI_POTION_VALUE=SELF_MAX_HP

Имя stat_calcAttackDamage для функции @ 0x444db0 неверно: это расчёт MaxHP, как в D_character §D4.2 и C-03. Вызов @ 0x42ce8c получает ecx=edi=self, не указатель цели. Константы 0x745da0=0.5f и 0x745d98=0.25f.

**Ветка B / AllyAutoPotion @0x42ce8c:** currentHP < 0.5 × self.maxHP; faction=3, action 0x50, timer=0, held item (type 1, subtype 4) без установленного списания из инвентаря. Достижимость конкретного AI-состояния PARTIAL.

**Ветка A / InventoryPotion @0x42cbdc:** currentHP < 0.25 × self.maxHP, требуется (type 1, subtype 1) в сумке, списывается 1 штука.

| SelfMaxHP | CurrentHP | Faction | B (50%) | A (25%, предмет есть) |
| ---: | ---: | ---: | --- | --- |
| 100 | 49 | 3 | Да | Нет |
| 100 | 50 | 3 | Нет | Нет |
| 100 | 24 | 1 | Нет | Да |
| 268 | 133 | 3 | Да | Нет |

**Рекомендация Claude:** два правила InventoryPotion и AllyAutoPotion; в расчёте использовать SelfMaxHealth, не IncomingDamage. Лечение, инвентарные мутации и реальное игровое поведение остаются отдельными этапами интеграции.

## Реальные проверки / воспроизведение

**Отчёт исполнителя (локально, не повторён здесь):**

    python .scratch/research/m0_00b_final/verify_two_questions.py
    VERIFY_TWO_QUESTIONS_RESULT=PASS checks=43 failed=0
    SUBTYPE_0=VALID_WEAPON
    AI_POTION_DAMAGE_SOURCE=SELF
    AI_POTION_VALUE=SELF_MAX_HP

Запускатель tools/alpha_research/verify_subtype0_and_potion.py требует оригинальный локальный скрипт: при его отсутствии честно возвращает BLOCKED. Для независимого ревью нужно сохранить/опубликовать исходный скрипт DeepSeek и подтвердить фактический запуск.

## Передача Claude

1. Перепроверить обе статические трассы в локальной исследовательской копии Alpha, воспроизвести 43-check и новые NPC тесты; не считать исторические 462 теста свежей регрессией.
2. Уточнить контракт в M0-00C с учётом Sword=0 и двух отдельных PotionPolicy, отметить, что S04 нельзя менять без решения M0-00D.
3. Провести независимую проверку готовности конвейера к массовому переносу, исправить дефекты подготовки в разрешённых границах и выдать PREBATCH_REVIEW_PASS либо блокеры.
4. Массовый перенос DeepSeek — после выполнения действующих гейтов; итоговая проверка и исправление игры в Unity — после BATCH.

Оригинальная Alpha и Unity здесь не запускались; Unity-сцены и принятый S04 не изменены.
