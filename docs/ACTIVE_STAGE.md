# ACTIVE STAGE — M0 ALPHA FULL UNITY PORT

**Decision:** [D92 — full Alpha-to-Unity port](decisions/D92-m0-full-alpha-unity-port.md)  
**Stage:** [M0 ALPHA FULL UNITY PORT](stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md)  
**Status:** `ACTIVE / REWORKED`  
**Canonical branch:** `integration/m0-all` (документы D92 и отчёты P0–P6 сведены 2026-11-14; код P0–P6 ещё в review-ветках)  
**Последний implementation checkpoint:** `codex/m0-p6-alpha-menu-hud` @ `fb4e0d5`  
**Следующий блок:** **P7 — QUESTS / CITIES / DUNGEONS / GAMEPLAY CONTENT**

## Решение продюсера 2026-11-11

M0 полностью переработан. Цель — перенести Cube World Alpha 2013 как цельную одиночную игру на Unity. Старый подход «подсистема существует + тест проходит = этап готов» больше не используется.

D90 F1–F5 и прежние F1–F4 результаты сохраняются как инженерные checkpoint'ы и доказательная база, но **не доказывают parity соответствующей системы**.

## Состояние P-блоков

Ветки идут одной цепочкой: каждая следующая содержит все предыдущие
(`review/m0-full-unity-port-rework` ⊂ P0 ⊂ P1-research ⊂ P1 ⊂ … ⊂ P6). Ни один блок не имеет `PASS`.

| Блок | Ветка @ коммит | Статус | Отчёт |
|---|---|---|---|
| P0 Boot / menu / input / save selection | `deepseek/m0-full-port-p0` @ `6e0c16c` | `PARTIAL` (логика — кандидат, presentation-fix) | [P0](reports/M0_ALPHA_FULL_PORT_P0_REPORT.md) |
| P1 research: code parity | `codex/m0-alpha-code-parity-p1` @ `c1f9f75` | `PARTIAL` | [P1 code parity](reports/M0_ALPHA_CODE_PARITY_P1_REPORT.md) |
| P1 World core / streaming / render distance | `codex/m0-p1-world-streaming` @ `0763c83` | `PARTIAL` | [P1](reports/M0_ALPHA_FULL_PORT_P1_WORLD_REPORT.md) |
| P2 Environment / objects / structures | `codex/m0-p2-environment` @ `85d4e13` | `PARTIAL` | [P2](reports/M0_ALPHA_FULL_PORT_P2_ENVIRONMENT_REPORT.md) |
| P3 Population / NPC / creature AI | `codex/m0-p3-population-ai` @ `f265108` | `PARTIAL` | [P3](reports/M0_ALPHA_FULL_PORT_P3_POPULATION_AI_REPORT.md) |
| P4 Action / combat / animation | `codex/m0-p4-action-combat` @ `f0d2b71` | `PARTIAL` | [P4](reports/M0_ALPHA_FULL_PORT_P4_ACTION_COMBAT_REPORT.md), [coverage](reports/M0_ALPHA_FULL_PORT_P4_ACTION_COVERAGE.tsv) |
| P5 Character / items / progression | `codex/m0-p5-character-items-progression` @ `01930ff` | `PARTIAL` | [P5](reports/M0_ALPHA_FULL_PORT_P5_CHARACTER_ITEMS_PROGRESSION_REPORT.md) |
| P6 UI / HUD / map / journal | `codex/m0-p6-alpha-menu-hud` @ `fb4e0d5` | `READY_FOR_PRODUCER_REVIEW` / `PARTIAL` | [P6](reports/M0_ALPHA_FULL_PORT_P6_ALPHA_UI_REPORT.md) |
| P7 Quests / cities / dungeons / content | `codex/m0-p7-quests-cities-dungeons` (база P6) | `RESEARCH` — TRACE миссий готов | [P7-A trace](research/alpha2013/P7_MISSIONS_TRACE.md) (в ветке P7) |
| P8 Audio / VFX / time / presentation | — | `NOT_STARTED` | — |
| P9 Persistence | — | `NOT_STARTED` | — |
| P10 Final parity run | — | `NOT_STARTED` | — |

Дополнительно, как вход и история:
- [Runtime recovery](reports/M0_ALPHA_RUNTIME_RECOVERY_REPORT.md) — упаковка AlphaContent, модели, отсутствие magenta.
- [M0-F4 presentation](reports/M0_F4_PRESENTATION_REPORT.md) — `codex/m0-f4-presentation` @ `60f24be`, инженерный checkpoint до D92 (RIFF/WAV, UI Toolkit baseline); вошёл в цепочку P-веток.
- `deepseek/m0-f5-stable-launcher` @ `44550b8` — стабильный лаунчер и первая Windows-сборка; **не** входит в цепочку P-веток.

Открытые долги каждого блока — в [parity backlog](planning/M0_ALPHA_PARITY_BACKLOG.md); `PARTIAL` выше означает, что там есть blocking-записи.

## Обязательный метод

Для каждого системного контура:

`FIND ORIGINAL CODE → TRACE → DATA/STATE/RNG → SPEC → UNITY IMPLEMENTATION → RUNTIME INTEGRATION → WINDOWS PLAYER → COMPARE → PRODUCER REVIEW`.

`UNKNOWN`, `PARTIAL`, `PLACEHOLDER_NOT_ALPHA` и непринятый `CUBEGAME_CONTRACT` на обязательном основном пути блокируют PASS системы.

## Следующий шаг — P7

База: `codex/m0-p6-alpha-menu-hud` @ `fb4e0d5`. Объём — [P7 в ТЗ этапа](stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md#p7--quests--cities--dungeons--gameplay-content):
mission generation/types/targets/rewards, city NPC roles/services, dungeon entry/layout/population, bosses/chests/rewards, pets.

Канонический код ещё не содержит P0–P6: перенос кода в `integration/m0-all` — решение продюсера после ревью.

## Источники

- [D92](decisions/D92-m0-full-alpha-unity-port.md)
- [Stage matrix](stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md)
- [Alpha parity backlog](planning/M0_ALPHA_PARITY_BACKLOG.md)
- [Root-cause audit](reports/M0_ALPHA_TO_UNITY_ROOT_CAUSE_AUDIT.md)
