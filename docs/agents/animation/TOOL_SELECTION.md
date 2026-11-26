# Tool selection — use the smallest suitable layer
Check actual installed UE 5.9 capabilities, plugins, callable MCP/editor interfaces and permitted paths; record probe and actual action/output. Native UE is preferred, not a requirement to use all features. Use the existing [playbook](../UE_ANIMATION_PLAYBOOK.md) for official documentation and detailed retarget/transition procedure.
| Task | Primary tool | Check |
|---|---|---|
| Original pose/key animation | Control Rig / FK Control Rig + Sequencer; verified native UE editor animation-data API when UI unavailable | block phases, curves, bake, reload and inspect actual sequence |
| Fingers, head, shoulders, local edits | FK Control Rig, Curve Editor, Animation Sequence Editor | only approved channels, track comparison, close-up and gameplay |
| Licensed source to target | IK Rig, IK Retargeter, retarget pose; custom mapping if intentional floating parts require it | axes, rest pose, scale, chains, contacts, rigid module offsets |
| Feet/IK correction | Control Rig IK or verified existing solver | support intervals, world drift, floor and visible shape |
| Transition defects | AnimBP/state machine, Montage, Sync Markers/Groups, CharacterMovement/root-motion analysis | active pose, blend, capsule/root, actual input replay |
| Runtime diagnostics | PIE, Rewind Debugger/Animation Insights, Pose Watch if available | actual state/weights/frame timestamps and capture |
| Curves/batch measurement | native editor Python/data controller, Animation Modifiers or scoped C++ editor utility; Blender Python when justified | saved/reloaded exports, idempotence, independent sample, no global rewrite |
| Targeted action displacement | Motion Warping if already compatible and authorized | target/capsule consistency; not a generic run fix |
| Broader selection | Pose Search/Motion Matching, only approved scope | no unsolicited architecture migration |
Archive RUN-02 reports fit_sprint.py, fit_male_mocap.py, author_transitions.py, correct_floor.py, polish_male_motion.py, measure_male_contacts.py, bake_male_mocap.py, render_review.py, open_smooth_forward.py, stabilize_female_pelvis.py. These are local candidates, not installed tools or permission to run. Inspect source, actual paths and affected assets first. Reuse measurement/review scripts if verified; do not run old Quaternius extraction/transfer code. Prefer existing RUN-02 driver and evidence scripts over creating duplicate infrastructure. Source reports: RUN-02 archive 27.11, Quaternius archive 27.11. No archive binaries published.
