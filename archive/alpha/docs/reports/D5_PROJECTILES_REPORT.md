# D5 шаг — снаряды: модели Alpha и полёт (волна 3, пакет DeepSeek)

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content` @ `12dea33`.
**STATUS: `READY_FOR_REVIEW`** (данные и движение); сами эффекты/снаряды Alpha остаются `BLOCKED`.

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Снаряды | в игре нет ни данных, ни движения; R-3 считался `BLOCKED` целиком | `Projectiles`: модель снаряда — **реальные данные Alpha** (стрела `arrow.cub`, бумеранг `wood-boomerang1.cub` из таблицы оружия предметного слоя), полёт — `CubeGame`-контракт (полунеявный Эйлер, гравитация по желанию, время жизни, без коллизий) |
| Честность | — | `MODEL=ALPHA_DATA (item layer); flight/lifetime/collision=CUBEGAME_CONTRACT (Alpha projectile renderers R-3 not ported)` |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Presentation/Visual/Projectiles.cs` | `ModelOf(subtype)`, `Launch(...)`, `Step(...)`, `ProjectileState`, константы |
| `Assets/CubeGame/Tests/Creatures/EditMode/ProjectilesTests.cs` | 4 теста: модели из таблицы Alpha, полёт к цели со скоростью, гравитация, отказы при неверных параметрах |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `115/115 passed`, 0 failed (`AlphaOutput/d5_editmode.xml`) |
| Полные EditMode/PlayMode | EditMode `533` (527 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

## 4. PARTIAL / BLOCKED (не выдаётся за Alpha)

1. **Настоящие эффекты и снаряды Alpha** — `BLOCKED`: `CombatBehavior_spawnProjectileHit` (`0x596D30`) и
   `Creature_spawnDeathEffects` (`0x59B180`) строят собственные меши/частицы, а не позу `drawModel`
   (R-3/R-8). Перенос требует отдельного research рендереров — не маскируем.
2. **Баллистика, урон, попадание** — за игрой (Codex, C4: `CombatSystem.ResolveHit`); здесь только полёт.
3. **Привязка снаряда к действию/способности** (какая атака стреляет, задержка, разброс) — `UNKNOWN`;
   при появлении событий `ProjectileSpawned/Hit` в контракте (API) подключается без изменения этого файла.

## 5. Что нужно от Codex (интегратор)

1. Чтобы снаряд появился в сцене: вызвать `Projectiles.Launch(...)` и вести `Projectiles.Step(...)` в кадре
   (или опубликовать событие снаряда — тогда визуал возьмёт модель по подтипу).
2. Урон/попадание — на твоей стороне; я не создаю параллельную боевую систему.
