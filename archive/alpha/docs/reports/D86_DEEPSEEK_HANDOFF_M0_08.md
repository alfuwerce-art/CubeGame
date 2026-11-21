# D86 — handoff DeepSeek → Claude: внешность и иконки (M0-08A)

**Дата:** 2026-11-09. **Ветка:** `deepseek/d85-visual-unity`. **Отчёт:** [M0_08_DEEPSEEK_VISUAL_REPORT](M0_08_DEEPSEEK_VISUAL_REPORT.md). **Статус:** `READY_FOR_REVIEW`.

## 1. API

```csharp
// цвет волос — Alpha-тинт (D61), вызывать после создания визуала персонажа:
AlphaAppearance.TrySetHairColour(view.Rig, new Color(0.4f, 0.25f, 0.1f), out var reason);

// иконка предмета для UI (без перерисовки, из Alpha-геометрии):
var sprite = AlphaItemIconRenderer.RenderFromFile(cubdecPath, 64, problems);
image.sprite = sprite;   // фон прозрачный

// раса/вид → подготовленный Alpha-актор (без подмены модели):
AlphaAppearance.TryResolveRaceActor(0x02, library, out var actorId, out var why);
```

## 2. Правила

- Иконка = реальная модель (тот же мешер `VoxelMesh` и материал мира); если файла нет — `null` + `ICON_FILE_MISSING`, а не пустая картинка.
- Цвет волос — только `_Tint` части `hair` (`hair_rgb/255`); остальные части белые, как в Alpha. Диапазон 0…1, иначе отказ.
- `TryResolveRaceActor` не подменяет вид: если вариант не подготовлен, вернёт `MISSING_VISUAL_CONTENT`.
- VFX/аудио в этой партии отсутствуют: R-3/R-8 не доказаны, звуковых данных нет.

## 3. Проверки

`AlphaOutput/editmode.xml` — 356/356; `AlphaOutput/playmode.xml` — 46/46 (tint волос, иконка iron-sword, передача действия AI→игровой хук, витрина). Пакетный рендер иконок: `RunRenderItemIcons` → `ICONS_RESULT=PASS icons=28`; атлас: `RunBuildIconAtlas` → `ICON_ATLAS_RESULT=PASS icons=42 columns=8 size=512x384` (`AlphaOutput/icons/atlas.png` + `atlas.json`, origin bottom-left).

## 4. Что дальше у DeepSeek

VFX/снаряды после доказательств R-3; аудио после данных; левая рука/расходники в предметном слое; расширение подготовленных визуалов видов; финальное покрытие M0-09 и производительность. Общая сцена и GameSession — за Claude.
