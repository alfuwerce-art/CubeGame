# COMFY-STYLE-04B — RUN REPORT (Civitai Voxel Style [SDXL] + Voxel Face Style Lock)

**Дата:** 2026-11-13 · **Ветка:** `factory/work`
**Основание:** AUTHORIZED COMFY-STYLE-04B (+ дополнение VOXEL FACE STYLE LOCK)
**Решения:** `FACT-D21` (лицензионный блокер), `FACT-D20` (экспериментальный SDXL-стек)

---

## 1. Сводка

```text
TASK=COMFY-STYLE-04B
STATUS=<см. §8>

LORA=Voxel Style [SDXL]  (Civitai model 461862, version 513975, author RalFinger)
LORA_FILE=ral-vxl.safetensors  228 453 420 B
LORA_SHA256=9dc480ee98c12a499bee1fc19ee0e13969a3c6b14be0e6ec133a11d563e83a25
LORA_TRIGGER=ral-vxl
LORA_STRENGTH=0.6 (альтернативы 0.4 / 0.8)
LORA_LICENSE=allowCommercialUse=RentCivit, allowDerivatives=False, allowNoCredit=False
             -> EXPERIMENTAL / LICENSE_BLOCKED_FOR_PRODUCTION (FACT-D21)

CHECKPOINT=DreamShaperXL_Turbo_v2_1.safetensors (ещё докачивается, отдельный процесс)
FICTIVERSE_VOXEL_XL=сохранён, не заменялся

WORKFLOW=workflows\dreamshaper_xl\DREAMSHAPER_XL_CIVITAI_VOXEL.json   (орк, канонический)
         workflows\dreamshaper_xl\DREAMSHAPER_XL_CIVITAI_VOXEL_HUMAN.json
         workflows\dreamshaper_xl\DREAMSHAPER_XL_CIVITAI_VOXEL_ELF.json
PROMPTS=workflows\dreamshaper_xl\prompt_voxel_face_lock_v1.json
LAUNCH=scripts\comfyui\launch_dreamshaper_xl.bat
```

## 2. Источник и проверка по Civitai API

Запрос: `GET https://civitai.com/api/v1/models/461862` (без авторизации, только чтение метаданных).

| Поле | Значение |
|---|---|
| name / type | Voxel Style [SDXL] / LORA |
| creator | RalFinger |
| tags | voxel, cubed, minecraft, 3d, cubes, style |
| version | `513975`, name «SDXL», `baseModel: SDXL 1.0`, Published 2024-05-18 |
| trainedWords | **`ral-vxl`** |
| file | `ral-vxl.safetensors`, 228 453 420 B (подтверждено `Content-Range: bytes 0-0/228453420`) |
| SHA-256 | `9DC480EE98C12A499BEE1FC19EE0E13969A3C6B14BE0E6EC133A11D563E83A25` |
| download | `https://civitai.com/api/download/models/513975` (307 → подписанный b2.civitai.com URL; без токена) |
| allowCommercialUse | **RentCivit** |
| allowDerivatives | **False** |
| allowNoCredit | **False** |

Лицензионный вывод — `docs/decisions/FACT-D21-civitai-voxel-lora-license.md`: LoRA пригодна для
экспериментов и сравнения стиля, **но не объявляется производственной**; Fictiverse Voxel XL
остаётся основной.

## 3. Установка

| Шаг | Результат |
|---|---|
| Загрузка | `scripts/comfyui/fetch_checkpoint_parallel.ps1` (6 диапазонов, перезапускаемая, SHA-256 в конце) |
| Целевой путь | `D:\programs\ComfyUI\ComfyUI\models\loras\ral-vxl.safetensors` |
| Проверка SHA-256 | см. §8 (финальный статус) |
| Обнаружение в ComfyUI | см. §5 (`LoraLoader.lora_name`) |
| Fictiverse Voxel XL | не тронут |

## 4. Workflow

Собраны `scripts/comfyui/build_civitai_voxel_workflows.py` из `prompt_voxel_face_lock_v1.json`
и живого `/api/object_info`; структурная проверка — `measurements/civitai_voxel_build_report.json`.

