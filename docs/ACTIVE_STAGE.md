# ACTIVE_STAGE — объединение репозиториев

**Код этапа** `REPOSITORY_UNIFICATION`
**Результат ревизии** `READY_FOR_OWNER_REVIEW`

CubeGame — единый активный репозиторий. Factoria импортирована в `Factory/` через `git subtree` без squash, с сохранением Git-истории. Ветка `codex/factoria-unification`, [PR #17](https://github.com/alfuwerce-art/CubeGame/pull/17). База `integration/m0-all` не изменена.

**Разрешено:** проверить Git-файлы, сравнить SHA, согласовать инструкции, внутренние ссылки, CI и отчёт. **Запрещено:** разработка игры, Unity-сцен, манекенов, генераторов и новых производственных моделей.

**HUMAN_MALE_BASE_V1:** `REVIEW_CLOSED / NOT_SELECTED` (21.11.2026), [исторический отчёт](../Factory/docs/HUMAN_MALE_BASE_V1_REVIEW.md). Новый вариант пока не выбран.

[Roadmap G0–G15](ROADMAP.md) и [открытые вопросы](design/OPEN_DECISIONS.md) сохраняются. Новые игровые стадии не открыты.

**GitHub-проверка:** 470/470 отслеживаемых файлов Factoria в `Factory/`, 466 исходных Git SHA неизменны и 4 намеренно обновлены. GitHub Actions документации PASS на `4a9e695`. Временный workflow импорта удалён. Текущий PR готов к ревью, но ещё не слит.

**Ограничение:** неотслеживаемые и игнорируемые файлы локального D: не доступны для Git-переноса. Отдельную Factoria и резервные копии сохранять. Ни merge, ни архивирование оригинала без решения владельца.
