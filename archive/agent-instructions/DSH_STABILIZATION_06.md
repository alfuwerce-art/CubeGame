# TASK — CUBEGAME-DSH-STABILIZATION-06

**Статус:** READY_FOR_LOCAL_EXECUTION; **не** считать выполненным до реальных локальных проверок.

## Подтверждённый исходный результат

- По пользовательскому отчёту `CUBEGAME-DSH-ACTIVATION-05`: после перезапуска 2026-11-21 20:14:09 новый запрос `cubegame-dev` содержит **30** инструментов вместо **32**, обе команды Rogalik отсутствуют.
- Пресет `cubegame-dev`, `D:\Projects\CubeGame`, `deepseek-official/deepseek-flash`, 9 проектных Skills; утверждённые права `danger-full-access + approval: never` сохранены. **Не менять** права.
- В исходной ветке `local/dsh-setup-20261121` остались незакоммиченные изменения, девять локальных навыков, отдельные исходники `cubegame-dev-preset` под игнорируемым `plugins/`, и 6 Markdown замечаний `git diff --check`.

## План — три проверки, без расширения объёма

1. **Rogalik live-check — отдельная READ-ONLY сессия, запущенная пользователем**: выбрать существующий workspace Rogalik и пресет `rogalik-dev`. Только проверить фактический список tool schemas текущего запроса, что `rogalik_env_check` и `rogalik_pixel_pipeline` присутствуют. Не вызывать сами инструменты, не менять Rogalik, не создавать workspace. Если пользователь не запускал отдельную сессию, пометить `ROGALIK_LIVE_NOT_CHECKED`, не выдумывать PASS.
2. **Markdown и Git в CubeGame**: проверить `git status --porcelain=v1`, `git diff --check` и `git diff --cached --check` отдельно. Отдельно проверить staged additions, поскольку `git diff --check` без `--cached` их не охватывает. Исправлять только реальные Markdown trailing whitespace, не менять смысл/историю и не стирать все переводы строк автоматически. Для Markdown hard-break можно заменить двойной пробел на другую корректную разметку, не портя документ. Повторить обе проверки до exit 0.
3. **Проверить переносимость guard**: определить `git check-ignore -v plugins/cubegame-dev-preset/guard/index.js` и `git ls-files plugins/cubegame-dev-preset`. Если исходники guard остаются в игнорируемой директории, вручную оценить **только** `guard/index.js`, `cordis.patch.yml`, `package.json`: запрет секретов, credential, приватных абсолютных путей, бинарников, `node_modules`. После анализа явно подготовить whitelist файлов для отдельного `git add -f -- <точные пути>`, но не делать `git add -f plugins` и не захватывать другие плагины. Не изменять symlink на Desktop и не перезапускать работающий DSH без необходимости.

Проверить `node --test tools/harness/hook-pretool.test.mjs`, `node tools/harness/validate-project-knowledge.mjs`, `node tools/harness/preflight.mjs` и `node --test tools/harness/dsh-tool-scope-probe.mjs` (или точную ранее проверенную команду пробного скрипта). Если выбранная команда probe не принимает `--test`, показать фактическую команду и результат. Не запускать Unity.

## Полномочия

Только DeepSeek Harness и Codex. Никаких новых worktree. Нельзя делать Git reset/clean, force-push, publish, merge, удалять исторические материалы, обновлять engine/систему, восстанавливать Factoria. Нельзя изменять `danger-full-access` или `approval: never`. Стабилизация и подготовка к коммиту не являются разрешением на автоматический push.

## Приёмка

`STATUS=PASS/PARTIAL/BLOCKED`, `CUBEGAME_TOOLS`, `ROGALIK_LIVE`, `GUARD_SOURCE_TRACKED`, `GIT_DIFF_CHECK`, `GIT_DIFF_CACHED_CHECK`, `NODE_TESTS`, `SKILLS`, `FILES_CHANGED`, `BRANCH`, `COMMIT_CREATED`, `BLOCKERS`. Отмечать, что live-сеанс Rogalik проверяется **в его собственном рабочем пространстве**, а не в CubeGame.
