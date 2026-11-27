# CubeGame — ACTIVE STAGE
**Обновлено:** 2026-11-28. Игра: самостоятельная Windows-first 3D voxel/pixel action RPG. Текущая работа над персонажами — Unreal Engine 5.9.

## Игровые этапы
- G0 — направление утверждено.
- G1 — кандидат deepseek/g1-00-runtime-baseline-candidate, не принят.
- G2 — кандидаты claude/g2-world-foundation, claude/g2-1-content-binding, не приняты.
- G3 — персонажи и редактор; метод изготовления первой модели не утверждён.
- G4–G15 — [Roadmap](ROADMAP.md).

## Текущая работа
Продолжать локальную доработку существующих мужского и женского манекенов в UE по прямым задачам владельца и [согласованному порядку](art/CHARACTER_WORKFLOW.md). Сначала сверить исходники и фактически открытый проект; защитить принятую форму головы. Полная приёмка манекенов и движений не завершена. Далее — анимации, внешний вид, экипировка и полный эталон; массовый генератор преждевременен. [Реестр](art/assets/ASSET_REGISTER.md), [настройка Codex](agents/CODEX_SETUP.md).

Следующая локальная анимационная задача: RUN-02 (checkpoint ниже) — восстановить рабочую мастерскую, SEARCH готовых женских циклов Walk/Run, сравнить Relaxed, Neutral и 50 Female на текущем риге, получить решение владельца по художественному направлению; затем согласованные старты/стопы. Не создавать оригинальные клипы в этой задаче, не менять мужской набор. [Review Lab](agents/UE_ANIMATION_PLAYBOOK.md#review-lab) и проверка регрессий обязательны по scope. Финальная приёмка не подтверждена; не повторять старый мужской пилот автоматически.

G1/G2 Unity-кандидаты сохранены, приёмка открыта. Не считать миграцию и производственный Roadmap завершёнными; изменения этих систем — по отдельным заданиям. Локальные hooks, пути и runtime не проверены ChatGPT.

## RUN-02 — checkpoint (28.11, отчёты, не независимая проверка UE)
Источники: архивы RUN-02/Quaternius 27.11 и RUN02-dialogue-2026-11-28.zip. Последняя известная историческая локальная ветка codex/mannequin-locomotion-transitions, ddbc8d3 (не текущий подтверждённый HEAD); ранее codex/quaternius-sprint, eacb9058. Сверить реальный локальный checkpoint и активные пакеты, не восстанавливать ZIP поверх работы.
По отчёту рабочий проект D:\Projects\CubeGame\.scratch\humanoid-run-02\preview\CubeGame.uproject, L_CG_AnimationWorkshop, MCP http://127.0.0.1:8000/mcp, SK_CG_Female_AnatomyReview, два манекена. Управление 1/2, WASD, Shift, orbit мышью. Скрипты ранее в .scratch/quaternius-sprint-worktree/Experiments/QuaterniusSprint. Проверить фактические пути/версию/уровень перед действием; не подменять стандартными моделями.
Мужские 13 клипов: по отчёту 1643 защищённых канала/метаданных совпали после reload, 10 возвратов Idle; осанка, пальцы и Idle доработаны, финальной приёмки нет. Женский авторский набор отклонён несмотря на 14 стыков и 33 сценария; AS_CG_Quaternius_PackFemaleWalk перезаписан, резервные копии заявлены. Не использовать этот пакет как оригинальный источник или восстанавливать без разрешения. Quaternius только визуально. Реальные логи gate и видео отсутствовали в архиве 28.11.
| ID | Owner requirement / observation | Current status |
|---|---|---|
| ANIM-001 | Male Idle jerky, then nearly static; restore perceptible calm motion without jerk | CANDIDATE REPAIRED / owner review pending (28.11 report) |
| ANIM-002 | Male fingers visibly animate in all active clips, do not alter other animation | CANDIDATE REPAIRED / owner review pending (28.11 report) |
| ANIM-003 | Start acceleration too fast then slows; no slide before first push | RECHECK |
| ANIM-004 | Small abrupt upward/downward jumps during male run | RECHECK |
| ANIM-005 | Natural start from actual idle pose | RECHECK |
| ANIM-006 | Natural walk-to-run transition from actual pose | RECHECK |
| ANIM-007 | Natural visible run stop, also release Shift to walk | RECHECK |
| ANIM-008 | Female shoulder/chest deformation reported by agent | OPEN / проверить в женской задаче |
| ANIM-009 | Female energetic run, steady head/neck, planted feet/heel in Idle; speed coherence | RECHECK / проверить в женской задаче |
| ANIM-010 | Smooth turns, independent camera, WASD movement, no separate backward/side/diagonal clips per later owner instruction | RECHECK |
Старые W-only и отдельные directional-клипы отменены позднейшим указанием владельца; актуальный режим проверить локально. Мужской пилот не повторять автоматически. Текущий shortlist: Relaxed/Neutral Epic, 50 Female; ни один не утверждён. Правила отбора и восстановления редактора — в [playbook](agents/UE_ANIMATION_PLAYBOOK.md). Локальный checkpoint хранит доказательства и точные ревизии; здесь только сводка.
