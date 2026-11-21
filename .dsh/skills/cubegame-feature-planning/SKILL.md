---
name: cubegame-feature-planning
description: "Turn an authorized CubeGame game design change into a scoped dependency-aware implementation plan without modifying the Unity project."
---
# Task planning for CubeGame

## Trigger
Owner requests a feature, gameplay change, integration or stage planning. This skill produces **a task contract**, not unsolicited code or a new game design.

## Workflow
1. Read `AGENTS.md`, `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `docs/CONCEPT.md` and relevant GDD/Dxx/ADR/roadmap only.
2. Confirm approved scope and source. Unknown product choices are `UNDECIDED`; do not select engine, mechanics, model factory or NPC behavior unilaterally.
3. Search existing code for types and call sites; consult [NAME_REGISTRY](../../../docs/architecture/NAME_REGISTRY.md) only as an index. Check exact case, namespace, assembly and serialized asset path from source.
4. Build explicit `requires / supplies / modifies / does_not_touch` following [DEPENDENCY_MAP](../../../docs/architecture/DEPENDENCY_MAP.md). Allow a narrow independent prototype with stubs, but require proper integration tests before closing.
5. Describe affected Data/GameLogic/View/Input/UI/Save/Utils boundaries and backward compatibility of save files/world seeds.
6. Produce the fields from [task template](../../../docs/agents/HARNESS_TASK_TEMPLATE.md) plus a test oracle: automated checks, Windows Player scenario, and visual evidence where relevant.
7. Mark any ambiguity as a question or blocked decision, and only implement after scope approval.

## Output
Exact source SHA, branch suggestion, allowed paths, criterion-by-criterion test checklist, risks, open choices; stage remains unchanged. No autonomous modifications to AGENTS, D95, PR merge or dependencies.
