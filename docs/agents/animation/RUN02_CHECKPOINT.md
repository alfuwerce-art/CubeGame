# RUN-02 — handoff checkpoint, 27.11.2026
Source: owner-provided RUN-02 dialogue/report and Quaternius dialogue/work log (27.11). Reported local state only, not independently executed or merged.
- Latest reported RUN-02 branch: codex/mannequin-locomotion-transitions, ddbc8d390109037879bd340e0ee8bac553ea91ff. Prior Quaternius branch codex/quaternius-sprint, eacb9058ec23a67747227c02e2834a2da98f20d1. RUN-02 archive includes six-commit patch and preview snapshot; these are local, not verified remote. Existing preview reported under .scratch/humanoid-run-02/preview; scripts in .scratch/quaternius-sprint-worktree/Experiments/QuaterniusSprint. Verify actual local location, HEAD, dirty files, active project/asset names and packages. Do not restore ZIP over current work.
- Last male iteration: 13 active clips per report; current candidate not accepted. Idle 2.60 s and upper-bone max adjacent rotation 0.598 -> 0.021 degrees; owner rejected near-static result. Finger pass 30 phalanx tracks, unrelated track hash unchanged, owner reported static fingers. No visual acceptance.
- Other reported values: speed 164.7 -> 168 cm/s at transition; max pelvis adjacent displacement 3.82 -> 0.98 cm; limited measured stance drift on earlier stop 10.7 -> 1.7 cm. Values are agent reports, not independent PASS or global thresholds.
| ID | Owner requirement / observation | Current status |
|---|---|---|
| ANIM-001 | Male Idle jerky, then nearly static; restore perceptible calm motion without jerk | OPEN / owner rejected |
| ANIM-002 | Male fingers visibly animate in all active clips, do not alter other animation | OPEN / owner rejected |
| ANIM-003 | Start acceleration too fast then slows; no slide before first push | RECHECK |
| ANIM-004 | Small abrupt upward/downward jumps during male run | RECHECK |
| ANIM-005 | Natural start from actual idle pose | RECHECK |
| ANIM-006 | Natural walk-to-run transition from actual pose | RECHECK |
| ANIM-007 | Natural visible run stop, also release Shift to walk | RECHECK |
| ANIM-008 | Female shoulder/chest deformation reported by agent | OPEN / deferred outside male pilot |
| ANIM-009 | Female energetic run, steady head/neck, planted feet/heel in Idle; speed coherence | RECHECK / deferred female |
| ANIM-010 | Smooth turns, independent camera, WASD movement, no separate backward/side/diagonal clips per later owner instruction | RECHECK |
Earlier female W-only restriction and earlier request for separate directional clips superseded by later owner messages; do not resurrect. Exact current control policy must be verified locally; do not infer final production architecture. Quaternius data transfer reported despite visual-only rule; no new extraction, do not certify provenance or delete candidates without owner direction.
## Local checkpoint template
Task/workstream; latest owner message and scope; current Git/worktree/project/active assets; last accepted version (or NONE); baseline restore recipe plus verified copy; candidate revision; references/provenance; requirements table (id, owner wording/source, mode, target/phase, acceptance check, protected elements, status, evidence, attempts, reason for deferred/superseded); runtime input/camera; five verdicts; next action; owner decision and exact revision. Keep private paths/evidence local; only concise shared status in repository.
