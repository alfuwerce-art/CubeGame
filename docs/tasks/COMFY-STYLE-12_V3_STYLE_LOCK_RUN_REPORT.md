# COMFY-STYLE-12 — MANNEQUIN V3 STYLE LOCK (визуальный эталон → ComfyUI)

**STATUS: READY_FOR_OWNER_VISUAL_REVIEW (геометрия V3 воспроизведена, стиль рендера — с оговорками)**  
**Дата:** 17–18 ноября 2026  
**Контур:** `D:\Projects\CubeGame Factory` / локальный ComfyUI 0.45.0. Основной CubeGame, Unity и Alpha не тронуты.

Владелец утвердил новый визуальный эталон: **HUMAN MALE — MANNEQUIN V3** и **HUMAN FEMALE —
MANNEQUIN V3**. Он заменяет предыдущие варианты и является единственным эталоном для этого
этапа. Решение `MANNEQUIN_V3_APPROVED` / `MANNEQUIN_V3_REWORK` принимает только владелец.

Кандидаты на просмотр (равный масштаб с эталоном):

| Пол | Кадр | Сравнение |
|---|---|---|
| Мужской | `outputs/factory/mannequin_v3/run_male_diag_torso/MANNEQUIN_V3_MALE_TR_00001_.png` | `outputs/factory/mannequin_v3/review_male.png` |
| Женский | `outputs/factory/mannequin_v3/run_female_diag_torso/MANNEQUIN_V3_FEMALE_TR_00001_.png` | `outputs/factory/mannequin_v3/review_female.png` |
| Пластины управления | — | `outputs/factory/mannequin_v3/review_guides.png` |
| Ранние диагностики (плоская фигура) | — | `outputs/factory/mannequin_v3/review_flat_diagnostics.png` |

## 1. Шаг 1 — эталон зарегистрирован

| Параметр | Значение |
|---|---|
| Файл | `outputs/factory/mannequin_v3/REFERENCE_MANNEQUIN_V3_OWNER.png` (локально, не публикуется) |
| SHA-256 | `b95e4666a3b3caae543e3ba4663cce8d7f9ad3d026f381cca2231fee04d841c5` |
| Размер | 1774 × 887 px, PNG; совпадает побайтово с утверждённым изображением владельца |
| Содержимое | два манекена V3 (male слева, female справа) + подписи |
| Использование | только визуальная проверка и построение технических направляющих; исходный Cube World/Alpha не задействован |

## 2. Шаг 2 — измеренные пропорции

Измерения сделаны по фронтальному 3D-рендеру (`scripts/comfyui/measure_mannequin_v3_reference.py`,
карточка `measurements/comfy_style_12/mannequin_v3/REFERENCE_MEASUREMENT.json`, оверлей
`outputs/factory/mannequin_v3/REFERENCE_MANNEQUIN_V3_LANDMARKS.png`). По одному фронтальному
рендеру точные 3D-размеры не определяются, поэтому все числа — **предварительные проектные
параметры**, а не измеренная геометрия.

| Признак | Мужской | Женский | Комментарий |
|---|---:|---:|---|
| Высота фигуры на рендере, px | 658 | 642 | одна камера |
| Блок головы (crown→подбородок) / высота | **0.316** | **0.313** | «около одной трети» подтверждено |
| Ширина головы / высота | 0.342 | 0.326 | головы практически одного размера |
| Уши: выступ в сторону, px | 39.5 | 38 | отдельные кубические уши |
| Уши: полоса по высоте фигуры | 0.146–0.254 | 0.140–0.243 | ниже середины головы |
| Шея, ширина, px | 120 (0.182) | 106 (0.165) | короткая, в тени рендера |
| Грудная клетка, ширина / высота | 0.251 | 0.218 | у женского уже |
| Талия, ширина / высота | 0.167 | 0.148 | лёгкое сужение |
| Таз, ширина / высота | 0.251 | 0.232 | компактный |
| Размах рук / высота | 0.825 | 0.752 | плечи женского уже |
| Полоса рук по высоте | 0.374–0.492 | 0.369–0.483 | горизонтальная T-pose |
| Рука: кисть / локоть / плечо, px | 70 / 57 / 52 | 67 / 63 / 58 | одна модульная система |
| Бедро, ширина / высота | 0.100 | 0.092 | короткие массивные ноги |
| Длина ноги (пах→подошва) / высота | 0.307 | 0.315 | ноги короче половины роста |
| Ступня, ширина, px | 85 | 77 | ступень «голень→ступня» |

