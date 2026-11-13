# M0 P6 · Главное меню, редактор и игровой экран как в Alpha

- **Статус `READY_FOR_PRODUCER_REVIEW` / `PARTIAL` · 14 ноября 2026**
- **Ветка** `codex/m0-p6-alpha-menu-hud`, база P5 `01930ff`
- **Исполнитель** Claude Code (side-session по задаче продюсера)
- **Источник** `Cube.exe` SHA-256 `84a7a132…30bf`, `gui.plx`, `start.plx`, `data3.db` установки (read-only), реконструкция
  `.scratch/CubeWorld-Reversal`, собственный дизассемблер (capstone). Alpha **не запускалась**; для сверки
  использованы снимки оригинала из P1 (`.scratch/shots/ingame1.png`, `shot08_esc.png`, `shot01/03/04/05.png`).
- Alpha-ассеты (иконки, логотип) **не коммитятся**: они извлекаются в локальный кэш `.scratch/content/gui` и
  попадают в сборку через существующий стейджинг контента.

Задача продюсера: «сделай нормальное меню как в игре и так же во время игры убрать кнопки меню на весь экран;
экран во время игры, меню и редактор — как в оригинальной альфе».

## 1. Что найдено в Alpha

### 1.1 Формат `.plx` расшифрован — `ALPHA_CODE_STATIC`

- `GameController_load_widget_file` → `GameController_deserialize_widget_tree 0x6555D0`. Поток — блоки
  `token, u32 length, payload`; токен 0 определяет имя (`u32 id, u32 len, bytes`).
- После блока `Seal` имена зашифрованы `decodeScrambledBuffer 0x6880C0`:
  `out[(i + seed % n) % n] = in[i] − key[((i + seed % n) % n) & 7]`, `seed = u32(key[0..4])`.
- Ключ восстановлен известным открытым текстом (`Seal` обязан дать `PlasmaGraphics` — это и проверяет 0x6555D0):
  `5ccdc75e0b263afb`, совпадает на всех 14 байтах.
- Свойства — строки `Widget.bindPos`, `Node.transformation`, `SmoothMeshShape.vertexPositions` и т.д.
  (`Widget_readProperty`). Инструмент: `tools/alpha_research/plx_dump.py` (только читает и печатает).
- `SmoothMeshShape`: `flags 0x10` — готовая triangle strip (обводки букв); `flags 1/3` — контуры: `face` =
  (count, индексы…), `vertexFlags` 1 = точка на кривой, 2 = контрольная (квадратичные кривые как в TrueType);
  `Display.clipping = 1` — градиентная сетка, обрезанная дочерними фигурами.

`gui.plx` — это палитра шаблонов (hpbar, mpbar, abilitybutton, menubutton…). Экранные позиции задаёт код.

### 1.2 Позиции HUD (W/H — клиентская область, y вниз) — `ALPHA_CODE_STATIC` по байтам

| Элемент | Позиция pivot | Где |
|---|---|---|
| hpbar | (W/2 − 163, H − 90) | `GameController::vfunc_10` 0x492CC6…0x492CEA |
| mpbar | (W/2 + 8, H − 90) | 0x492A71 |
| staminabar / castbar | (W/2, H − 115) / (W/2, H − 150) | 0x492E3E / 0x49303C |
| experiencebar (панель персонажа) | (100, 75) | 0x490BC8 |
| crosshair | (W/2, H/2) | 0x4927DA |
| хотбар (7 клонов) | x = W/2 + 24·(2i − n) + 6, y = H − 50, шаг 48 | `vfunc_4` 0x48331F |
| кнопки меню (6) | центр x = W/2 + 30·(2i − n + 1), y = H − 140, шаг 60 | `vfunc_4` 0x483422 |
| окно SystemWidget (+0x800910) | по центру, 150×120 | `vfunc_4` 0x482F92 |

Шаблоны (`ALPHA_DATA`, plx): рамка бара 158×27, pivot в левом верхнем углу, белая обводка 2 px поверх чёрной
8 px, заливка с отступом 4 px, блик 12 % на верхней половине, подпись `HP/MP/XP` 10 px, значение 9 px; HP
`(1,0.21,0)`, MP `(0,0.52,1)`, XP `(0.63,0,1)`, выносливость `(0,0.98,1)`, каст `(0.68,0.61,1)`. Кнопка
способности 42×42 (рамка 2 px белая, обводка 4 px чёрная), подпись 10 px; кнопка меню 48×48 — спрайт с
подписью клавиши снизу, при наведении масштаб 1.5 (0x48EC02).

Подписи хотбара `M1, M2, 1…4, Q` (0x70088C, 0x700894, `i − 1`, 0x70089C). Кнопки меню в порядке
help / skills / crafting / inventory / worldmap / system с подписями `F1, X, C, B, M, O` (0x7008C4…0x700924).
Таблица action id → иконка восстановлена из конструктора `GameController` (67 записей, `slam.png`,
`attack-left.png`, `smash.png`…), иконки лежат в `data3.db` (104 PNG, расшифровка `cub_format.deobfuscate`).

### 1.3 Клавиши в игре — `ALPHA_CODE_STATIC` (0x47E1B0, символ = индекс + 9)

| Клавиша | Действие Alpha |
|---|---|
| Esc | 0x47E770: переключить GUI-флаг `+0x8008F2` (появляются 6 кнопок меню, мышь у GUI), закрыть карту |
| O | 0x488D00: окно системы (Options / Start Menu / Exit Game) |
| B, I | 0x488C00: инвентарь |
| C | 0x488BD0: крафт |
| X | 0x488C70: навыки |
| M | флаг карты `+0x8006E4` |
| Tab, V, F, G | флаги 0x800A40, 0x8007B4, бит существа, … (не портируются в P6) |

