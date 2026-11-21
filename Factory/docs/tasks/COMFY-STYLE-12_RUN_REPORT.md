# COMFY-STYLE-12 — MANNEQUIN_V1, первый Human Male FRONT T-pose

**READY_FOR_OWNER_VISUAL_REVIEW · 17 ноября 2026**
Решение `MANNEQUIN_APPROVED` / `MANNEQUIN_REWORK` принимает только владелец.

Кандидат на просмотр: `outputs/factory/mannequin_v1/run_diag3/MANNEQUIN_V1_DIAG3_00001_.png`
(одна фигура, полный рост, пустая голова, один серый материал). Сравнение четырёх кадров:
`outputs/factory/mannequin_v1/MANNEQUIN_V1_COMPARISON.jpg`.

## 1. Что сделано

Первый и единственный основной прогон выполнен строго с параметрами раздела 6 ТЗ, затем — три
диагностических варианта с тем же seed. Все четыре кадра получены на одном сервере, одной
конфигурации и одном seed `20261117`; отличие только во входной технической guide-картинке.

| Случай | Guide | Время, с | Пик RSS, МБ | VRAM до / пик, МБ | Пик − старт, МБ | Свободно RAM, ГБ | Наблюдение |
|---|---|---:|---:|---:|---:|---:|---|
| PRIMARY | V1 planes | 64.0 | 13 635 | 1619 / 3710 | 2091 | 5.37 | Объём и воксельность есть, но тело распалось на отдельные висящие плиты |
| DIAG1 | V2 solid | 43.1 | 13 654 | 1700 / 3769 | 2069 | 6.07 | Отклонён: тонкий контур слишком слаб — ушли в гладкого манекена с лицом и руками вниз |
| DIAG2 | V3 stepped ring | 42.2 | 13 657 | 1698 / 3767 | 2069 | 5.92 | T-pose, пустая голова и серый материал держатся, но ступенчатый силуэт снова режет тело на плиты |
| DIAG3 | V4 approved+ring | 43.1 | 13 657 | 1694 / 3767 | 2073 | 5.93 | **Кандидат:** цельная блочная фигура, T-pose, пустая голова, один серый материал |

Пик RSS — процесс ComfyUI (psutil). VRAM — занято всей картой (pynvml): на Windows/WDDM
память процесса недоступна (ловушка F12), поэтому дельта включает рабочий стол и чужие приложения.
До запуска сервера: VRAM 1300 MiB, свободно RAM 18.26 ГБ. После остановки сервера: VRAM 1293 MiB,
свободно RAM 19.36 ГБ.

## 2. Стек и параметры

| Параметр | Значение |
|---|---|
| Checkpoint | `sd_xl_base_1.0.safetensors`, 6 938 078 334 Б, SHA-256 `31e35c80…7e5b` |
| LoRA | `VoxelXL_v1.safetensors` (Fictiverse), 912 554 516 Б, SHA-256 `7d9a5f11…8b5c`, model/clip 0.60 / 0.60 |
| ControlNet | `controlnet-union-sdxl-1.0.safetensors`, 2 512 030 408 Б, SHA-256 `a9e13fd6…c746`, union `canny/lineart/anime_lineart/mlsd`, сила 0.60, интервал 0.0–1.0 |
| Canny | 0.35 / 0.75 (как в COMFY-STYLE-11B) |
| Латент | `EmptyLatentImage` 768×1024, batch 1 |
| Сэмплер | `dpmpp_2m` / `karras`, 28 шагов, CFG 6.0, denoise 1.0, seed `20261117` |
| IPAdapter / img2img / ral-vxl | не подключены / не подключены / не использован |
| Новые загрузки | нет |

Промпты — дословно разделы 7 и 8 ТЗ (positive/negative), без правок и без добавления
триггера LoRA: это единственный прогон этапа, где сравнивается только геометрия управления.

## 3. Граф

`workflows/character_factory/mannequin/MANNEQUIN_V1.json` — 14 нод, формат **API** (отправляется
в `/prompt` как есть, поэтому позиционный порядок виджетов UI-формата не может испортить параметры).
Граф побайтово совпадает с `measurements/comfy_style_12/MANNEQUIN_V1_DIAG3.api.json`, кроме
`filename_prefix`. SHA-256 графа: `3028c5fa…a28f`.