Измеренные артефакты прошлого этапа в эталоне отсутствуют: над макушкой ничего нет, ободка/
причёски нет, лицо пустое, ушей-деталей нет. Это отдельно проверено по увеличенным кропам.

## 3. Шаг 3 — направляющие

Построены **две независимые** геометрические направляющие на фиксированных пропорциях V3
(не на старых BASE_BODY_V1/V2):

| Пластина | Назначение | SHA-256 |
|---|---|---|
| `HUMAN_MALE_MANNEQUIN_GUIDE` | детерминированная блочная пластина: силуэт V3 + внутреннее кольцо 6 px | `89c5098245ef416d9f183fbd4a30241817da2d8e7f3512590ddde1c472fa4850` |
| `HUMAN_FEMALE_MANNEQUIN_GUIDE` | то же для женского профиля | `cacc80819926b4ee90bf85b59727ad9f660881a1e44d0ba02cca855365d26527` |
| `HUMAN_MALE_MANNEQUIN_DEPTH` | глубина блочного стека (ветка `depth` union-ControlNet) | `7478cf40d05431d6869687d70711b827b7be8b19f84b3a07bbcd4d1a3a188984` |
| `HUMAN_FEMALE_MANNEQUIN_DEPTH` | то же | `72f598feadbe6e2d8a087c4d227da06fb321f9643b525366e1654b9b293b1cf5` |
| `HUMAN_MALE_MANNEQUIN_REFPLATE` | эталонный рендер, вырезанный по измеренному контуру, тот же кадр (crown 174, sole 850) | `6bccf92d95f63af979c59b5ebf737f24f86f740b54f2b3f25d6ef3e6584c9ced` |
| `HUMAN_MALE_MANNEQUIN_REFPLATE_TORSO` | + измеренные границы торса (грудь/талия/таз) | `b857599faddf4683df2812b972e57d090fc91d83bd460a8e4cb2568743aff907` |
| `HUMAN_FEMALE_MANNEQUIN_REFPLATE` | то же для женского | `6f73a4381334229b3ac1a910d19d5068cc71a2aa651543c1c70a04333f4189fc` |
| `HUMAN_FEMALE_MANNEQUIN_REFPLATE_TORSO` | + границы торса | `70773ed57eb0de29018f5a4edba7ed329e81130b1fe8f9a6539b16661983f94f` |

Блочные пластины: одна фигура, симметрия по x = 383.5 без ошибки (`symmetry_error_px = 0`),
одна связная маска (`mask_components = 1`), голова 31.7 % / 31.3 % высоты фигуры. Генераторы:
`scripts/comfyui/prepare_mannequin_v3_guides.py`, `scripts/comfyui/prepare_mannequin_v3_ref_plate.py`;
карточки `measurements/comfy_style_12/mannequin_v3/{GUIDES.json,REFERENCE_PLATES.json}`.

## 4. Шаг 4 — генерация (стек не менялся)

Стек ровно из ТЗ: `sd_xl_base_1.0.safetensors` + `VoxelXL_v1.safetensors` (Fictiverse) 0.60/0.60 +
`controlnet-union-sdxl-1.0.safetensors` (canny/lineart, 0.60), 768×1024, 28 шагов, CFG 6.0,
`dpmpp_2m`/`karras`, seed `20261117`, `EmptyLatentImage`, без IPAdapter и img2img. Новых весов не
скачивалось. Каждый шаг менял **один** фактор.

