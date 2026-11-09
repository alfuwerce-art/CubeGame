# D86 — handoff DeepSeek → Claude: runtime-витрина контента (M0-09C)

**Дата:** 2026-11-09. **Ветка:** `deepseek/d85-visual-unity`. **Статус:** `READY_FOR_REVIEW`.

## 1. Что это

`AlphaContentShowcase` (`Assets/CubeGame/Presentation/Showcase/`) — компонент для **пустой сцены**, чтобы
продюсер/Claude увидели перенесённый контент в Play mode без модуля мира:

- визуал игрока (человек 0x02) из `AlphaPlayerVisualBuilder`;
- N существ из подготовленных видов с действиями (`CreatureSpawner` + `CreatureAi`), расставленных по кругу;
- несколько домов из `AlphaStructurePlacementPlanner` (детерминированный контракт размещения).

Правил игры компонент не содержит: игрок — только визуал, существа живут на существующем AI, дома — кубы
планировщика (не заменяют дома Claude).

## 2. Использование

```csharp
var showcase = new GameObject("AlphaShowcase").AddComponent<AlphaContentShowcase>();
showcase.Seed = 2024;            // детерминизм
showcase.CreatureCount = 6;
showcase.StructureCount = 2;
showcase.Radius = 8f;
showcase.SpawnOnStart = true;    // Build() в Start; в тестах можно вызвать Build/Tick вручную
// Проблемы: showcase.Problems; готовность: showcase.Player != null, SpawnedCreatures, StructureObjects
```

`Tick(dt)` двигает существ и визуал; в сцене `Update` вызывает сам.

## 3. Ограничения

- Климат/растительность в витрину не входят (W-18 работает через `AlphaScatterField`, см. handoff M0-02) —
  витрина не выдумывает климат.
- Дома — кубы 3×3×4 по планировщику; интерьер/двери не рисуются.
- Существа выбираются из staged `ACTIONS`; при отсутствии кэша `Problems` содержит `SHOWCASE_CONTENT_NOT_READY`.
- Аудио/VFX отсутствуют (R-3/R-8).

## 4. Проверки

`AlphaOutput/playmode.xml` — 45/45 (витрина: игрок + 4 существа + 2 дома, 60 кадров без ошибок);
`AlphaOutput/editmode.xml` — 351/351.
