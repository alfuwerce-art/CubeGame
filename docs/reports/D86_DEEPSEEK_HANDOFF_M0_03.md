# D86 — handoff DeepSeek → Claude: визуал игрока из дескриптора персонажа (M0-03C/M0-04B)

**Дата:** 2026-11-09. **Ветка:** `deepseek/d85-visual-unity`. **Статус:** `READY_FOR_REVIEW`.

## 1. API

```csharp
var request = CharacterVisualRequest.Default;   // все опциональные слоты выключены (-1)
request.Kind = 0x02;                            // Alpha kind расы (0x02 HumanMale — подготовлен)
request.HasHairColour = true; request.HairColour = new Color(0.2f, 0.4f, 0.6f, 1f);
request.WeaponSubtype = 0; request.WeaponModelPath = ".../items/iron-sword1.cubdec";
request.OffhandSubtype = 13; request.OffhandModelPath = ".../items/iron-shield01.cubdec";
request.ChestSubtype = 0;                       // броня: грудь/перчатки/сапоги, -1 = нет
request.HandsSubtype = 0; request.FeetSubtype = 0;
request.HeldType = 1; request.HeldSubtype = 1;  // зелье жизни в руке, -1 = нет
var items = ".../.scratch/content/items";      // локальный кэш предметных .cubdec
var result = AlphaPlayerVisualBuilder.Build(request, playerTransform, AlphaActorLibrary.Shared, items);
if (!result.Ok) Debug.Log(result.Status);    // причины: MISSING_VISUAL_CONTENT, WEAPON_SUBTYPE_NOT_EQUIPPABLE, ...
result.View.ApplyLocomotion(state); result.View.Tick(dt);
```

## 2. Что делает builder

1. Вид расы → подготовленный Alpha-актор (`CreatureVisualCatalog`, без подмены модели).
2. Цвет волос → `AlphaAppearance.TrySetHairColour` (тинт `hair_rgb/255`, диапазон 0…1).
3. Оружие → `AlphaWeaponLayer.TryEquipFromFile` (26 измеренных якорей, рука/спина по клипу).
4. Левая рука → `AlphaOffhandLayer.TryEquipFromFile` (10 измеренных классов).
5. Любой отказ попадает в `Problems` и `Ok=false`; частичный визуал не выдаётся за готовый.

## 3. Ограничения

- Цвет волос — только тинт, без выбора причёски (варианты моделей волос не подготовлены под UI).
- Расы: подготовлены все 156 видов, но у человекоидов действие-библиотека есть только у 0x02; для остальных — формульная ходьба.
- Оружие/офф-хенд требуют локального `.cubdec` (кэш, не Git); пути передаёт игра.
- VFX/аудио/UI — вне этой части (M0-08, R-3).

## 4. Проверки

`AlphaOutput/playmode.xml` — 42/42 (+3: сборка игрока с волосами/мечом/щитом; броня+зелье в дескрипторе; отказы без подмены), `AlphaOutput/editmode.xml` — 350/350.