| # | Кадр | Пластина | CN | depth | Время, с | Пик RSS, МБ | VRAM пик−старт, МБ | Мин. своб. RAM, ГБ | Наблюдение |
|---|---|---|---:|---|---:|---:|---:|---:|---|
| 1 | `MANNEQUIN_V3_MALE_RUN1` | блочная | 0.60 | — | 65.1 | 13 714 | 1715 | 4.71 | плоская «вырезанная» фигура: силуэт верный, объёма нет |
| 2 | `MANNEQUIN_V3_MALE_PV1` | блочная | 0.60 | — | 46.1 | 13 680 | 2065 | 5.25 | тот же дефект при старом (V1) промпте → причина в пластине, не в тексте |
| 3 | `MANNEQUIN_V3_MALE_DEPTH55` | блочная | 0.60 | 0.55 | 52.2 | 13 703 | 2048 | 4.93 | объём появился, но части читаются как наклеенные плитки |
| 4 | `MANNEQUIN_V3_MALE_REFPLATE` | ref-пластина (сырой кроп) | 0.60 | — | 52.2 | 13 698 | 2083 | 5.05 | фигура V3 воспроизведена, но в кадр попала прямоугольная «стена» фона |
| 5 | `MANNEQUIN_V3_MALE_REFCLEAN` | ref-пластина (фон очищен) | 0.60 | — | 46.2 | 13 674 | 2083 | 5.14 | объём и блоки V3; таз/талия читаются слабее |
| 6 | `MANNEQUIN_V3_MALE_CLEAN_D55` | ref clean | 0.60 | 0.55 | 55.2 | 13 715 | 2066 | 4.69 | объём + таз; альтернатива |
| 7 | `MANNEQUIN_V3_FEMALE_RUN1` | ref clean Female | 0.60 | — | 48.1 | 13 698 | 2095 | 5.10 | торс сжался в тонкую колонну: грудь слилась с полосой рук |
| 8 | `MANNEQUIN_V3_FEMALE_CN75` | ref clean Female | **0.75** | — | 46.1 | 13 686 | 2121 | 5.10 | не исправило: тот же сжатый торс |
| 9 | `MANNEQUIN_V3_FEMALE_REINFORCED` | ref + все границы блоков | 0.60 | — | 45.2 | 13 674 | 1975 | 4.70 | грудь появилась, но фигура «плоские плитки», ноги вытянуты |
| 10 | `MANNEQUIN_V3_FEMALE_REINF_D55` | ref reinforced | 0.60 | 0.55 | 53.2 | 13 693 | 2139 | 4.97 | геометрия верная, стиль — плоские плитки |
| 11 | `MANNEQUIN_V3_FEMALE_REINF_D35` | ref reinforced | 0.60 | 0.35 | 58.2 | 13 693 | 2086 | 4.58 | без изменений → «плитки» даёт пластина, не глубина |
| 12 | `MANNEQUIN_V3_FEMALE_CLEAN_D55` | ref clean Female | 0.60 | 0.55 | 55.2 | 13 693 | 2068 | 4.50 | грудь+талия+таз есть, стиль плоский |
| 13 | `MANNEQUIN_V3_FEMALE_SEALED_D55` | ref sealed | 0.60 | 0.55 | 55.2 | 13 717 | 2081 | 4.63 | грудь и таз есть, стиль плоский |
| 14 | **`MANNEQUIN_V3_FEMALE_TORSO`** | ref + **только** границы торса | 0.60 | — | 45.2 | 13 693 | 2053 | 3.84 | **выбрано:** объём + грудь/талия/таз; дефекты ниже |
| 15 | **`MANNEQUIN_V3_MALE_TORSO`** | ref + только границы торса | 0.60 | — | 46.4 | 13 699 | 2069 | 4.72 | **выбрано:** объём + таз/талия, тот же рецепт, что у женского |

Графы: `workflows/character_factory/mannequin/MANNEQUIN_V3_MALE.json` (SHA-256
`af2580e66174e6779ddb4090fc965ad2d5f2a5fd798e35b6ec8a53a4bb1abc58`, 14 нод) и
`MANNEQUIN_V3_FEMALE.json` (SHA-256 `145d9ffda90600640ee1e0fe61065c56d1d85ea08b10a7d1b05b8171d02ef752`,
14 нод). Оба проверены против живого `/object_info`: 14 нод, 0 проблем. Все классы — штатные ноды
ComfyUI 0.45.0; сервер поднят с `--disable-all-custom-nodes --offline --novram --reserve-vram 5`,
готовность 14.4 с.