Ноды: `CheckpointLoaderSimple → LoraLoader → CLIPTextEncode ×2`, `EmptyLatentImage`,
`LoadImage → ImageScale → Canny → ControlNetApplyAdvanced` (через `ControlNetLoader` и
`SetUnionControlNetType`) → `KSampler → VAEDecode → SaveImage`. Все 14 классов — штатные ноды
ComfyUI 0.45.0: custom nodes не требуются, сервер поднят с `--disable-all-custom-nodes --offline`.

Проверка графа до запуска: `compile_mannequin_v1.py --validate` сверяет каждый класс, каждый
вход и каждое COMBO-значение с живым `/object_info` — 14 нод, 0 проблем (4/4 прогона).

## 4. Почему менялась только guide-картинка

ТЗ §5 разрешает подготовить детерминированную техническую guide-картинку с крупными
плоскостями/ступенями тела и пустой головой; она остаётся только control input. Оказалось, что
именно плотность краёв, а не тон, определяет силу управления: ControlNet получает **бинарную**
карту Canny, поэтому цвет и заливка guide не переносятся, а переносится только топология краёв.

| Пластина | Вариант | Что даёт в Canny | Итог |
|---|---|---|---|
| `MANNEQUIN_GUIDE_V1_PLANES.png` `88dd3192…d29` | per-part плоскости (верх/бок/низ каждой части) | контур + прямоугольник вокруг каждой части | объём и воксельность высокие, но каждая часть стала отдельным объектом → «висящие плиты» |
| `MANNEQUIN_GUIDE_V2.png` `571e4fb8…de7` | только ступенчатый силуэт | один тонкий контур | управление слишком слабое: T-pose потерян, появились лицо, волосы, руки вниз |
| `MANNEQUIN_GUIDE_V3.png` `b22a7527…ba1` | ступенчатый силуэт + внутреннее кольцо 6 px | двойной контур по всему телу | T-pose и пустая голова держатся, но ступени силуэта всё ещё читаются как отдельные блоки |
| `MANNEQUIN_GUIDE_V4.png` `269d974d…ead` | утверждённый силуэт COMFY-STYLE-11B + кольцо 6 px | двойной контур без внутренних прямоугольников | **цельная блочная фигура** |

V1 сохранён побайтово (`MANNEQUIN_GUIDE_V1_PLANES.png`, SHA совпадает с использованным в
PRIMARY). V2–V4 воспроизводятся генератором
[`prepare_mannequin_guide_v1.py`](../../scripts/comfyui/prepare_mannequin_guide_v1.py)
с `--variant solid|ring|mask_ring`; силуэт V4 — существующая пластина
`outputs/factory/human_base_front_v2/HUMAN_MALE_FRONT_POSE_SILHOUETTE.png` (SHA `c849c7d5…bca`),
то есть поза и пропорции не изобретены заново.

## 5. Критерии раздела 11 ТЗ по кандидату DIAG3

| № | Критерий | Результат |
|---:|---|---|
| 1 | Одна фигура, полный рост | да |
| 2 | Строгий FRONT | да; у головы есть тонкая боковая накладка слева — артефакт, не второй объект |
| 3 | T-pose сохранена | да, руки горизонтальны; на запястьях небольшой ступенчатый перепад |
| 4 | Голова пустая: без глаз, рта, волос, бороды | да |
| 5 | Нет одежды, обуви, брони, оружия, аксессуаров | да |
| 6 | Всё тело одного нейтрального серого материала | да, только мягкая студийная светотень |
| 7 | Форма не выглядит плоской бумажной фигурой | да, читаются объём, боковые и верхние плоскости |
| 8 | Читаются крупные воксельные объёмы и плоскости | **частично**: фигура цельная и блочная, но поверхность сглажена — жёстких кубических переломов меньше, чем в PRIMARY |
| 9 | Кисти и стопы не обрезаны | да; под стопами тонкие пластины-подошвы |
| 10 | Нет второй фигуры / character sheet | да |
| 11 | Сохранены guide и все параметры воспроизведения | да (см. §7) |
| 12 | Результат показан владельцу | этот отчёт |

## 6. Бюджет памяти (карточка §4.1 AGENTS)

