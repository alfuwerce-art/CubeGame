# Финальная ревизия CubeGame и Factoria

**Результат** `READY_FOR_OWNER_REVIEW`
**Этап** `REPOSITORY_CONSOLIDATION_FINAL`
**Дата** 21.11.2026

## Документы и решения

- [CubeGame PR #16](https://github.com/alfuwerce-art/CubeGame/pull/16) сверяет GDD, G0–G15, реестр, межчатовую память, AGENTS, активную задачу и контракт. Входной HEAD `f801f7b`, база `integration/m0-all@4e36f15`.
- [Factoria PR #2](https://github.com/alfuwerce-art/factoria/pull/2) сверяет инструкции, производственную память, карту рабочих каталогов, исторические карточки и отчёт о снятом манекене. Входной HEAD `0e0b321`, база `factory/work@9a2273a`.
- `HUMAN_MALE_BASE_V1` — `REVIEW_CLOSED / NOT_SELECTED`. [Запись решения](../decisions/2026-11-21-human-male-base-v1.md) отделяет итог владельца от [прежней технической проверки](CHARACTER_WORKSPACE_REVIEW.md). Модель, риг, экспорты и отчёты сохранены.
- Семь рас D63 относятся к основе дизайна. Восемь рас Alpha и карточки Factoria описывают исторические исследования. Frogman исключён. Фильдор входит в первый публичный одиночный релиз как редкие NPC по D80 и D92. Заморский континент и кампания остаются отдельной последующей вехой. Первая конфигурация игрового редактора и трактовки Dragonkin/Lizard и Skeleton/Undead [открыты](../design/OPEN_DECISIONS.md).
- M0/M1 и прежняя производственная память сохранены с историческим статусом. Переход D93 и утверждённая концепция D94 действуют. Следующий манекен требует нового поручения. Геймплейные проблемы, если агент их обнаружит, описываются отдельно.

## Ветки и PR

[Полный снимок](repository-branches-current.tsv) содержит все 53 ветки CubeGame и 5 веток Factoria, SHA, последний коммит, уникальное содержимое относительно рабочей базы, PR, назначение и источник восстановления.

| Репозиторий | KEEP | MERGE_CANDIDATE | ARCHIVE_CANDIDATE | DELETE_CANDIDATE | UNKNOWN |
|---|---:|---:|---:|---:|---:|
| CubeGame | 6 | 4 | 42 | 1 | 0 |
| Factoria | 3 | 0 | 1 | 0 | 1 |

- CubeGame `review/s05b2-dragon-visual-fix` указывает на `30f88a1`, который достижим из `integration/m0-all`. GitHub не помечает ветку защищённой. Открытого PR или отдельного worktree для неё нет. Это кандидат для будущего обсуждения с владельцем. Ветка и решение D83 сохранены.
- G1/G2 и G3 engineering proposals имеют отдельные коммиты и требуют предметного ревью при отдельном допуске. Между самими кандидатами есть общие коммиты. Числа в таблице нельзя складывать как независимую работу.
- Factoria `codex/mannequin-geo-02` сохраняет уникальный `5ccdfb0` с рабочими материалами GEO-02R и отчётом. Категория `UNKNOWN` относится к дальнейшей производственной целесообразности, история и ветка сохранены.
- CubeGame PR #1 и #2 открыты, а #15 закрыт. Factoria PR #1 закрыт. При текущей ревизии использованы только действующие PR #16 и #2. Дополнительных удалений, слияний, force-push и изменений целевых веток в этой ревизии не было.

## Локальные материалы

Снимок охватил 20 рабочих деревьев и 83 модельных файла без обхода junctions, кэшей и сверочных копий в каталоге восстановления. Все 19 групп прежней инвентаризации совпали по SHA-256. Две группы (`ElfRunner.blend` и вариант `FloatingHumanoid.blend`) оказались альтернативными путями через junctions к тем же физическим файлам. Остальные 17 групп состоят из физических дублей. У семи имён файлов есть разные SHA: `ElfRunner.blend`, `FloatingHumanoid.blend`, `mannequin_v3_male_rig.glb`, `mannequin_v3_rig.blend`, `orc_modular_v01.glb`, `orc_new.glb`, `VoxelElfMannequin.blend`.

| Группы | Зависимость и состояние |
|---|---|
| 3 группы HUMAN_MALE_BASE_V1 | Исторический кандидат сохраняется полностью. Повторно проверен SHA-256 всех 21 локальных файлов по исходному манифесту. |
| 9 групп PREP-01, ORC-01/02R, Mannequin V3 | Сохранить. В скриптах Factoria обнаружены имена входов/выходов. Например, `orc01_import_roundtrip.py` читает `orc_modular_v01.glb`, `orc02r_validate.py` — `orc_new.glb`, `mannequin_v3_validate.py` — BLEND/GLB V3, `prep01_diag_controls*.py` — `prep01_rigify_skinned.blend` по фиксированному пути. |
| 5 групп ElfRunner, FloatingHumanoid, VoxelElf | Кандидаты на последующее архивирование лишних независимых копий. В отслеживаемых скриптах/графах не найдено буквальных ссылок на эти имена. Ручные ссылки, ссылки из бинарных ассетов и динамически построенные пути требуют отдельной проверки перед перемещением. |

Пользовательские рабочие копии, референсы, модельные файлы и исторические экспорты сохранены на месте. Основные рабочие каталоги с несохранёнными правками не переключались. Детальные `worktrees.tsv`, `duplicate-groups.tsv`, `duplicate-paths.tsv`, `same-name-variants.tsv`, `previous-19-groups.tsv` и `human-male-integrity.tsv` находятся в `D:\Projects\CubeGame\.scratch\consolidation-recovery-20261121\final-audit`. Bundles `CubeGame-before.bundle` и `factoria-before.bundle` содержат точные входные SHA всех 58 веток. Предыдущие восстановленные bare-копии и прошлые bundles сохранены.

## Проверки

- GitHub: `git ls-remote --symref origin HEAD 'refs/heads/*' 'refs/tags/*'`, список PR GitHub REST, `git rev-list --left-right --count`, `git merge-base`, `git diff --name-only`.
- Локально: `git status --porcelain=v1`, `git worktree list --porcelain`, SHA-256 файлов, `git bundle verify`, `git fsck --connectivity-only --no-dangling`.
- Из Factoria: `python -B scripts/test_verify_character_docs.py`, `python -B scripts/verify_character_docs.py` и `python -B scripts/verify_character_docs.py --cubegame <рабочая-копия>`. Проверка различает архивные и активные записи, контролирует обязательные документы, точные якоря Markdown, роль каждого репозитория и согласованные этапы/статусы. Смысл решений и внешние источники проверены вручную; скрипт не определяет канон автоматически.
- GitHub Actions запускает заданную часть проверки в каждом репозитории. CubeGame берёт валидатор из зафиксированного коммита Factoria. Игровые исходники, Unity, риги, модели и генерация не входят в проверку.