Схема каждого файла:
`CheckpointLoaderSimple → LoraLoader(ral-vxl, strength 0.6) → CLIPTextEncode(+/-) →
EmptyLatentImage 768×1024 → KSampler(8 шагов, CFG 2.0, dpmpp_2m/karras) → VAEDecode → SaveImage`.

| Файл | Назначение | Positive-промпт |
|---|---|---|
| `DREAMSHAPER_XL_CIVITAI_VOXEL.json` | **первый тест, орк** | `ral-vxl` + BODY_STYLE_LOCK + FACE_STYLE_LOCK + орк |
| `DREAMSHAPER_XL_CIVITAI_VOXEL_HUMAN.json` | сравнительный тест, человек | тот же стиль-блок, меняется только раса |
| `DREAMSHAPER_XL_CIVITAI_VOXEL_ELF.json` | сравнительный тест, эльф | то же |

Сила LoRA меняется в одной ноде `Voxel Style LoRA (ral-vxl)`; рекомендуемая серия сравнения —
0.6 → 0.4 → 0.8 при одном и том же seed (все файлы используют seed `424242`).

## 5. Voxel Face Style Lock (дополнение к ТЗ)

Реализовано как **два текстовых блока одного промпта** (как и требует дополнение — независимое
управление стилями пока не существует):

- **BODY_STYLE_LOCK** — блочная 3D-геометрия тела, иерархия блоков (крупные/средние/мелкие),
  пиксельные текстуры по всей поверхности, гуманоидные пропорции, разделённые конечности,
  нейтральная A-pose, полный рост, нейтральный фон.
- **FACE_STYLE_LOCK** — воксельная геометрия лица: череп, надбровные дуги, скулы, нос, челюсть и
  подбородок из блоков разного размера; пиксельные текстуры лица согласованы с телом; взрослые
  пропорции; глаза с монолитными блочными «зрачками», но не гипертрофированные; лицо читаемо при
  уменьшении до игрового масштаба.
- **Расовые блоки:** орк (тяжёлая нижняя челюсть, клыки, надбровье, широкий нос), человек
  (выраженные скулы, прямой нос, квадратный подбородок), эльф (тонкая челюсть, высокие скулы,
  длинные ступенчатые уши).
- **Negative** содержит запрещённые отклонения: chibi, детские пропорции, огромные глаза,
  крошечный нос, кукольное лицо, аниме-пропорции, органическая кожа, гладкие округлые лица,
  «лицо из пары пикселей», карикатура, 2D-спрайт, сросшиеся конечности.

Сравнительный тест (инструкция и критерии — в `prompt_voxel_face_lock_v1.json`, §comparative_test):
орк → человек → эльф при одинаковом seed и одинаковой силе LoRA; проверить единство воксельного
языка при сохранении расовых отличий и читаемость лица при уменьшении до ~256 px по высоте.

**Чего НЕ сделано (честно):** региональное управление BODY/FACE и отдельный inpaint-корректор лица
не создавались — это следующий шаг при необходимости; дополнительные модели лица не ставились.

## 6. Совместимость с SDXL Turbo

LoRA обучена на `SDXL 1.0`; рабочий режим чекпоинта — turbo (8 шагов, CFG 2.0). Совместимость
по базовой архитектуре есть (SDXL LoRA применима к SDXL-производным), но на CFG 2 влияние LoRA
слабее — поэтому сила 0.6 выбрана как компромисс, а серия 0.4/0.8 предусмотрена именно для проверки.
**Фактическое качество не проверено:** генерация не выполнялась (чекпоинт ещё качается).
Если на turbo стиль «плывёт» или лицо теряет детализацию — по ТЗ фиксируется необходимость
испытания на обычном (не-turbo) SDXL.

## 7. Координация загрузок

- `fetch_style04_models.ps1` **не перезапускался** и его активные файлы не изменялись
  (продолжают работать процессы, запущенные в предыдущей сессии: 12 диапазонов чекпоинта).
