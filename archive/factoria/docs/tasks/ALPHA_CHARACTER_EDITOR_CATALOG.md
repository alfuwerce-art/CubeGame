# Cube World Alpha — каталог редактора персонажа

**14 ноября 2026 · READY_FOR_OWNER_REVIEW**

[JSON master](../../data/alpha_character_editor/alpha_character_editor_catalog.json) · [Human Male/Female](HUMAN_EDITOR_ALPHA_REFERENCE.md)

Полный ростер в проверенной сборке содержит **8** позиций. Детально каталогизированы **7 рас × 2 пола, 71 Face и 100 Haircut**. Frogman (race_id=7) остаётся в ростере как OUT_OF_SCOPE по решению владельца.

| Race | Gender | Face count | Haircut count | Color system | Other confirmed modules | Missing assets (UI) |
|---|---|---:|---:|---|---|---|
| Human | [Male](../../data/alpha_character_editor/HUMAN/MALE.json) | 6 | 15 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Human | [Female](../../data/alpha_character_editor/HUMAN/FEMALE.json) | 6 | 7 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Elf | [Male](../../data/alpha_character_editor/ELF/MALE.json) | 4 | 10 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Elf | [Female](../../data/alpha_character_editor/ELF/FEMALE.json) | 6 | 10 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Dwarf | [Male](../../data/alpha_character_editor/DWARF/MALE.json) | 5 | 3 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Dwarf | [Female](../../data/alpha_character_editor/DWARF/FEMALE.json) | 5 | 5 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Orc | [Male](../../data/alpha_character_editor/ORC/MALE.json) | 5 | 10 | 112 RGB, Haircut | Body, Head, Hand, Foot | 8, 9, 10 |
| Orc | [Female](../../data/alpha_character_editor/ORC/FEMALE.json) | 5 | 4 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Goblin | [Male](../../data/alpha_character_editor/GOBLIN/MALE.json) | 5 | 6 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Goblin | [Female](../../data/alpha_character_editor/GOBLIN/FEMALE.json) | 5 | 6 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Lizard | [Male](../../data/alpha_character_editor/LIZARD/MALE.json) | 2 | 6 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Lizard | [Female](../../data/alpha_character_editor/LIZARD/FEMALE.json) | 5 | 6 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Undead | [Male](../../data/alpha_character_editor/UNDEAD/MALE.json) | 6 | 6 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |
| Undead | [Female](../../data/alpha_character_editor/UNDEAD/FEMALE.json) | 6 | 6 | 112 RGB, Haircut | Body, Head, Hand, Foot | 0 |

Числа включают позиции карусели. Доступны все 71 Face и 97 из 100 Haircut. Body, Hand и Foot приходят из kind. В Creator Face выбирает Head, а Haircut выбирает overlay. Карточки содержат registry ID и ключи моделей, включая базовые части и значения дополнительных слотов.

## Код редактора

| Поле | Доказательство |
|---|---|
| Race | 0x428E40 отображает Human, Elf, Dwarf, Orc, Goblin, Lizard, Undead, Frogman. 0x42BA60/0x42BAB0 циклически обходят 0…7. |
| Gender | 0x42B990 переключает 0/1. 0x42C080 использует jump table по Race 0x42C3FC и kind = base + gender. |
| Class | 0x42B810/0x42B860 меняют widget+0x190 в диапазоне 0…3. Class записывается как index+1. 0x4772B0, ветвление 0x47742E, устанавливает экипировку. У ветки есть вызовы инициализации класса и пересчёта HP. |
| Face / Haircut | Виджеты меняют +0x198/+0x19C. Таблица 0x42C41C в 0x42C080 задаёт два независимых предела по kind. При выходе ниже 0 выбирается последняя позиция, при ≥ count — 0. При смене Race/Gender значение сохраняется, пока оно входит в диапазон нового kind. |
| Hair color | Сетка 16×7, 112 различных RGB8. Рисование/выбор 0x428E40, применение 0x42B8B0, передача в appearance 0x42C2D0…0x42C350. |
| Name | Экран имеет поле Name / charactername и обработчик ввода 0x43A0D0. 0x44D790 записывает имя перед appearance. |

Проверены ветки 0x42C080 по семи расам, обоим полам и четырём классам. Класс передаётся в Creature+0x140, а 0x43F7C0 получает отдельный kind и блок данных внешности без class. Проверены оба граничных перехода и все допустимые индексы. В полном контуре создания Class меняет стартовую экипировку, отчего могут измениться видимые предметы. Класс участвует в игровых способностях и прогрессии. Base body и диапазоны Face/Haircut от класса не зависят.

Skin Color, Eye Color, Beard, Mustache, Headgear, Ears, Horns, Crest, Tail, Scale Color и Effect не имеют самостоятельных контролов в проверенных связках панели и сериализации. В части Haircut видны борода, усы или гребневидная геометрия. Игровые эффекты за пределами редактора в этом аудите не исследовались.

## Как читалась геометрия

- loader_id — VA вызова VoxelModel_load_from_file_or_buffer, asset_id — hex-индекс реестра, asset_path — ключ таблицы blobs в data1.db, index — позиция карусели. Номер в UI = index + 1.
- Размер сетки и габариты занятых клеток разделены. Порядок осей X, глубина, высота. SHA-256 вычислен по хранимому SQL-блобу. Число непрозрачных вокселей исключает чёрный и при loader flag 0 три первичных RGB-маркера. В оригинальном меше грань рядом с маркером может отсутствовать даже при прозрачном маркере. Адреса 0x4E7650, 0x4E7870, 0x4E71D0.
- Для каждого Face просмотрены фронт и профиль. Для каждого доступного Haircut проверены фронт, профиль и затылок с Face 1. Симметрия измерена отражением занятых координат по X. covers содержит зоны геометрического пересечения в проекции. Семантика полного покрытия ушей остаётся UNKNOWN. Визуальные классы HAIR, BEARD, CREST, OTHER отмечены INFERRED. У Goblin Male Haircut 1 и Undead Male Haircut 1 ресурс есть, отдельная верхняя форма в проекции почти не видна. HAT и HEADBAND уверенно не установлены.
- Превью — ортопроекции локальных данных Alpha. Источником для сопоставления служил Face 1. Clipping с остальными Face и итоговое освещение не проверялись.

