# D3 — регистрация провайдеров контента одним вызовом (стык с интегратором)

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content` @ `b8b9c5a`.
**STATUS: `READY_FOR_REVIEW`.**

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Подключение моих провайдеров | интегратор должен был вручную собрать список, порядок и предикаты | `AlphaContentProviders`: `CreateDefault(...)` → порядок «растительность/существа/постройки → город (тип зоны 1) → данж (предикат вызывающего)»; `RegisterDefaults(...)` регистрирует в `ZoneContentRegistry` и возвращает число добавленных |
| Правило данжа | не задано | **не придумывается**: без предиката данж-провайдер не создаётся; `Notes` прямо пишет, что тип зоны данжа в Alpha `UNKNOWN` |
| Проверка предикатов | — | `IsCityZone` (null-safe), `ByRecordType(type)` |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Features/Creatures/AlphaContentProviders.cs` | фабрика и регистрация провайдеров |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaContentProvidersTests.cs` | 5 тестов: состав/порядок, данж только с предикатом, null-safe предикаты, фактическая регистрация в `ZoneContentRegistry`, честные Notes |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `134/134 passed`, 0 failed (`AlphaOutput/d3g_editmode.xml`) |
| Полные EditMode/PlayMode | EditMode `552` (546 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

## 4. Что нужно от Codex (интегратор)

1. Вызвать `AlphaContentProviders.RegisterDefaults(isDungeonZone: <правило>)` **после** проходов рельефа,
   до генерации контента зоны; при смене набора провайдеров менять id генератора мира (требование контракта).
2. Если правило «какая зона — данж» появится (например, тип записи 4), передать
   `AlphaContentProviders.ByRecordType(4)`; сейчас тип не доказан, поэтому в коде его нет.
3. Для населения — `AlphaWorldPopulation(seed, zoneLevel, groundHeight, npcTable:false, zoneType: <тип зоны>)`,
   как описано в [DEEPSEEK_M0_STATUS](DEEPSEEK_M0_STATUS.md).

## 5. PARTIAL / UNKNOWN

- Тип зоны данжа — `UNKNOWN` (не выдумывался).
- Плотность/позиции контента остаются `CUBEGAME_CONTRACT` (D1 часть B).
