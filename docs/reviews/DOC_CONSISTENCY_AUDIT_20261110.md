# CubeGame — аудит согласованности документации, 2026-11-10

**Status:** REVIEW VALIDATION / NO MERGE.
**Review branch:** `review/docs-consolidation-20261110` at `725c30862669477d3ad81399413126e1abbb0296`.
**Canonical:** `integration/m0-all` kept unchanged during this review (baseline `05336984ea48925a957a518ae1ee059be8299768`).

## Approved resolution
- D91 Q1=batch: model/animation exemplar + producer-approved batches, technical validation per Asset ID; D35 per-item manual art review superseded only in that respect.
- D91 Q2=single: complete standalone public single-player release prior to multiplayer/server systems; M0-F5 is not public-release authorization.
- D91 Q3=later: overseas continent and D60 four-act campaign form a later major product milestone after first public solo release.
- D92 producer approval: rare Fildor NPCs **required for first public solo release**; player-selectable race remains future DLC. D60 overseas campaign is still later under D91; origin lore does not move the continent into the first release.

## Cross-document checks
| Area | Result |
|---|---|
| GDD | D91 release boundaries and batch review added; historic S05 status caveat added; existing draft mechanics remain draft |
| Master Roadmap proposal | M0 D90, M1/S06 future stages, public solo milestone, later campaign, later multiplayer, postlaunch biome/season catalog distinguished |
| Art Direction | D67 + D91 explicit batch review, per-ID technical validation |
| Production Pipeline | D91 references, staged artistic acceptance, active AGENTS priority |
| AGENTS | Reads GDD/decision/roadmap for design tasks; executable authority remains ACTIVE_STAGE and D90 |
| M1/S06 | Updated Roadmap references; concepts remain NOT_AUTHORIZED; seasons remain postlaunch |
| Decision history | D59–D82 source text retained; D91 recorded as later explicit decision rather than retroactive rewriting |
| Alpha parity | Backlog unchanged; missing Alpha features remain tracked |

## Technical checks
- Compared review branch vs `integration/m0-all`: **39 modified/added files; 0 Unity or other non-documentation changes** (only `AGENTS.md` outside `docs/`).
- Across 12 key documents checked **125 Markdown links**: **0 broken repository-relative destinations**; external URLs were not checked for HTTP health.
- Not a Unity test run: documentation-only change, so no new Unity parity or build result is claimed.
- No wholesale merge of old docs branch. No deletions of historical files.

## Known editorial follow-ups / remaining limitations
1. `docs/planning/ROADMAP_DRAFT.md` deliberately retained as historical; `ROADMAP_CONSOLIDATION_PROPOSAL.md` is the review candidate, not yet a newly accepted canonical roadmap.
2. Earlier F1/F2 summaries inside PROJECT_MEMORY/ACTIVE_STAGE remain verbose/history-heavy; no destructive rewrite made to canonical current-status documents in this review.
3. GDD still carries historic S05 references in body. Opening caveat says those are historical; deeper sentence-by-sentence editorial migration and heading renumbering remain pending, to avoid altering anchors/content without complete checks.
4. Fildor NPC release placement is RESOLVED by D92 (first solo release); D80/D82 kept as historical original decisions, D91/Q3 remains later for overseas campaign. Detailed implementation, rarity, placement and gameplay integration remain OPEN for future stage design.
5. Non-key historical decision/workshop links and external URLs were not exhaustively checked; a full repository-wide link checker remains recommended before final promotion.
6. Removing/archive-deduplicating old source files remains pending after ensuring historical links and evidence are preserved.

## Gate for promotion
Review product decisions [D91](../decisions/D91-document-consolidation-release-batches.md), [GDD](../game-design/GDD_DRAFT.md), [Roadmap proposal](../planning/ROADMAP_CONSOLIDATION_PROPOSAL.md), and [conflict record](DOC_CONFLICTS_FOR_PRODUCER.md). Producer approval explicitly required before canonical integration.

## Редакционная сверка 2026-11-10 (дополнение)

- Устранено утверждение GDD, что каталог девяти биомов ещё DRAFT: D82 приоритетен, утверждено 9/41 по составу, но реализация postlaunch.
- Заголовки GDD «3.21/3.22» под разделом 5 и «3.7.1» под разделом 6 исправлены на 5.1/5.2 и 6.1. При наличии внешних ссылок на прежние якоря потребуется их обновить; content rule не менялся.
- Привязка D92 уточнена в дорожной карте: Фильдор-NPC в первом solo-релизе, Фильдор playable — future DLC.