## 5. Шаг 5 — визуальная проверка кандидатов

Сравнение в одинаковом масштабе: `review_male.png`, `review_female.png` (эталон V3 | кандидаты).

| Критерий ТЗ | Мужской (кадр 15) | Женский (кадр 14) |
|---|---|---|
| Крупная квадратная голова, ровная макушка | да, ~1/3 высоты | да, ~1/3 высоты |
| Нет выступа/ободка/причёски над макушкой | да | да |
| Ступенчатый подбородок | да | да |
| Отдельные кубические уши, размер/положение | да, низкие боковые кубы | да, но тоньше эталона |
| Пустое лицо, без глаз/рта/волос | да | да |
| Широкие плечи / компактная грудь | да | плечи уже мужского |
| Умеренно узкое туловище, узкая талия | да | талия уже мужской |
| Узкий/компактный таз | да | да |
| Женская грудь выражена умеренно | н/п | **частично:** есть грудной блок, но две отдельные грудные планки не разделились, в центре тёмная выемка |
| Короткие массивные ноги, сегменты бедро/голень/ступня | да, но бедро тоньше эталона (~0.07 против 0.10 высоты) | да, но ноги тоньше эталона |
| Блочные кисти, отдельные сегменты рук | да (кисть, локоть, плечо) | да |
| Симметричная T-pose, руки горизонтально | да | да |
| Один персонаж, полный рост, без обрезки | да | да |
| Нет одежды/обуви/брони/оружия/аксессуаров | да | да |
| Один нейтральный серый материал | да | да |
| Объём, а не плоская фигура | да | да |
| Единые масштаб/ориентиры для риггинга | да: crown 174 / sole 850, ось 383.5 в обоих кадрах | да |

Честные дефекты, которые владелец увидит в сравнении:

1. **Бёдра/голени тоньше эталона** у обоих кандидатов (манекен выглядит легче, чем V3).
2. **Женская грудь**: вместо двух отдельных грудных планок читается один блок с тёмной выемкой
   по центру.
3. **Уши** у женского кадра тоньше, чем в эталоне.
4. Ступни у мужского кадра чуть уже эталонных.

## 6. Бюджет памяти (карточка §4.1 AGENTS)

| Параметр | Факт (15 кадров) | Ориентир | Вердикт |
|---|---|---:|---|
| Пик RSS ComfyUI | 13.67–13.72 ГБ | ≤ 15 ГБ | в пределах |
| Прирост VRAM на кадр (вся карта) | **1.72–2.14 ГБ** | ≤ +2 ГБ | 12 из 15 кадров выше на 48–139 МБ → бюджетная часть `PARTIAL` |
| Минимум свободной RAM | 3.84–5.25 ГБ | без подкачки/OOM | OOM и подкачки не было |
| Время кадра | 45.2–65.1 с | — | первый кадр дольше (загрузка весов) |
| Холодный старт сервера | 14.4 с | — | — |

Измерения: `measurements/comfy_style_12/mannequin_v3/*.manifest.json` (psutil RSS процесса +
pynvml по всей карте, т.к. WDDM не отдаёт VRAM процесса — известная ловушка F12).

## 7. Что доказано, а что нет

**Доказано (реальным запуском):**

- новый эталон зарегистрирован и побайтово совпадает с утверждённым;
- пропорции V3 измерены, и по ним построены две независимые направляющие с нулевой асимметрией;
- причина плоского «вырезанного» результата локализована: двухтоновый блочный силуэт без
  внутренних рёбер (кадры 1–2 дают одинаково плоскую фигуру при двух разных промптах);
- эталонный рендер сам по себе является рабочим ControlNet-входом: пропорции и блочная
  структура V3 воспроизводятся (кадры 4–5, 14–15);
- одной и той же рецептурой (`ref-пластина + измеренные границы торса`, CN 0.60) получены оба
  манекена в одном кадре/масштабе.

**Не доказано / ограничения:**

