# tools/alpha_models/catalog — полный каталог Alpha → Unity (D64)

Исследование и инструменты Claude Code по [D64](../../../docs/decisions/D64-claude-full-catalog-research-handoff.md).
Отчёт: [S05B2_CLAUDE_FULL_RESEARCH_REPORT](../../../docs/reports/S05B2_CLAUDE_FULL_RESEARCH_REPORT.md) ·
методичка массового прогона: [FULL_CATALOG_DEEPSEEK_PLAYBOOK](../../../docs/agents/FULL_CATALOG_DEEPSEEK_PLAYBOOK.md).

Принцип тот же, что у анимаций: **исполнять байты `Cube.exe`** (`x86emu`/`drawmodel_emu`), а не угадывать
по декомпиляции и не требовать живого захвата. Все производные данные Alpha — только в `.scratch/catalog`
(не в Git); в Git — код и файлы доказательств из чисел (адреса, номера действий, статусы).

| Модуль | Что делает | Исполняемый код Alpha |
|---|---|---|
| `asset_catalog.py` | реестр **всех** моделей: 2857 блобов `data1.db` + все 2576 вызовов загрузчика; маршрут и независимые статусы GEOMETRY / VISUAL / RIG / ANIMATION / BINDING для каждой | `model_registry` (байты загрузчика), селекторы ниже |
| `item_models.py` | предмет → индекс модели для всего домена предметов (тип/подтип/материал/+0xc/seed) | `GameController_item_icon_id` 0x4EC400 |
| `equipment_probe.py` | как `drawModel` рисует экипировку: SWAP (замена модели блока) / ATTACH (свой блок) + закон якоря | `Creature_drawModel` 0x4128F0 + `anchor_probe` |
| `trigger_catalog.py` | какие действия запускает ИИ **каждого** вида при каких классе/специализации/оружии; `export` → `npc_trigger_evidence.json` | `CombatBehavior::vfunc_0` 0x42D87F..0x42EAEF |
| `walk_oracle.py` | формула ходьбы каждого вида против исполненного `drawModel` → `walk_oracle_evidence.json` | `Creature_drawModel` |
| `anim_catalog.py` | все записи байта действия `creature+0x68` по `Cube.exe`, действие → состояние `drawModel`, все функции-рендереры моделей | резолвер состояний 0x4B4EF6 |
| `static_export.py` + `run_catalog_import.ps1` + `unity/Editor/AlphaCatalogImport.cs` | статичная геометрия **любой** модели в изолированный Unity: `.cubdec` + манифест → Mesh + prefab, проверка размеров/вокселей/маркеров, возобновление по SHA256 | правило маркеров D61 (`VoxelMesh.cs`) |

Закоммиченные доказательства (только числа): `walk_oracle_evidence.json` (156/156 `ORACLE_VERIFIED`),
`npc_trigger_evidence.json` (триггеры ИИ по видам с условиями; проверка дрейфа по SHA256 кода).

Команды (по порядку, всё пишет в `.scratch/catalog`):

```powershell
python -m tools.alpha_models.universal.pipeline scan --out .scratch/universal/static/alpha_static.json
python -m tools.alpha_models.catalog.asset_catalog                       # + alpha_static, item_models, registry_sizes
python -m tools.alpha_models.catalog.trigger_catalog --out .scratch/catalog/trigger_catalog.json   # ~2 ч, 10 процессов
python -m tools.alpha_models.catalog.trigger_catalog export
python -m tools.alpha_models.catalog.walk_oracle                          # ~25 мин, 2 процесса
python -m tools.alpha_models.catalog.anim_catalog --out .scratch/catalog/anim_catalog.json
python -m tools.alpha_models.catalog.equipment_probe --out .scratch/catalog/equipment_probe.json
powershell -File tools\alpha_models\catalog\run_catalog_import.ps1 -Select all
```