| Параметр | Факт | Ориентир | Вердикт |
|---|---|---|---|
| Пик RSS ComfyUI | 13.66 ГБ | ≤ 15 ГБ | в пределах |
| Минимум свободной RAM | 5.37–6.07 ГБ | без подкачки и OOM | OOM/подкачки не было |
| VRAM всей карты, до / пик | 1619–1700 / 3710–3769 МБ | — | — |
| Прирост VRAM на кадр | 2069–2091 МБ (+2.02…+2.04 ГБ) | ≤ +2 ГБ | **превышен на 21–45 МБ** |
| Время кадра (тёплый сервер) | 42.2–43.1 с | — | первый кадр 64.0 с (загрузка весов) |
| Холодный старт сервера | 255.4 с | — | медленнее прежних 146 с (полный скан нод без custom nodes) |

По ТЗ §9 превышение +2 ГБ нельзя называть PASS: бюджетная часть кадра помечена `PARTIAL`.
Причина — режим `--novram --reserve-vram 5` держит веса в RAM, а на карте живут активации
SDXL; измерение по всей карте включает рабочий стол. Скрытого уменьшения качества или
параметров не делалось.

## 7. Воспроизведение

```powershell
# 1. сервер (RAM-first, без custom nodes)
$env:PYTHONUTF8='1'; $env:PYTHONIOENCODING='utf-8'
Start-Process 'D:\programs\ComfyUI\python_embeded\python.exe' `
  -ArgumentList '-s','D:\programs\ComfyUI\ComfyUI\main.py','--windows-standalone-build',
  '--disable-auto-launch','--port','8188','--novram','--reserve-vram','5',
  '--disable-assets','--disable-all-custom-nodes','--offline' `
  -WorkingDirectory 'D:\programs\ComfyUI' -PassThru

# 2. guide и граф
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\prepare_mannequin_guide_v1.py `
  --out outputs\factory\mannequin_v1 --name MANNEQUIN_GUIDE_V4 --variant mask_ring `
  --silhouette outputs\factory\human_base_front_v2\HUMAN_MALE_FRONT_POSE_SILHOUETTE.png --canny-preview
Copy-Item outputs\factory\mannequin_v1\MANNEQUIN_GUIDE_V4.png D:\programs\ComfyUI\ComfyUI\input\
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\compile_mannequin_v1.py `
  --out workflows\character_factory\mannequin\MANNEQUIN_V1.json --guide MANNEQUIN_GUIDE_V4.png `
  --validate http://127.0.0.1:8188

# 3. прогон с замерами
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\run_mannequin_v1.py `
  --url http://127.0.0.1:8188 --api workflows\character_factory\mannequin\MANNEQUIN_V1.json `
  --server-pid <PID> --out outputs\factory\mannequin_v1\run_diag3 `
  --report measurements\comfy_style_12\MANNEQUIN_V1_DIAG3.manifest.json --case MANNEQUIN_V1_DIAG3 `
  --guide-local outputs\factory\mannequin_v1\MANNEQUIN_GUIDE_V4.png `
  --guide-server D:\programs\ComfyUI\ComfyUI\input\MANNEQUIN_GUIDE_V4.png
```

Манифест каждого кадра пишет: API JSON и его SHA-256, все параметры сэмплирования, SHA-256
чекпоинта/LoRA/ControlNet, SHA-256 guide (локально и в `input` сервера, с проверкой совпадения),
SHA-256 результата, время, пики RAM/VRAM, версию ComfyUI/PyTorch/устройство.

## 8. Что не сделано и следующий шаг

- Female, другие ракурсы и расы не запускались — запрещено до приёмки этого кадра.
- Пригодность для Tripo не утверждается: это визуальный вход-кандидат, не проверенный image-to-3D.
- Замечания к кандидату: сглаженная поверхность вместо жёстких кубических переломов, тонкая
  накладка у головы, пластины-подошвы под стопами.
- Лицензия Fictiverse VoxelXL — openrail; `ral-vxl` в производстве по-прежнему заблокирован
  (FACT-D21).

Если владелец даёт `MANNEQUIN_REWORK`, минимальный следующий вариант — тот же граф V4 при
LoRA 0.70 (или ControlNet 0.75) — то есть только штатные шаги раздела 6 ТЗ, без новой guide.