## Hair color и глаза

В бинарнике цикл рисует 16 столбцов по 7 ячеек (0x42B421…0x42B6F7). Три верхних ряда затемняют базовый тон, четвёртый показывает его без изменения, три нижних добавляют белый. Константы 0.25, 0.2 и 255.0 прочитаны из EXE. В таблице сохранены все 112 RGB после float32 и усечения. FFC832 в reset и реальном сейве задаётся отдельно. При клике preview RGB копируется в committed RGB. В refresh проверяется адрес widget+0x1A3. Код оригинала при валидном widget использует preview bytes и при вызове обработчиком с аргументом 0.

Для обоих полов семи включённых рас 0x43F7C0 скопировал RGB 07 81 D3 в runtime appearance. Head (0x4206C2) и Haircut (0x420A38) используют разные вызовы отрисовки. 0x448280 перед Haircut устанавливает materialColor (VS 0x6FD928, c32) из байтов Creature+0x7A…0x7C, делённых на 255. Материал умножает RGB вершин всего overlay, включая видимые части бороды, усов или гребня. На итоговый кадр влияют цвет существа и освещение. Независимый материал части overlay не обнаружен.

Глаза читаются в Face-only проекциях всех 71 ресурса. В .cub RGB хранятся по ячейкам. Для семи рас в проверенном пути игрока отдельная модель глаз, UV-текстура и поле цвета глаз отсутствуют.

## Отсутствующие модели

| Orc Male UI | Индекс | Registry | Loader VA | Ключ data1.db/blobs |
|---:|---:|---|---|---|
| 8 | 7 | 0x525 | 0x4F1EC5 | orc-hair-m08.cub |
| 9 | 8 | 0x526 | 0x4F1F09 | orc-hair-m09.cub |
| 10 | 9 | 0x527 | 0x4F1F4D | orc-hair-m10.cub |

Исполненный выбор выдаёт эти registry IDs. SQL-поиск с COLLATE NOCASE и поиск loose .cub в установке не нашли модели. Loader 0x4E7290 при ненайденном DB key пропускает VoxelModel_set_data. Фактический кадр и реакция игры при переключении трёх позиций остаются UNKNOWN.

## Save

SQLite Save/characters.db → blobs[key] → zlib. Сериализатор 0x44D790 и десериализатор 0x44BE40. Префикс содержит класс u8 @ 0x34, специализацию u8 @ 0x35, затем 13 элементов экипировки и длину имени с NUL-терминатором. Длина имени в байтах находится по 0xE76. Начало 20-байтного блока appearance вычисляется как 0xE76 + 4 + name_length.

| Offset в блоке | Тип | Поле |
|---|---|---|
| 0x00 | u32 LE | race |
| 0x04 | u8 | gender |
| 0x05–0x07 | 3 байта | семантика UNKNOWN |
| 0x08 | i32 LE | face index |
| 0x0C | i32 LE | haircut index |
| 0x10–0x12 | u8 × 3 | R, G, B |
| 0x13 | 1 байт | семантика UNKNOWN |

Blob 0 прочитан в режиме read-only. После zlib — 63 753 байта, appearance на 0xE7F с Human/Male, Face 0, Haircut 0, FFC832, классом 1 и специализацией 0. Пройдены массивы до тега версии 11 и 11 рангов, до конца записи. Дополнительный appearance list (11 записей по 0x118 в образце) и map хранятся после инвентаря и счётчиков. Точная семантика вложенных данных остаётся частично неизвестной, полей редактора из них не выводили.

## Воспроизводимость и достоверность

Версию идентифицируют SHA-256 Cube.exe и data1.db в каждом JSON. Подписи и привязки UI сверены с декомпиляцией Reversal, оригинальные байты выбора 0x42C0A9…0x42C3C2 исполнены в Unicorn в отдельной памяти. Вызовы подготовки буфера подменены, генератор выполнен отдельно с реальным race/gender seed. Для registry применён tools.alpha_models.universal.alpha_static.model_registry к оригинальному EXE. Для частей тела исполнены Spawn_initDefaults (0x428750) и creature_generateAppearance (0x43F7C0). Блоб каждой модели прочитан через SQLite read-only и декодирован tools.alpha_models.cub_format.parse_stored. Проверка подтверждает факты по коду, ассетам и сейву. Интерактивного прогона редактора и создания новых сейвов нет. Метки по отдельным утверждениям — CODE_CONFIRMED, ASSET_CONFIRMED, SAVE_CONFIRMED, INFERRED и UNKNOWN.

Контроль результата: 14 самостоятельных JSON совпали с master; 71 Face и 100 Haircut сверены с результатами исполнения оригинальных инструкций; сверено 168 SHA-256 существующих позиций. Перепроверены номера UI, 112 RGB, три отсутствующих позиции и ссылки всех карточек. 28 диагностических листов лежат локально по папкам Face и Haircut.

## FOLLOW_UPS

- На проверочной копии Alpha наблюдать Orc Male Haircut 8–10 и уточнить покрытие ушей и смысл слабо выраженных overlay при сопоставлении со всеми Face.
