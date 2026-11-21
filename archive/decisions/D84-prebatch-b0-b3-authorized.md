# D84 — Предмассовое ревью M0-00: DeepSeek выполняет партии B0–B3

**Дата:** 2026-11-09  
**Источник:** прямое решение продюсера в сессии Claude Code: «Да, B0–B3».  
**Статус:** APPROVED. Это **не** `ACCEPTED` M0-00/S05B-2 и **не** запуск M0-01+.  
**Основание:** [M0_00_PREBATCH_REVIEW_REPORT](../reports/M0_00_PREBATCH_REVIEW_REPORT.md) — итог
`PREBATCH_REVIEW_PASS` (ограниченный).

**Номер:** D70–D82 заняты в других ветках, D83 — дракон 0x65; на всех удалённых ветках D84 свободен.

## Решение

1. **База партий:** `claude/m0-00-prebatch-review` @ `7e22eb05852dfeec3ee5edf86bdb965a28586528`
   (D83 `dragon_0x65_v1` + DeepSeek `d07564c` + исправления П1–П6).
2. **Исполнитель:** DeepSeek Harness, в своей рабочей папке и своей ветке; партии строго последовательно,
   одна партия = один коммит + отчёт.
3. **Разрешённые партии** (критерии — отчёт §5):

| Партия | Содержание | Критерий PASS |
|---|---|---|
| B0 | Сверка базы | pytest 375/375; M0-пилоты 478/478; `gen_alpha_tables` и `build_transfer_registry --check` PASS; `item_layer --verify` MATCH; `verify_catalog_import` 2856/2856; Unity EditMode 296/296; PlayMode walk/attack; HumanMale 98/98 при 30/60/120 Гц |
| B1 | Каталог + дракон D83 в review-сцене | 2856/2856; дракон совпадает с D83 |
| B2 | `transfer_planner batch --authorization D63 --scope d63_full --producer-approved-d64` (сначала `--dry-run`) | все виды PASS или явный BLOCKED с причиной; HumanMale 98/98 |
| B3 | то же, `--scope d63_actions` | действия выполнены, отклонение 0; RuneGiant — BLOCKED; HumanMale 98/98 |

4. Любое расхождение с критерием — остановка и отчёт, без подгонки порогов.
5. Код планировщика не меняется: B2/B3 используют существующие разрешения D63/D65 (`BATCH_AUTHORIZATIONS`).

## Не разрешено

- **B4** (слой предметов в Unity: нет адаптера и scope) и **B5** (таблицы систем → Core: ждёт M0-00D) —
  `PREBATCH_BLOCKED`, нужны отдельные решения.
- Merge в `main`; изменения S02–S04 и `Assets/` вне партий; публикация ассетов Alpha; присвоение `ACCEPTED`;
  запуск M0-01+.

## Дальше

После B0–B3 — фактическая проверка игры в Unity по D72 (M0-00U) с исправлением найденных проблем,
затем ревью Claude и личная приёмка продюсера.
