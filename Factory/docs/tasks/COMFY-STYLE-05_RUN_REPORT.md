# COMFY-STYLE-05 — RUN REPORT (Полная настройка фабрики персонажей)

**Дата:** 2026-11-14 · **Ветка:** `factory/work` · **Исполнитель:** DeepSeek · **Владелец:** Producer
**Статус:** `READY_FOR_OWNER_VISUAL_REVIEW` — графы собираются и исполняются, метрики сняты,
**художественная приёмка не выполнена и не заявляется**.

---

## 1. Сводка

```text
TASK=COMFY-STYLE-05
STATUS=READY_FOR_OWNER_VISUAL_REVIEW

CHECKPOINT=DreamShaperXL_Turbo_v2_1.safetensors    6 939 220 250 B  SHA-256 OK
LORA_FICTIVERSE=VoxelXL_v1.safetensors             912 554 516 B    SHA-256 OK
LORA_RAL=ral-vxl.safetensors                       228 453 420 B    SHA-256 OK  (эксперимент, лицензия запрещает производство)
IPADAPTER=ip-adapter-plus_sdxl_vit-h.safetensors   847 517 512 B    SHA-256 OK
CLIP_VISION=CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors 2 528 373 448 B SHA-256 OK
CONTROLNET=controlnet-union-sdxl-1.0.safetensors   2 512 030 408 B  SHA-256 OK
COMFYUI=0.45.0 (Manager 4.2.2, Crystools 1.27.4, IPAdapter_plus a0f451a, controlnet_aux a587a4c, rgthree bc48c6a)

MAIN_WORKFLOW=workflows\factory_characters\FACTORY_CHARACTER_GENERATION_V1.json
MODES=BASE | FICTIVERSE | RAL  (+ FACTORY_CHARACTER_IPADAPTER.json, FACTORY_CHARACTER_CONTROLNET.json)
CHARACTER_CONFIG=workflows\factory_characters\characters_v1.json
TEST_MATRIX=workflows\factory_characters\factory_test_matrix_v1.json
RESULTS=outputs\factory\characters\ (PNG + <run>.manifest.json)
CONTACT_SHEETS=outputs\factory\characters\contact_sheet{,_races,_views,_tuning}.png
METRICS=measurements\factory_batch_report*.json
LAUNCH=scripts\comfyui\factoria_launch.ps1 -Preset xl -Port 8190 -DisableAssets -KeepRunning
RUN=scripts\comfyui\factory_run_batch.py --url http://127.0.0.1:8190 --server-pid <pid> ...
```

## 2. Аудит (фактическая проверка, а не «по отчёту»)

| Проверка | Результат |
|---|---|
| Все 6 файлов весов: размер + SHA-256 (`Get-FileHash`) | **OK** у всех |
| Частично скачанные файлы (`.parts`) | нет |
| ComfyUI 0.45.0, custom nodes с зафиксированными commit'ами | подтверждено |
| `CheckpointLoaderSimple` видит DreamShaper XL; `LoraLoader` видит обе LoRA; `ControlNetLoader` — union; `CLIPVisionLoader` — ViT-H | подтверждено живым `/api/object_info` (снимок `measurements/comfyui_object_info_2026-11-14_full.json`) |
| Чужие процессы | сервер владельца на 8188 (`--novram --reserve-vram 5`) **не останавливался**; для тестов поднят отдельный экземпляр на 8190 (`--reserve-vram 1 --disable-assets`, своя БД не нужна) |
| Свободная VRAM до запуска | 6 435 MB из 8 188 MB (≈1.5 ГБ занимали другие приложения) |

## 3. Архитектура графа

`FACTORY_CHARACTER_GENERATION_V1.json` (10 нод, 11 связей) — функциональные блоки:

| Блок | Реализация |
|---|---|
| **A. MODEL** | `CheckpointLoaderSimple` (SDXL Turbo) → `LoraLoader` (сила отдельно для MODEL и CLIP; в BASE нода в BYPASS) |
| **B. CHARACTER DESCRIPTION** | один текстовый блок в ноде POSITIVE, собирается из `characters_v1.json`: race/species, sex, body_type, head/face, clothing, armor, accessories, palette, pose, camera |
| **C. BODY_STYLE_LOCK** | стабильный текстовый блок (блочная геометрия, иерархия блоков, пиксельные текстуры, разделённые конечности, A-pose) |
| **D. FACE_STYLE_LOCK** | стабильный текстовый блок (воксельный череп/скулы/нос/челюсть/подбородок, взрослые пропорции, читаемость в игровом масштабе) |
| **E. COMPOSITION** | полный рост, A-pose, 3/4, нейтральный фон, без обрезки |
| **F. SAMPLING** | KSampler: 8 шагов, CFG 2.0, dpmpp_2m/karras, seed 424242, 768×1024, batch 1 |
| **G. OUTPUT** | SaveImage с уникальным префиксом; `factory_run_batch.py` пишет JSON-манифест на каждый результат |

