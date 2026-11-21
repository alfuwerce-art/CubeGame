# Параллельная работа CubeGame: безопасная установка второго Git worktree

**Решение:** [D71](../decisions/D71-parallel-isolated-worktrees.md). Оба агента могут работать одновременно, но только в разных каталогах, разных ветках и без общих Unity Library/Temp. **Это инструкция для запуска на ПК пользователя; создание удалённой ветки в GitHub не создаёт локальную папку.**

## Предварительная проверка, в PowerShell
```powershell
cd D:\Projects\CubeGame
git status --short
git worktree list
git fetch origin
```
Если `D:\Projects\CubeGame-deepseek` уже существует, **не перезаписывать**. Разобраться в её происхождении перед продолжением.

## Создать копию для DeepSeek (один раз)
```powershell
cd D:\Projects\CubeGame
git worktree add -b deepseek/m0-00b-gameplay-research D:\Projects\CubeGame-deepseek origin/planning/d71-parallel-worktrees-m0-00
cd D:\Projects\CubeGame-deepseek
git status --short
git branch --show-current
```
Это новая рабочая директория со своим checkout и индексом, но общей базой объектов Git; не полный повторный clone. Прежде проверить, что ветка/путь ещё не существует.

## Спасти незакоммиченные исследования #5 — только копированием
**Не удалять оригиналы. Ничего не перезаписывать, если целевые файлы уже существуют.**

```powershell
$src = "D:\Projects\CubeGame"
$dst = "D:\Projects\CubeGame-deepseek"
$paths = @(
  "docs\research\alpha2013\M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX.md",
  "docs\reports\M0_00B_RESEARCH_GAPS_REPORT.md"
)
foreach ($rel in $paths) {
  $from = Join-Path $src $rel
  $to = Join-Path $dst $rel
  if (!(Test-Path -LiteralPath $from -PathType Leaf)) { throw "Отсутствует черновик: $from" }
  if (Test-Path -LiteralPath $to) { throw "Целевой файл уже существует, не перезаписываю: $to" }
  New-Item -ItemType Directory -Path (Split-Path $to -Parent) -Force | Out-Null
  Copy-Item -LiteralPath $from -Destination $to -ErrorAction Stop
  if ((Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash) {
    throw "Контрольные суммы не совпали: $rel"
  }
}
cd $dst
git status --short
```
Теперь DeepSeek может **сначала зафиксировать эти два документа своим коммитом** (после проверки содержимого), потом продолжить #5/#8. Не добавлять непроверенные файлы массовым `git add .`. Исходные черновики в `D:\Projects\CubeGame` сохраняются до публикации.

## Параллельная координация и коммиты
- DeepSeek работает только в `D:\Projects\CubeGame-deepseek`, ветке `deepseek/m0-00b-gameplay-research`, получает задачи из #5/#8.
- Claude работает только в `D:\Projects\CubeGame`, отдельной ветке для #7, а затем другой ветке для #9. При смене задачи все свои рабочие изменения предварительно сохраняет; ничего чужого не стирает. Драфт M0-00A читать из `origin/planning/m0-00a-unity-architecture-audit`.
- После логической пачки: `git status --short` → явный `git add <проверенные файлы>` → `git commit` → `git push -u origin <своя-ветка>`; SHA и blockers в комментарии GitHub issue. Статус `PASS` только для реально проведённых тестов, не для read-only аудита.
- Контрактные вопросы и связи между ветками — через GitHub issue comments / опубликованный SHA; не общие текстовые файлы.
- Не делайте merge в main, rebase чужой ветки или массовый cherry-pick автоматически. После публикаций #7/#8/#9 — отдельная integration/review задача с тестами, только после продюсерского решения.

**Отдельное правило Unity:** если Claude уже открыл `S05B2_D69_Review`, второй экземпляр этого же проекта запрещён; DeepSeek в рамках M0-00B выполняет исследование без запуска Unity. Важный статус: S05B-2 VISUAL_REVIEW_FIX_REQUIRED, M0-00D DRAFT, M0-01 NOT_AUTHORIZED.
