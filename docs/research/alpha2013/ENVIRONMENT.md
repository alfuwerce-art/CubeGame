# CubeGame — ENVIRONMENT (S00-G0)

**Дата проверки:** 2026-11-02, 21:17–21:35 MSK
**Исполнитель:** DeepSeek Harness, локальная сессия на ПК продюсера (модель DeepSeek-V4.1-Flash)
**Статус документа:** факты, снятые командами на этом ПК. Всё, что не подтверждено командой, помечено `UNKNOWN`.

## 1. Отступление от рекомендованной конфигурации сессии

| Параметр | Рекомендовано (`docs/agents/DEEPSEEK_SETUP_STATUS.md`) | Фактически в этой сессии |
|---|---|---|
| Файловая песочница | `workspace-write` | `danger-full-access` |
| Политика подтверждений | `ask` | `never` (подтверждения отключены продюсером) |

FACT: запуск игры, чтение `D:\Games\Cube world` и запись отчётов выполнены в этой сессии. Продюсер дал прямое разрешение на запуск Alpha в постановке задачи. Рекомендация зафиксировать безопасный профиль (`workspace-write / ask`) остаётся открытой — см. `S00_G0_REPORT.md`, п. 8.

## 2. Операционная система и железо

| Параметр | Значение | Проверка |
|---|---|---|
| ОС | Microsoft Windows 11 Pro, версия 10.0.26200, build 26200, x64 | `Get-CimInstance Win32_OperatingSystem` |
| Последняя загрузка ОС | 2026-10-15 23:51:19 | то же |
| CPU | AMD Ryzen 5 7535HS, 6 ядер / 12 потоков, 3.3 ГГц | `Win32_Processor` |
| RAM | 31.2 ГБ | `Win32_ComputerSystem` |
| GPU 0 | AMD Radeon(TM) Graphics (iGPU), драйвер 32.0.11038.3 | `Win32_VideoController` |
| GPU 1 | NVIDIA GeForce RTX 4060 Laptop GPU, драйвер 32.0.16.1047 | то же |
| Мониторы | 2 × 1920×1080; основной `\\.\DISPLAY2` (0,0), второй `\\.\DISPLAY1` (−1920,0) | `System.Windows.Forms.Screen` |
| Диски | C: 476 ГБ (свободно 66), D: 954 ГБ (свободно 685) | `Get-Volume` |
| Anti-Virus | Microsoft Defender: активен, RTP включён, сигнатуры 1.463.84.0 от 2026-11-02 01:25 | `Get-MpComputerStatus` |
| Prefetch | `EnablePrefetcher=3`, но каталог `C:\Windows\Prefetch` пуст → история запусков недоступна | реестр + `Get-ChildItem` |

## 3. Инструменты разработки (только чтение, ничего не устанавливалось)

| Инструмент | Факт |
|---|---|
| Git | 2.55.0.windows.1 |
| Git identity (repo-local, CubeGame) | `alfuwerce-art <317812753+alfuwerce-art@users.noreply.github.com>` |
| PowerShell | Windows PowerShell 5.1.26100.9711 |
| Python | 3.13.14 |
| .NET | рантаймы `Microsoft.NETCore.App`/`WindowsDesktop.App` 7.0.14 и 8.0.33; **SDK отсутствует** (`dotnet --list-sdks` пуст) |
| Unity Hub | **не установлен** (`C:\Program Files\Unity Hub` отсутствует, записи Uninstall нет) |
| Unity Editor | **не установлен** (`C:\Program Files\Unity`, `D:\Unity` отсутствуют) |
| Blender | **не установлен** |

Вывод: для S01 потребуется отдельная установка Unity Hub + Unity LTS (по решению продюсера). В S00-G0 установка не выполнялась.

## 4. Клиент Alpha 2013

| Параметр | Значение |
|---|---|
| Путь | `D:\Games\Cube world` (существует, читается) |
| Заявленная версия | `Cube world Alpha 0.1.1` (запись Uninstall в HKLM, `Publisher = Zombie`) |
| Установка | 2026-11-02 20:48:30–20:48:31, `InstallSource = C:\Users\bow\Downloads\` |
| Установщик | `C:\Users\bow\Downloads\Cube world.exe`, 36 753 785 Б, получен 20:47:28 с торрента `[winstation.ru] cube-world.torrent` (см. отчёт, п. 4) |
| Исполняемый файл игры | `Cube.exe` (32-bit, unsigned, компоновка 2013-07-20), запускается напрямую из папки |
| Launcher | `CubeLauncher.exe` (32-bit, unsigned, компоновка 2013-07-05) — оригинальные строки Picroma на месте, вход не использовался |
| Ресурсы | `data1–4.db`, `start/gui/help/quest-tag.plx`, `cursor.plx`, `resource1–2.dat`, `logo.bmp` — все с датами 2013 года, не изменялись |
| Сохранения | `Save\characters.db`, `Save\worlds.db`, `Save\world_G0WORLD.db`, `Save\map_G0WORLD.db` (SQLite) |
| Локализация UI | частично русская (`ЗЕМЛИ ARULON`, `РАЗРАБОТЧИК`), английские подписи меню сохранены; `options.cfg: language 9` |
| Окно | 800×600, режим windowed (при `options.cfg: resolutionX/Y = 1920/1080`) |
| Полный перечень файлов и SHA-256 | `S00_G0_REPORT.md`, п. 3 |

## 5. Локальные артефакты проверки (вне репозитория)

Каталог: `C:\Users\bow\Documents\CubeGame_S00_G0\` — 24 скриншота шагов проверки (`shot_01_launch.png` … `shot_25_startmenu.png`) и резервные копии сохранений до теста (`characters.pre.db`, `worlds.pre.db`, `options.pre.cfg`).

Причина выноса из репозитория: на скриншотах — закрытые ассеты Alpha 2013, публикация в GitHub не требуется для G0 (см. `docs/stages/S00_ALPHA_RESEARCH.md`, п. 4).

## 6. Неизвестное

1. Кто и как запускал клиент в 20:49 (за 28 минут до начала сессии агента) — следы есть (`Save\*.db` 20:49:12, `options.cfg` 20:49:35), исполнитель не установлен.
2. Эталонные SHA-256 официальной сборки Alpha 0.1.1 отсутствуют в открытых источниках, поэтому bit-exact подлинность не доказывается (см. отчёт, п. 5).
3. Точный способ русской локализации (правка `data*.db`/`.plx` или встроенные в репак ресурсы) — не установлен, строки в файлах не найдены ни в одной из проверенных кодировок (вероятно, сжаты внутри архивов).