Дополнительные файлы: `FACTORY_CHARACTER_BASE/FICTIVERSE/RAL.json` (три независимых режима),
`FACTORY_CHARACTER_IPADAPTER.json` (блок IPAdapter: LoadImage muted, UnifiedLoader+Advanced в BYPASS —
граф валиден и работает без референса, включается владельцем), `FACTORY_CHARACTER_CONTROLNET.json`
(ControlNet union: LoadImage/препроцессоры/ControlNetLoader muted, ApplyAdvanced BYPASS).

**Серверная валидация `execution.validate_prompt`: 6/6 PASS** (`measurements/factory_characters_validation.json`).

## 4. Метрики (реальные прогоны, RTX 4060 Laptop 8 GB)

| Режим | Разрешение | Время | Пик VRAM (вся карта) | Пик RAM процесса ComfyUI |
|---|---|---|---|---|
| BASE | 512×768 | 7.0 с | 7 684 MB | 2 671 MB |
| FICTIVERSE 0.6 | 512×768 | 8.1 с | 7 704 MB | 2 829 MB |
| BASE | 768×1024 | 9.0 с | 7 658 MB | 2 767 MB |
| FICTIVERSE 0.4 / 0.6 / 0.8 | 768×1024 | 10.0 / 10.0 / 10.0 с | 7 672 / 7 731 / 7 701 MB | 2 865 / 2 845 / 2 865 MB |
| RAL 0.4 / 0.6 / 0.8 | 768×1024 | 10.0 / 9.2 / 10.0 с | 7 672 / 7 690 / 7 696 MB | 2 870 / 2 866 / 2 867 MB |
| 1024×1024 | — | **SKIPPED** | свободно было 2 379 MB < 3 800 MB | — |

- **Всего прогонов: 19 PASS, 0 FAIL, 1 SKIP** (11 основных + 3 тюнинг + 3 ракурса + 2 расовых повторных).
- Пик VRAM «вся карта» включает ~1.5 ГБ посторонних приложений → собственный пик ComfyUI ≈ **6.2 ГБ**.
- Пик RAM процесса 2.6–2.9 ГБ, свободная RAM не опускалась ниже ~11 ГБ. OOM/подкачки нет.
- Повторная генерация идёт тем же временем (модель уже в памяти), кэш ComfyUI переиспользует неизменённые узлы.
- Влияние LoRA на ресурсы: +1–2 % времени и +30–60 МБ RAM; на VRAM в пределах шума.

## 5. Сравнение стилевых моделей и контроль качества (техническая оценка)

| Проверка (ТЗ §8) | BASE | FICTIVERSE 0.4–0.8 | RAL 0.4–0.8 |
|---|---|---|---|
| Распознаётся как орк | да | да (лицо орка, клыки) | да (клыки, зелёная кожа) |
| Блочная 3D-геометрия | **нет** — реалистичный рендер | **нет** — визуально как BASE | **да**, но «игрушечная» воксельность |
| Пиксельные текстуры | нет | **нет** | частично (блочные плоскости, не пиксельная текстура кожи) |
| Пропорции рук/ног/головы | нормальные | нормальные | голова крупнее, пропорции ближе к chibi |
| Лицо не чрезмерно мультяшное | скорее да (реалистичное) | да (реалистичное) | **нет** — упрощённое «игрушечное» лицо |
| Обрезанные конечности | нет | нет | нет |
| Пригодность как исходник для Tripo | сомнительно (нет стиля) | сомнительно (нет стиля) | формально да, но стиль не соответствует ТЗ |

### Ключевые технические выводы

1. **FICTIVERSE LoRA на SDXL-Turbo практически не работает.** При CFG 2.0 и 8 шагах её влияние
   не видно ни при 0.4, ни при 0.6, ни при 0.8, ни при 1.0 (доп. прогон `tune_fictiverse10_single`).
   Результат визуально неотличим от BASE. Причина — CFG 2.0: LoRA обучена под обычный CFG 5–7,
   а turbo-режим подавляет её вклад. Это **не** дефект установки: файл цел, нода загружает её,
   граф валиден.
2. **RAL LoRA работает даже при CFG 2.0**, но даёт «игрушечный»/Minecraft-подобный вид,
   упрощённое лицо и иногда оружие — прямое нарушение FACE_STYLE_LOCK и negative-списка.
   Плюс лицензия запрещает производственное использование (`FACT-D21`).
3. **Утечка «character sheet»**: 3 из 7 орк-прогонов дали 2–4 фигуры вместо одной, несмотря на
   negative (`multiple characters`, усиленный `character sheet, turnaround, single character only`).
   При CFG 2.0 негативы работают слабо.
4. **Дрейф пола**: несколько прогонов дали женских персонажей при `male orc` / `adult human fighter`.
5. **1024×1024 на этой карте без закрытия сторонних приложений не проходит** — свободной VRAM
   было 2.4 ГБ; прогон корректно пропущен, а не упал.

### Рекомендация (не выполнена, требует решения владельца)