- LoRA качалась **отдельным** экземпляром `fetch_checkpoint_parallel.ps1` со своими
  `<файл>.parts` — конфликта нет.
- Приоритет по пропускной способности у DreamShaper XL: LoRA небольшая (218 MiB) и завершится
  первой; после неё освободившиеся соединения не добавляются к чекпоинту (его набор не менялся).

## 8. Итоговый статус

**`READY_FOR_OWNER_TEST`** — новый workflow и LoRA готовы к запуску владельцем.

| Проверка | Результат |
|---|---|
| Метаданные Civitai (версия, тип, размер, trigger, лицензия) | **PASS** (§2) |
| Загрузка `ral-vxl.safetensors` | **PASS** — 228 453 420 B, путь `D:\programs\ComfyUI\ComfyUI\models\loras\ral-vxl.safetensors` |
| **SHA-256** | **PASS** — вычисленный `9dc480ee98c12a499bee1fc19ee0e13969a3c6b14be0e6ec133a11d563e83a25` совпал с опубликованным Civitai |
| Структура файла | kohya-формат: 2958 тензоров (2166 UNet + 792 TE1), `.alpha` + `lora_down/up` — обычная SDXL-LoRA |
| Обнаружение в ComfyUI | **PASS** — `LoraLoader.lora_name = ["ral-vxl.safetensors"]` (живой сервер, `/api/object_info/LoraLoader`) |
| Серверная валидация новых workflow | **PASS** — `DREAMSHAPER_XL_CIVITAI_VOXEL.json` (орк), `..._HUMAN.json`, `..._ELF.json`: 8 нод в prompt, output `SaveImage` |
| Конфликты с существующими нодами | нет (`LoraLoader` — core-нода; импорт custom nodes без ошибок) |
| Fictiverse Voxel XL | сохранён, не заменялся (`DREAMSHAPER_XL_PIXEL_STYLE.json` ждёт его веса — единственная причина его FAIL в валидации) |
| Совместимость с SDXL Turbo на практике | **NOT_TESTED** — чекпоинт `DreamShaperXL_Turbo_v2_1.safetensors` ещё докачивается (1.9 ГБ из 6.9 ГБ), поэтому **генерация не объявляется проверенной** |
| Лицензия | **BLOCKER для производства** — `allowCommercialUse=RentCivit`, `allowDerivatives=False` (`FACT-D21`); для эксперимента разрешено, в релиз без разрешения автора нельзя |

**Важно про валидацию:** граф проверялся серверным `validate_prompt` с **подставленным** SD1.5-чекпоинтом
(флаг `--substitute-ckpt`), потому что SDXL-чекпоинт ещё не готов. Это доказывает связность графа,
корректность нод/widget'ов и наличие LoRA, но **не** доказывает качество на SDXL — для этого нужен
реальный прогон после загрузки чекпоинта.

**Что должен сделать владелец:**

```text
1. Дождаться завершения fetch_style04_models.ps1 (DreamShaper XL). Если процесс остановлен:
   powershell -NoProfile -ExecutionPolicy Bypass -File `
     "D:\Projects\CubeGame Factory\scripts\comfyui\fetch_style04_models.ps1" -Connections 12
2. Запустить D:\Projects\CubeGame Factory\scripts\comfyui\launch_dreamshaper_xl.bat
3. http://127.0.0.1:8188 → Workflow → Open →
   workflows\dreamshaper_xl\DREAMSHAPER_XL_CIVITAI_VOXEL.json   (орк, seed 424242, LoRA 0.6)
4. Queue → оценить лицо/тело; затем повторить с силой LoRA 0.4 и 0.8
5. Затем ..._HUMAN.json и ..._ELF.json с тем же seed — сравнительный тест рас
6. Зафиксировать RAM/VRAM пики в Crystools
```

Известные ограничения на момент отчёта: SDXL-чекпоинт не готов (генерация не проверена);
лицензия LoRA запрещает коммерческое использование выходов; лицо/тело управляются только текстом
(региональный контроль и inpaint-корректор не реализованы).
