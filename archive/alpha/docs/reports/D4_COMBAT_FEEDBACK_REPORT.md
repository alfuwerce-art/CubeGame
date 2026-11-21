# D4/D5 шаг — отклик боя в визуальном рантайме (волна 2, пакет DeepSeek)

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content` @ `26722fd`.
**STATUS: `READY_FOR_REVIEW`.**

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Попадание | `OnHitResolved` — пустой метод: попадание никак не видно | цель коротко «вспыхивает» (импульс масштаба 6 % за 0.12 с) — `CombatFeedback.FlashIntensity` |
| Смерть | тело просто ложилось на бок и оставалось навсегда | тело ложится (наклон 90°), погружается на 0.5 блока за 2 с и скрывается по завершении (`CorpseFinished`) |
| Честность | — | `CUBEGAME_CONTRACT: Alpha hit/death presentation not restored (R-3/R-8 blocked); flash and corpse are CubeGame's` — за паритет не выдаётся |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Presentation/Visual/CombatFeedback.cs` | чистые кривые отклика (вспышка, погружение, завершение), константы и `Validate()` |
| `Assets/CubeGame/Presentation/Gameplay/GameVisualSink.cs` | `OnHitResolved` → вспышка цели; `OnEntityDied` → наклон + погружение + скрытие; `Entry` хранит `DeathTimeMs`/`HitTimeMs` |
| `Assets/CubeGame/Tests/Creatures/EditMode/CombatFeedbackTests.cs` | 3 EditMode-теста (вспышка, монотонное погружение и завершение, константы/провенанс) |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `108/108 passed`, 0 failed (`AlphaOutput/d4c_editmode.xml`) |
| Полные EditMode/PlayMode | EditMode `526` (520 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

## 4. PARTIAL / UNKNOWN

1. **Настоящие эффекты попаданий/смерти Alpha** (R-3, R-8: `spawnHitParticles`, `Creature_spawnDeathEffects`,
   динамические части) — `BLOCKED` на research рендереров: в поставке это отдельные меши, не поза `drawModel`.
2. **Вспышка цветом** — сейчас импульс масштаба (не требует инстансов материалов); цветовой вариант —
   после выбора шейдера/материала в сцене (этап визуального осмотра).
3. **Труп как объект мира** (лут, обыск) — за игрой (C5), визуал только скрывает тело.

## 5. Что нужно от Codex (интегратор, D89)

1. Ничего обязательного: `OnHitResolved`/`OnEntityDied` уже в контракте sink; шина публикует события.
2. Для проверки: ударить волка/героя — вспышка; убить — тело ложится и уходит в землю за 2 с.
