# ACTIVE STAGE — M0-F4 PRESENTATION

**Status:** `ACTIVE` at last checked canonical checkpoint (2026-11-10)  
**Canonical:** `integration/m0-all`  
**Governing plan:** [D90 — five-stage M0 finish](decisions/D90-m0-five-stage-finish-plan.md)  
**Task:** [M0-F4 presentation stage](stages/M0_F4_PRESENTATION_STAGE.md)

## Scope and order
1. F4-A — fix RIFF/WAV parsing of 93 catalogued sounds; connect in-game audio and event mapping.
2. F4-B — gameplay HUD and feedback; no duplication of gameplay state in UI.
3. F4-C — inventory, skills, specialization, trade, services and crafting interface.
4. F4-D — map, quest journal, icons and minimal player feedback.
5. F4-E — full EditMode/PlayMode and end-to-end presentation regression.

**Exit:** presentation features accessible from gameplay, all 93 WAV handled by technical pipeline, debug/IMGUI no longer required for key systems. Agent can propose `READY_FOR_PRODUCER_ACCEPTANCE` only; the producer alone decides `ACCEPTED`. Then M0-F5 can be opened separately.

## Verified input checkpoints
- [M0-F2 integration](reports/M0_F2_INTEGRATION_REPORT.md) — `ACCEPTED` on `4b9480836beeba859340938486f4200d16c1549e`.
- [M0-F3 gameplay loop](reports/M0_F3_GAMEPLAY_LOOP_REPORT.md) — `ACCEPTED` on `3cd79b4366dda4ff0d2410be22ee11d084eefaf1`.
- [History index and previous F1/F2 reports](history/M0_F1_F3_HISTORY_INDEX.md).

## Required guardrails
- [Alpha parity backlog](planning/M0_ALPHA_PARITY_BACKLOG.md) — no silent disappearance of missing Alpha mechanics.
- Artistic redesign of UI, M1 Character/Creature Factory, S06 renderer/weather and new M2 RPG mechanics are **not** part of F4.
- No publication of original or converted Alpha assets.
- `M0-F5` = foundation technical closeout, **not** public solo release.
- Proposed GDD/Roadmap revisions in documentation review are not a separate stage authorization.
