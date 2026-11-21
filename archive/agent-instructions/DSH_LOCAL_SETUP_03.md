# TASK — CUBEGAME-DSH-LOCAL-SETUP-03

**Статус:** READY_FOR_LOCAL_EXECUTION. Это поручение для установленного DeepSeek Harness Desktop 0.2.2-rc.1, а не отчёт о выполнении. Требуются фактические локальные проверки.

## Обязательные входные данные

- Workspace: `D:\Projects\CubeGame`; **никаких дополнительных worktree/workspace**.
- Git ветка: `local/dsh-setup-20261121` (исходная ветка Codex не затрагивается), HEAD исторически `515d501`; перед работой проверить фактические SHA и status.
- Untracked `.dsh/skills` содержит 9 уже обнаруженных навыков. Сохранить весь каталог.
- Профиль DSH Desktop: `C:\Users\bow\.dsh\profiles\desktop`. Рабочий пресет `cubegame-dev`, источники пользовательского пресета могут быть в `plugins/cubegame-dev-preset` локального проекта; установить **точные** реальные пути, симлинки и конфигурацию перед записью.
- Рабочая модель `deepseek-flash`; только DeepSeek и Codex. Никаких новых API-ключей, платных подключений и Claude.
- **Права оставляем намеренно**: `danger-full-access`, `approval: never`. Не исправлять и не переключать. Не запрашивать повышение полномочий.
- D95 и D96 остаются действующими: ни отдельной Factoria, ни массового производственного конвейера; Unreal — только отдельный исследовательский spike, Unity — основной движок.

## A. Локальный аудит и сохранение

1. Проверить рабочую директорию, Git branch, `git status --porcelain`, `git remote -v`, номер DSH `0.2.2-rc.1`, наличие 9 skills, фактический пресет/модель/права.
2. Найти фактические источники `rogalik_env_check`, `rogalik_pixel_pipeline` в `package.json`, `cordis.patch.yml`, зависимостях/плагинах. Не предполагать, что сам `package.json` — единственный источник регистрации. Отделить Host-global tool и preset-scoped tool. Собрать **редактированный (redacted)** вывод с именами пакетов, row ids и источником scope — без значения credentials.
3. Перед правкой конфига создать локальный backup только затрагиваемых файлов **вне репозитория**, например `$HOME\.dsh\backups\cubegame-setup-03-<date>`; проверить пути, размер и копирование. Не логировать их содержимое и не добавлять их в Git.
4. Изучить именно локальную реализацию DSH `0.2.2-rc.1` и схему собственных preset-плагинов. Официальная текущая справка о scoped restriction: https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/core/tools/README.md. **Не копировать YAML с master вслепую:** версия пользователя может отличаться.

## B. Изоляция инструментов Rogalik

Цель: в `cubegame-dev` инструменты `rogalik_env_check` и `rogalik_pixel_pipeline` **не видны модели и не доступны к исполнению в контексте этого preset**. При этом в Rogalik-пресете остаются доступными, а общие Desktop-плагины, настройки полного доступа, другие пресеты и модели работают по-прежнему.

Предпочтение: preset-scoped `ctx.tools.restrict({deny: ['rogalik_env_check','rogalik_pixel_pipeline']})` или другой проверенный эквивалент **только если** установленная версия гарантирует одинаковое ограничение на presentation и dispatch, и обработчики действительно регистрируются в глобальном слое. Для scoped registrations у этого API могут быть исключения, поэтому проверять принадлежность и поведение экспериментом. **Не** отключать global bundle, не удалять Rogalik пакет, не редактировать чужие preset-файлы, не использовать `toolFilter` subagent-параметр из устаревших примеров. Не перекладывать весь Desktop профиль на новый CLI профиль.

Если правильную изоляцию невозможно доказать локально: оставить конфигурацию неизменной, указать конкретные модули и блокер; следующий шаг — на решение владельца. Не выдавать `PASS`.

## C. Восстановление Harness project layer

1. Убедиться, что `origin/review/ai-game-development-suite-20261121` виден (`git fetch` при необходимости; не push).
2. **Сначала** скопировать новые `tools/harness` и новые (отсутствующие) документы из ревью-ветки, не смешивая игровые .cs / .unity / assets. Пример только после Git проверки:
   `git restore --source=origin/review/ai-game-development-suite-20261121 --worktree -- tools/harness`
3. При импорте существующих `AGENTS.md`, `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `docs/GDD.md`, `docs/ROADMAP.md` и контрактов персонажей — **сделать содержательный 3-way review**, сохранить последние утверждённые факты обеих веток. Не заменять эти файлы простым `git restore` без diff. При неразрешимом противоречии — остановить только конкретное редактирование и перечислить вопрос владельцу.
4. Обновить старые упоминания Factoria/Alpha как `HISTORICAL`, подтвердить D95/D96/D97, только два исполнителя: DeepSeek и Codex.
5. Убедиться, что `node` есть: использовать локальный `node.cmd` из встроенного runtime DSH лишь после проверки реального пути и версии. **Не** изменять system PATH без поручения. Отдельный Node.js 22/24 LTS — опция, не обязательная немедленная установка.
6. Выполнить `node --test tools/harness/hook-pretool.test.mjs`, `node tools/harness/validate-project-knowledge.mjs`, `node tools/harness/preflight.mjs`; если часть новых docs отсутствует — не скрывать FAIL. Не включать хуки на Desktop. Не пытаться обходить `BLOCKED` preflight.
7. `git diff --check`, `git status --short`, осмотр diff. Не делать `git reset --hard`, `git clean -fd`, force-push, merge и автоматический push/PR. Не трогать собственную папку приватных референсов.

## D. Acceptance

- Scope и изменения пресета документированы; существует backup затронутой конфигурации, если она менялась.
- Обе Rogalik-команды **не видны в CubeGame**; они **по-прежнему видны в Rogalik**. Если не удаётся проверить обе стороны — status `PARTIAL`, даже если YAML выглядит корректно.
- Девять навыков подтверждены в новом контексте.
- Выборочный project-layer импорт воспроизводим; docs не противоречат D95–D97.
- Действующая модель и намеренные права не изменены.
- Реальные результаты Node и Git проверок с exit codes, включая ограничение/отсутствие тестов Unity.
- Вывести `STATUS=PASS | PARTIAL | BLOCKED`, `DSH_VERSION`, `PRESET`, `MODEL`, `PERMISSIONS`, `ROGALIK_SCOPE`, `SKILLS`, `HARNESS_TESTS`, `BRANCH`, `FILES_CHANGED`, `BLOCKERS`. Не выводить токены, env secrets, конфиги целиком.
