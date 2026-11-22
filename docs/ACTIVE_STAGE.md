# CubeGame — ACTIVE STAGE
**Обновлено:** 2026-11-22. **Активная задача:** `UNREAL_FIRST_PLAYABLE` (`AUTHORIZED_EXPERIMENT`).
**Контракт:** [U0–U4](experiments/UNREAL_FIRST_PLAYABLE.md).

## Сейчас
1. Подтвердить фактически установленную UE, C++ toolchain, поддержку Python Editor и Unreal MCP клиентом DeepSeek.
2. В `Unreal/CubeGameTrial/` создать маленькую арену из повторяемого Editor Python-скрипта.
3. **U2 — работа по прямому заданию владельца:** DeepSeek самостоятельно находит **Human (приоритет) или Elf** и его модель/части/процедурную сборку/анимации Unity по [U2-MANNEQUIN](experiments/U2_MANNEQUIN_DEEPSEEK_EXECUTION_TASK.md); проверяет `.scratch/content` и другие реальные источники, читает референсы из `D:\Projects\референсы для CudeGame` **строго на чтение**; без дополнительного M0-отчёта создаёт **собственный модульный 3D-манекен** в Unreal, rig, новые анимации через встроенные средства UE, Third Person управление и проверяет PIE/Standalone. Старые Unity анимации — только пример поведения. Боевой Enemy/HP/предмет/SaveGame — следующий **U3**, а не условие U2.
4. Проверить PIE/Standalone и Windows build, измерить FPS/CPU/RAM/VRAM и число исправлений агентом.

**Unity G1/G2:** исторический текущий кандидат и код сохраняются без изменений; приёмка не подтверждена. Изменение движка релизного проекта не утверждено. После пилота владелец выбирает дальнейший путь.

**Производственный Roadmap:** [G0–G16](ROADMAP.md) — `PROPOSED_FOR_OWNER_REVIEW`, переработан под First Playable → Vertical Slice → Production → Alpha → Beta → RC → Release; пока **не принят**, GitHub `main` не изменён. [ADR-кандидат](architecture/ADR-0002-unreal-cpp-first-proposal.md). Первые реальные задачи по порядку: **U1 через Python Editor**, затем **U2 с фактическим применением Unreal MCP**; для сравнения учитывать разную сложность. U0 засчитывается только после проверки настоящего подключения. Никаких выводов о принятом движке или готовности G0–G16 до U4 и решения владельца.

**Исполнительский контракт U2:** [DEEPSEEK U2 MANNEQUIN](experiments/U2_MANNEQUIN_DEEPSEEK_EXECUTION_TASK.md). **M0/M1/M2 как отдельные этапы и их приёмка отменены**; это только технические внутренние операции поиска/моделирования/анимации. Локальный аудит коммита `1ae387c` использовать как входную информацию. Агент **не останавливается** на его повторной сдаче, не спрашивает где Human/Elf и способен ли продолжить. Итогом U2 — только реальный манекен и демонстрация в Unreal с `READY_FOR_OWNER_VISUAL_REVIEW`. U1 не объявлять выполненным без проверки; существующая/минимальная тестовая сцена U2 может использоваться для демонстрации манекена.

**DSH U0 факт по пользовательскому отчёту (22.11.2026):** локальный Unreal MCP read-only вызовы прошли `PASS` (DSH Desktop 0.2.2-rc.1; UE 5.9.0, `D:\\CubeGame\\CubeGame.uproject`, уровень `/Game/ThirdPerson/Lvl_ThirdPerson`, 106 toolsets). Отчёт `docs/agents/DSH_UNREAL_MCP_05_REPORT.md` локальный/untracked, **не опубликован**. Это не означает проверки реальной C++/Win64 сборки или принятия U0 полностью. Не путать `D:\\CubeGame` с заявленным `D:\\Projects\\CubeGame\\Unreal\\CubeGameTrial` — без аудита не копировать и не перемещать.