Проверить тот же матрикс на **обычном (не-turbo) SDXL** — DreamShaper XL 1.0 или SDXL base,
CFG 5–7, 25–30 шагов. Только так можно честно оценить FICTIVERSE LoRA и качество лиц.
Это требует загрузки ещё одного checkpoint (~6.5–7 ГБ) — по ТЗ §6 без отдельного обоснования
и согласования не скачивалось.

## 6. Сравнительные листы

| Файл | Содержимое |
|---|---|
| `outputs/factory/characters/contact_sheet.png` | 11 прогонов: BASE / FICTIVERSE 0.4–0.8 / RAL 0.4–0.8 / расы / диагностика |
| `contact_sheet_tuning.png` | тюнинг: усиленный single-character negative, FICTIVERSE 1.0, RAL 0.6 |
| `contact_sheet_races.png` | человек и эльф при FICTIVERSE 0.6, тот же seed |
| `contact_sheet_views.png` | front / side / back (независимые генерации, **не** согласованный turnaround) |

**PNG-файлы в git не публикуются** (размер + лицензионные ограничения RAL): `.gitignore` исключает
`outputs/factory/characters/*.png`; в репозиторий идут только манифесты (JSON) и скрипты.

## 7. Ракурсы для будущего 3D (ТЗ §9)

В `characters_v1.json` есть блок `views` (front / three_quarter / side / back); граф и раннер
поддерживают любой ракурс. Сгенерированы front/side/back для орка (FICTIVERSE 0.6).
**Это четыре независимые картинки, а не согласованная 3D-геометрия** — для Tripo потребуется
отдельный этап согласования ракурсов.

## 8. Лицензии (ТЗ §10)

| Модель | Условия | Как используем |
|---|---|---|
| DreamShaper XL Turbo | openrail++ | допустимо с use-based restrictions |
| Fictiverse VoxelXL | openrail | коммерческое использование **требует отдельной проверки**, автоподтверждения нет |
| `ral-vxl` | `allowCommercialUse=RentCivit`, `allowDerivatives=False` | **только эксперимент**: манифесты помечены `NOT_ALLOWED_EXPERIMENT_ONLY`, обучать на выходах нельзя, в Tripo-конвейер не передавать |
| IPAdapter / ControlNet / CLIP ViT-H | apache-2.0 / MIT | ок для локального использования |

## 9. Ограничения и известные проблемы

- Стиль (блочная геометрия + пиксельные текстуры) на текущем чекпоинте **не достигается** ни одним
  из режимов без нарушения других требований.
- Независимого регионального управления BODY/FACE нет (только текстовые блоки).
- IPAdapter/ControlNet блоки подготовлены и валидны, но их вклад на CFG 2.0 тоже будет слабым;
  реальный тест — после перехода на обычный SDXL и при наличии референса/позы.
- 1024×1024 требует освободить VRAM (закрыть Unity/браузер) либо перейти на 768×1024 как рабочий режим.
- Turbo-модель чувствительна к длинным промптам: утечки «несколько фигур» и дрейф пола.

## 10. Инструкция запуска

```powershell
# 1) отдельный экземпляр ComfyUI под SDXL (владельческий 8188 не трогаем)
powershell -NoProfile -ExecutionPolicy Bypass -File `
  "D:\Projects\CubeGame Factory\scripts\comfyui\factoria_launch.ps1" -Preset xl -Port 8190 -DisableAssets -KeepRunning

# 2) в другом окне: перегенерировать матрикс (или одиночный прогон)
$py = 'D:\programs\ComfyUI\python_embeded\python.exe'
& $py -s "D:\Projects\CubeGame Factory\scripts\comfyui\factory_run_batch.py" `
  --url http://127.0.0.1:8190 --server-pid <PID> `
  --config "D:\Projects\CubeGame Factory\workflows\factory_characters\characters_v1.json" `
  --matrix "D:\Projects\CubeGame Factory\workflows\factory_characters\factory_test_matrix_v1.json" `
  --workflow-dir "D:\Projects\CubeGame Factory\workflows\factory_characters" `
  --out "D:\Projects\CubeGame Factory\outputs\factory\characters" `
  --report "D:\Projects\CubeGame Factory\measurements\factory_batch_report.json"

# 3) контактный лист
& $py -s "D:\Projects\CubeGame Factory\scripts\comfyui\factory_contact_sheet.py" `
  --report "D:\Projects\CubeGame Factory\measurements\factory_batch_report.json" `
  --out "D:\Projects\CubeGame Factory\outputs\factory\characters\contact_sheet.png"
```

В UI: `http://127.0.0.1:8190` → `Workflow → Open` → `FACTORY_CHARACTER_GENERATION_V1.json`.

## 11. Что дальше (первый шаг — решение владельца)

1. Посмотреть контактные листы и решить, какой режим ближе к цели.
2. Согласовать (или отклонить) загрузку **обычного SDXL** для честной проверки LoRA при CFG 5–7.
3. При переходе на обычный SDXL — перегенерировать матрикс и повторно оценить лица (FACE_STYLE_LOCK).
4. Только после визуального одобрения фиксировать стиль; сейчас статус — `READY_FOR_OWNER_VISUAL_REVIEW`.
