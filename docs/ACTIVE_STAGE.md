# CubeGame — ACTIVE STAGE
**Обновлено:** 2026-11-22. **Активная задача:** `UNREAL_FIRST_PLAYABLE` (`AUTHORIZED_EXPERIMENT`).
**Контракт:** [U0–U4](experiments/UNREAL_FIRST_PLAYABLE.md).

## Сейчас
1. Подтвердить фактически установленную UE, C++ toolchain, поддержку Python Editor и Unreal MCP клиентом DeepSeek.
2. В `Unreal/CubeGameTrial/` создать маленькую арену из повторяемого Editor Python-скрипта.
3. **До U2:** аудит местоположения Unity-модели, формата/происхождения/прав, актуальных авторских референсов и фактически открытого Unreal-проекта по [U2-MANNEQUIN](experiments/U2_MANNEQUIN_UNITY_TO_UNREAL_SPEC.md); после визуального ревью — модульный риггованный технический гуманоид с **оригинальными анимациями на основе наблюдений за Unity, без переноса клипов**; максимально использовать штатные Animation Blueprint/Control Rig/Montage/IK в Unreal 5.9 по фактической необходимости; C++ управление и затем противник/атака/предмет/HP/сохранение (U3). Не импортировать чужой меш в релиз и не публиковать его в Git. Blueprint только если он действительно упрощает редакторскую связку.
4. Проверить PIE/Standalone и Windows build, измерить FPS/CPU/RAM/VRAM и число исправлений агентом.

**Unity G1/G2:** исторический текущий кандидат и код сохраняются без изменений; приёмка не подтверждена. Изменение движка релизного проекта не утверждено. После пилота владелец выбирает дальнейший путь.

**Производственный Roadmap:** [G0–G16](ROADMAP.md) — `PROPOSED_FOR_OWNER_REVIEW`, переработан под First Playable → Vertical Slice → Production → Alpha → Beta → RC → Release; пока **не принят**, GitHub `main` не изменён. [ADR-кандидат](architecture/ADR-0002-unreal-cpp-first-proposal.md). Первые реальные задачи по порядку: **U1 через Python Editor**, затем **U2 с фактическим применением Unreal MCP**; для сравнения учитывать разную сложность. U0 засчитывается только после проверки настоящего подключения. Никаких выводов о принятом движке или готовности G0–G16 до U4 и решения владельца.

**Подробная исполняемая задача DeepSeek для манекена:** [U2-MANNEQUIN EXECUTION](experiments/U2_MANNEQUIN_DEEPSEEK_EXECUTION_TASK.md). Первый запуск = **M0 read-only аудит** Unity, анимаций и папки `D:\Projects\референсы для CudeGame` (строго не менять исходные файлы). M1/M2 после U1 и ревью владельца.

**DSH U0 факт по пользовательскому отчёту (22.11.2026):** локальный Unreal MCP read-only вызовы прошли `PASS` (DSH Desktop 0.2.2-rc.1; UE 5.9.0, `D:\\CubeGame\\CubeGame.uproject`, уровень `/Game/ThirdPerson/Lvl_ThirdPerson`, 106 toolsets). Отчёт `docs/agents/DSH_UNREAL_MCP_05_REPORT.md` локальный/untracked, **не опубликован**. Это не означает проверки реальной C++/Win64 сборки или принятия U0 полностью. Не путать `D:\\CubeGame` с заявленным `D:\\Projects\\CubeGame\\Unreal\\CubeGameTrial` — без аудита не копировать и не перемещать.
