# Разделение CubeGame и Factoria — перенос 2026-11-13

## Статус: GitHub-часть DONE; PREP-03 (ComfyUI) — см. раздел «ComfyUI»

**Источник:** `alfuwerce-art/CubeGame`, review-ветка `review/m1-creature-family-research` (коммит `776a6ce`); копия исторических исследований — в `references/cubegame-m1/`.

**Цель:** весь дальнейший конвейер, код, память, отчёты и лабораторные модели ведутся в `alfuwerce-art/factoria` ветке `factory/work`, независимо от основного CubeGame.

---

## 1. GitHub-синхронизация (выполнено 2026-11-13)

**Remote:** `origin` = `https://github.com/alfuwerce-art/factoria.git` (добавлен 2026-11-13, fetch → расхождение историй подтверждено).

### Как объединяли

| Шаг | Факт |
|---|---|
| `git fetch origin` | получены `origin/main` и `origin/factory/work` |
| `git merge-base HEAD origin/factory/work` | **пусто** — истории не связаны (remote создан заново, локальная история своя) |
| `git merge origin/factory/work --allow-unrelated-histories` | конфликт только в `README.md` (add/add) |
| Разрешение конфликта | взят **локальный** README (актуальное состояние), в него добавлен раздел «Репозиторий и маршрутизация» со ссылками на `CHAT_ROUTING.md`, `references/cubegame-m1/`, `docs/MIGRATION_STATUS.md` |
| Итоговый merge-коммит | `8395409` — обе истории сохранены, ничего не перезаписано и не удалено |
| `git push origin factory/work` | успешно, `ca5bfef..8395409` |
| Проверка | `git rev-parse HEAD` == `git rev-parse origin/factory/work` == `83954091a30ee82771c5b0e9a8d5b600c4f1febd`; `git diff HEAD origin/factory/work` — **0 строк**; на remote 47 файлов |

**Итоговое состояние публикации** проверено на коммите **`03d35fe`** (после добавления ComfyUI):
`origin/factory/work == HEAD`, `git diff` — 0 строк, **65 файлов** на remote, крупнейший блоб 670 КБ
(PNG-доказательство), весов/бинарей/секретов нет. `origin/main` остаётся на `6dbb3d8` (не изменялся).
Последующие правки документации фиксируются отдельными коммитами в `factory/work`.

### Что теперь есть в `factory/work`

- Четыре главных документа Factory: `AGENTS.md`, `01_DEEPSEEK_FACTORY_IMPLEMENTATION_TZ.md`, `02_FACTORY_OPERATIONAL_DESIGN.md`, `03_FACTORY_PROJECT_MEMORY.md`.
- Правила маршрутизации: `CHAT_ROUTING.md`; этот файл.
- Скрипты и конфигурации лаборатории: `scripts/` (Blender smoke/diagnostics, ComfyUI API smoke), `plugins/cubegame-factory-preset/`, `.gitignore`.
- Отчёты и доказательства PREP-00 / PREP-01: `docs/` (`TOOL_INVENTORY`, `TEST_RESULTS`, `INSTALLATION_LOG`, `LICENSE_MATRIX`, `PIPELINE_GAPS`), `logs/PREP-00_*`, `logs/PREP-01_*`, безопасные малые артефакты `outputs/blender/*.blend|*.png`, `outputs/reports/*.json`.
- Исторические материалы M1: `references/cubegame-m1/` (8 файлов, включая CSV реестра 156 записей).

### Публикационная политика — что НЕ публиковалось

- Веса AI-моделей (`*.safetensors`, `*.ckpt`, `*.pt`, `*.pth`, `*.onnx`, `*.bin` — в `.gitignore`).
- Тяжёлые DCC-выходы: `outputs/**/*.fbx`, `*.glb`, `*.gltf`, `*.blend1` (в `.gitignore`; FBX PREP-01 1.54 МБ остаётся локально).
- Оригинальные Alpha-ассеты и их производные, `.scratch/content`, локальные конвертированные ресурсы CubeGame — не копировались и не публиковались.
- Скачанные установщики и архивы (`*.zip`, `*.7z`, `7zr.exe` — в `_installers/` вне репозитория, в `.gitignore` по расширениям).
- Секреты/токены/ключи отсутствуют; машинно-специфичный конфиг Blender (`environments/blender/`) в `.gitignore`.
- Основной CubeGame не изменялся: `git -C D:\Projects\CubeGame status` показывает только ранее существовавшие untracked-файлы M0 (созданы до этой сессии), HEAD не двигался.

---

## 2. ComfyUI (PREP-03 — установка и runtime smoke выполнены)

Старое предположение «ComfyUI уже установлен» **отменено решением владельца**: предыдущий экземпляр удалён, поиск не выполнялся.

| Параметр | Факт (2026-11-13) |
|---|---|
| Версия / источник | ComfyUI **0.45.0** portable NVIDIA, официальный GitHub release `Comfy-Org/ComfyUI` (`.7z`, 1.93 ГБ) |
| Целостность | SHA-256 `60c12e8c…a6f` **совпал** с digest из release assets |
| Путь / размер | `D:\programs\ComfyUI\` — 4.25 ГБ, 63 891 файл, изолированный `python_embeded` 3.13.14 |
| Лицензия | GPL-3.0 (файл `LICENSE` в поставке) |
| GPU | torch 2.14.1+cu130, CUDA 13.0, RTX 4060 Laptop распознана (capability 8.9), matmul 2048² за 0.12 с |
| Запуск | `--reserve-vram 5` (резерв 5 из 8 ГБ VRAM) — PASS; `--cpu` (RAM-first) — PASS |
| Runtime smoke | UI 200, API `POST /prompt` → `/history` → `/view`, граф `EmptyImage → SaveImage` без моделей, PNG 96×96 сохранён |
| Замеры | пик RAM процесса 0.86–0.97 ГБ; прирост VRAM ≈ 86 МБ; холодный старт 146 с, тёплый ~11 с |
| Веса моделей | Генеративные чекпоинты **не скачивались** (`models\checkpoints` пуст); в поставке 6 служебных TAESD-превью (~14 МБ) |

**Чего НЕ доказано:** способность ComfyUI генерировать/перерабатывать 3D-геометрию, качество и лицензионная
чистота AI-выходов, поведение VRAM под реальными моделями. Статус PREP-03 = `PARTIAL` (инфраструктура готова,
прикладная пригодность — после согласования конкретной модели). Подробности: `docs/TEST_RESULTS.md` (PREP-03).

---

## 3. Что осталось (не выдавать за сделанное)

- **PREP-02** (обмен форматами `.blend`/`.fbx`/`.glb`, оси/масштаб/материалы/action, deform-only скелет) — не выполнялся, статус `PLANNED`. Это следующий обязательный шаг.
- **PREP-03** — установка и smoke-тест ComfyUI не доказывают способность генерировать 3D: нужен отдельный согласованный эксперимент с конкретной моделью (источник, размер, лицензия, VRAM, качество сетки).
- Содержательные переговоры по AI-кандидатам и тяжёлым весам — только по согласованию владельца.
- `main` в Factoria не изменялся исполнителем; публикация — только в `factory/work`.
