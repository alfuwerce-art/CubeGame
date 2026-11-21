# CubeGame — ACTIVE STAGE
**Обновлено:** 2026-11-22. **Активная задача:** `UNREAL_FIRST_PLAYABLE` (`AUTHORIZED_EXPERIMENT`).
**Контракт:** [U0–U4](experiments/UNREAL_FIRST_PLAYABLE.md).

## Сейчас
1. Подтвердить фактически установленную UE, C++ toolchain, поддержку Python Editor и Unreal MCP клиентом DeepSeek.
2. В `Unreal/CubeGameTrial/` создать маленькую арену из повторяемого Editor Python-скрипта.
3. На C++ реализовать управление, контактную атаку, одного врага, один предмет, HP и сохранение. Blueprint только если он действительно упрощает редакторскую связку.
4. Проверить PIE/Standalone и Windows build, измерить FPS/CPU/RAM/VRAM и число исправлений агентом.

**Unity G1/G2:** исторический текущий кандидат и код сохраняются без изменений; приёмка не подтверждена. Изменение движка релизного проекта не утверждено. После пилота владелец выбирает дальнейший путь.

**Производственный Roadmap:** [G0–G16](ROADMAP.md) — `PROPOSED_FOR_OWNER_REVIEW`, переработан под First Playable → Vertical Slice → Production → Alpha → Beta → RC → Release; пока **не принят**, GitHub `main` не изменён. [ADR-кандидат](architecture/ADR-0002-unreal-cpp-first-proposal.md). Первые реальные задачи по порядку: **U1 через Python Editor**, затем **U2 с фактическим применением Unreal MCP**; для сравнения учитывать разную сложность. U0 засчитывается только после проверки настоящего подключения. Никаких выводов о принятом движке или готовности G0–G16 до U4 и решения владельца.