- это визуальный вход-кандидат, пригодность для Tripo не проверялась;
- толщина бёдер и разделение грудных планок отличаются от эталона (см. §5);
- пластина-эталон несёт освещение исходного рендера; «плоский» стиль появляется, если
  дополнительно обвести **все** блоки (кадры 9–11) — это зафиксировано, чтобы не повторять;
- числовая проверка пропорций **сгенерированных** кадров не выполнена: попытка выделить силуэт
  по градиенту дала мусорную маску (низкий контраст студийного фона), скрипт удалён, чтобы не
  публиковать нерабочий инструмент. Проверка кадров — визуальная, по §5.

## 8. Воспроизведение

```powershell
# 1. сервер (RAM-first, без custom nodes)
$env:PYTHONUTF8='1'; $env:PYTHONIOENCODING='utf-8'
Start-Process 'D:\programs\ComfyUI\python_embeded\python.exe' `
  -ArgumentList '-s','D:\programs\ComfyUI\ComfyUI\main.py','--windows-standalone-build',
  '--disable-auto-launch','--port','8188','--novram','--reserve-vram','5','--disable-assets',
  '--disable-all-custom-nodes','--offline' -WorkingDirectory 'D:\programs\ComfyUI' -PassThru

# 2. измерения эталона, пластины, графы
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\measure_mannequin_v3_reference.py
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\prepare_mannequin_v3_guides.py --canny-preview
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\prepare_mannequin_v3_ref_plate.py --reinforce-torso
Copy-Item outputs\factory\mannequin_v3\HUMAN_MALE_MANNEQUIN_REFPLATE_TORSO.png D:\programs\ComfyUI\ComfyUI\input\
Copy-Item outputs\factory\mannequin_v3\HUMAN_FEMALE_MANNEQUIN_REFPLATE_TORSO.png D:\programs\ComfyUI\ComfyUI\input\
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\compile_mannequin_v3.py --figure male `
  --guide HUMAN_MALE_MANNEQUIN_REFPLATE_TORSO.png --validate http://127.0.0.1:8188
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\compile_mannequin_v3.py --figure female `
  --guide HUMAN_FEMALE_MANNEQUIN_REFPLATE_TORSO.png --validate http://127.0.0.1:8188

# 3. кадры и замеры
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\run_mannequin_v1.py `
  --url http://127.0.0.1:8188 --api workflows\character_factory\mannequin\MANNEQUIN_V3_MALE.json `
  --server-pid <PID> --out outputs\factory\mannequin_v3\run_male_diag_torso `
  --report measurements\comfy_style_12\mannequin_v3\MANNEQUIN_V3_MALE_TORSO.manifest.json `
  --case MANNEQUIN_V3_MALE_TORSO `
  --guide-local outputs\factory\mannequin_v3\HUMAN_MALE_MANNEQUIN_REFPLATE_TORSO.png `
  --guide-server D:\programs\ComfyUI\ComfyUI\input\HUMAN_MALE_MANNEQUIN_REFPLATE_TORSO.png

# 4. сравнение
D:\programs\ComfyUI\python_embeded\python.exe scripts\comfyui\compose_mannequin_v3_review.py
```

Манифест каждого кадра пишет: API JSON и его SHA-256, все параметры сэмплирования, SHA-256
checkpoint/LoRA/ControlNet, SHA-256 пластины (локально и в `input` сервера с проверкой совпадения),
SHA-256 результата, время, пики RAM/VRAM, версию ComfyUI/PyTorch/устройство.

## 9. Следующий шаг (после решения владельца)

- **`MANNEQUIN_V3_APPROVED`** → фиксирую рецептуру как эталон этапа, переношу её в
  `03_FACTORY_PROJECT_MEMORY.md` как производственную, и только тогда обсуждается следующий шаг
  (другие ракурсы / раса / Tripo — по отдельному разрешению).
- **`MANNEQUIN_V3_REWORK`** → минимальный проверяемый вариант: усилить бёдра/голени в
  ref-пластине до измеренных значений (бедро 0.10 высоты у мужского, 0.092 у женского) и явно
  разделить две грудные планки у женского; стек, seed и ControlNet не трогать.