**Исправление прежней интерпретации P0:** Esc в Alpha не открывает системное меню — это делает O или кнопка `O`
в ряду меню. Снимок `shot08_esc.png` подтверждает ряд из 6 иконок после Esc.

### 1.4 Редактор и создание мира — `ALPHA_CODE_STATIC` (`vfunc_4` 0x482E71…0x48324C, CharacterStyleWidget)

- Back 200×40 в (20, 20); панель внешности 300×330 в (20, H − 350).
- Строки Race / Gender / Class / Face / Haircut: подпись x = 15, y = 27 + 30·i, 12 px; стрелки в x = 100 и у
  правого края; значение по центру между ними (0x428E40, значения `Face n`, `Haircut n`).
- Палитра волос — сетка 16×7 в квадрате 280×150 в (10, 200).
- Поле имени по центру (W/2, H − 50); «Create Character» 300×40 в правом нижнем углу.
- Создание мира: поле seed (W/2, H − 100), имя (W/2, H − 50), «Create World» в правом нижнем углу.

### 1.5 Стартовый экран — `ALPHA_DATA`

Логотип `CUBE WORLD α` и `Picroma ©2013` — векторные узлы `cubeworld` и `picroma` в `start.plx` (а не текстуры).

## 2. Что сделано в Unity

| Блок | Файлы | Статус |
|---|---|---|
| Извлечение UI-иконок `data3.db` → `.scratch/content/gui` + каталог без пикселей | `tools/alpha_content/extract_gui_icons.py`, `tools/alpha_content/data/gui_icon_catalog.json` | `ALPHA_DATA` |
| Растеризация логотипа `start.plx` в локальный кэш | `tools/alpha_content/render_start_logo.py` | геометрия `ALPHA_DATA`, растеризатор `EMULATED_ALPHA_CODE` |
| Стейджинг папки `gui` (`.png`) в сборку | `AlphaContentStaging.cs`, `AlphaContentInventory.GuiFolder` | `CUBEGAME_CONTRACT` (как звуки) |
| Загрузка иконок, таблица action → icon | `AlphaGuiIcons.cs` | `ALPHA_CODE_STATIC` |
| HUD: HP/MP внизу по центру, хотбар 7 слотов, панель персонажа (имя, `LVL n CLASS`, HP, XP), время, прицел | `GameBootstrap.AlphaHud.cs`, `GameInterface.uss` | позиции `ALPHA_CODE_STATIC`, вид `ALPHA_DATA` + `ALPHA_CAPTURE` |
| Постоянная панель кнопок CubeGame удалена; ряд из 6 кнопок меню только после Esc | `GameBootstrap.AlphaHud.cs`, `GameBootstrap.Interface.cs` | `ALPHA_CODE_STATIC` |
| Клавиши Esc/O/B/I/C/X/M, F1; J журнал | `GameBootstrap.Interface.cs` | Alpha; J — `CUBEGAME_CONTRACT` |
| Крафт — отдельное окно (C); инвентарь с панелью валют | `GameBootstrap.Interface.cs` | `PARTIAL` (окна не повторяют раскладку Alpha) |
| Системное окно и Options без полноэкранного затемнения, мир виден | `GameBootstrap.AlphaMenus.cs`, USS | `ALPHA_CODE_STATIC` (150×120 по центру) |
| Редактор персонажа по раскладке Alpha | `GameBootstrap.AlphaMenus.cs`, USS | `ALPHA_CODE_STATIC` |
| Создание мира по раскладке Alpha | то же | `ALPHA_CODE_STATIC` |
| Логотип и Picroma на стартовом экране | то же | `ALPHA_CAPTURE` позиция |
| Диагностика `-cubeUiScreen` для снимков Player | `GameBootstrap.Diagnostic.cs` | инструмент проверки |

Окна панелей (инвентарь, навыки, сервисы, карта, журнал) получили стиль Alpha-окна (тёмная заливка, белая
рамка 2 px, шрифт `resource1.dat`), но их внутренняя раскладка — CubeGame.

## 3. Проверки

TESTS_PLACEHOLDER

## 4. Открытые долги (не считать закрытыми)

- Фон стартовых экранов: Alpha рисует живой мир за меню; в порте чёрный фон — `PLACEHOLDER_NOT_ALPHA`
  (какой мир Alpha показывает при холодном старте — `UNKNOWN`).
- Портрет головы персонажа в левом верхнем углу, TEMP/HUM, компас, название региона и мини-карта справа
  вверху — не портированы (`PARTIAL`).
- Полосы выносливости и каста: позиции известны, модели выносливости/каста в порте нет — не показываются.
- Цвет фона слотов способностей 1…4 — `UNKNOWN` (известны M1 оранжевый, M2 фиолетовый, пустой серый по снимку).
- Перезарядка на слотах (узел `cooldown`) не рисуется.
- Полоса цели: Alpha рисует `enemylifebar` над существом в мире; в порте — вверху по центру (`CUBEGAME_CONTRACT`).
- Подсказка `[E] TALK`: Alpha — над NPC; в порте — под прицелом (`CUBEGAME_CONTRACT`).
- Сообщения: область ChatWidget слева внизу, формулировки CubeGame.
- Окна инвентаря/крафта/навыков/карты/справки: раскладка Alpha (`InventoryWidget`, `SkillWidget`, `help.plx`)
  не перенесена (`PARTIAL`). F1 показывает список клавиш вместо страниц `help.plx`.
- Esc сначала закрывает открытое окно, и только без окон переключает ряд меню (`CUBEGAME_CONTRACT`).
- Квадратичные контуры логотипа растеризуются собственным кодом; размытие/свечение α Plasma не повторено.
- Живая игра (`D:\Games\CubeGame\game`) **не обновлялась** — нужно согласие продюсера.
