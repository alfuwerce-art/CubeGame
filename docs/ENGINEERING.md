# Инженерные проверки CubeGame

## Репозиторий

```powershell
python tools/verify_repository.py
git diff --check
git status --short --branch
```

GitHub Actions запускает тот же Python-скрипт на PR и интеграционных ветках. Он проверяет активные ссылки, указатели на ветки, структуру памяти, старые входные пути, JSON/GLB и хэши источников Factoria. Codex lifecycle hooks требуют доверия к определению. Для этой основы используются GitHub Actions и явный запуск Unity. [Документация Codex hooks](https://learn.chatgpt.com/docs/hooks).

Для восстановления Git применяйте `git bundle verify <bundle>`, затем `git clone <bundle> <отдельный-каталог>` или fetch требуемой ссылки. Незакоммиченные файлы восстанавливаются из отдельной копии по SHA-256.

## Unity

Версия указана в [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt). Команды запускаются из корня, `$run` — отдельный каталог результатов. Дождитесь завершения текущего Unity Editor перед запуском следующего.

```powershell
& $unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$run/edit.xml" -logFile "$run/edit.log"
& $unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults "$run/play.xml" -logFile "$run/play.log"
$env:CUBEGAME_RELEASE_DIR = "$run/player"
& $unity -batchmode -quit -projectPath "$PWD" -executeMethod CubeGame.EditorTools.ReleaseBuild.BuildWindowsDevelopment64 -logFile "$run/build.log"
```

`CUBEGAME_ALPHA_CONTENT` используется только для локальных проверок совместимости с разрешённым кэшем. Windows smoke запускается с одноразовым `-cubeSaveDir`. Сценарий G2.1 использует `-cubeSmokeContent fresh` и затем `verify` с тем же каталогом. Художественный результат проверяется отдельно.

## Experience

[Подтверждённый опыт](engineering/EXPERIENCE.md) фиксирует воспроизводимый результат до и после. Изменение обвязки имеет смысл, когда повторный запуск показывает наблюдаемый эффект.
