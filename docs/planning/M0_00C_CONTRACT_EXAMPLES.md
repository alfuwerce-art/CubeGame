# M0-00C — примеры контрактов (эскизы C#, DRAFT)

**Статус: `DRAFT`.** Иллюстрация к [M0_00C_INTEGRATION_ARCHITECTURE_DRAFT](M0_00C_INTEGRATION_ARCHITECTURE_DRAFT.md) §4.
Это **не код проекта** и не API: имена и поля — предложение для M0-00D. Не компилировались; реализация — только
в утверждённом M0-01+. Существующие типы `Core` (`CharacterSheet`, `EquipmentLoadout`, `CombatFormulas`,
`HitResolver`, `DamageMitigation`, `SaveGame`) используются как есть и здесь не повторяются.

## 1. Идентификаторы
```csharp
namespace CubeGame.Features.Ids
{
    /// Устойчивый ID сущности мира: детерминирован от seed региона и индекса спавна (нет дублей после выгрузки).
    public readonly record struct EntityId(long RegionId, int SpawnIndex, int Generation)
    {
        public static EntityId Hero(int saveSlot) => new(long.MinValue, saveSlot, 0);
    }
    public readonly record struct KindId(int Value);        // тип существа CubeGame (для видов Alpha = kind)
    public readonly record struct ItemDefId(int Value);
    public readonly record struct ActionId(int Value)       // 1:1 с ID действия Alpha там, где доказано
    {
        public const int FirstCubeGameOnly = 1000;          // новые действия CubeGame — не пересекаются с Alpha
    }
}
```

## 2. Часы и сессия
```csharp
namespace CubeGame.Features
{
    public sealed class GameClock
    {
        public const int TickMs = 33;                       // предложение: ~30 Гц логики (решение M0-00D)
        public long NowMs { get; private set; }
        public bool Paused { get; set; }
        long _carryMs;
        /// Единственная точка, получающая реальное время кадра (Presentation вызывает 1 раз за кадр).
        public int Advance(long frameMs, Action<long> tick)
        {
            if (Paused) return 0;
            _carryMs += frameMs; int n = 0;
            while (_carryMs >= TickMs) { _carryMs -= TickMs; NowMs += TickMs; tick(NowMs); n++; }
            return n;
        }
    }

    public enum SessionState { Boot, MainMenu, LoadingWorld, InGame, Paused, SavingCheckpoint, Unloading, Quit }

    public interface IGameSession
    {
        SessionState State { get; }
        GameClock Clock { get; }
        IEventBus Events { get; }
        bool Submit(ICommand command, out string rejectReason);   // команда либо принята, либо отклонена с причиной
    }
}
```

## 3. Команды и события
```csharp
namespace CubeGame.Features.Messaging
{
    public interface ICommand { EntityId Actor { get; } }
    public sealed record StartActionCommand(EntityId Actor, ActionId Action, EntityId? Target) : ICommand;
    public sealed record UseItemCommand(EntityId Actor, int InventorySlot) : ICommand;
    public sealed record EquipCommand(EntityId Actor, int InventorySlot, EquipmentSlot Slot) : ICommand;

    public interface IGameEvent { long AtMs { get; } }
    public sealed record ActionStarted(long AtMs, EntityId Actor, ActionId Action, ActionPhases Phases) : IGameEvent;
    public sealed record ActionPhaseChanged(long AtMs, EntityId Actor, ActionId Action, ActionPhase Phase) : IGameEvent;
    public sealed record ActionResolved(long AtMs, EntityId Actor, ActionId Action, ActionOutcome Outcome) : IGameEvent;
    public sealed record ActionRejected(long AtMs, EntityId Actor, ActionId Action, string Reason) : IGameEvent;
    public sealed record HitResolved(long AtMs, EntityId Attacker, EntityId Target, float Damage, bool Blocked, bool Killed) : IGameEvent;
    public sealed record ItemEquipped(long AtMs, EntityId Actor, ItemDefId Item, EquipmentSlot Slot) : IGameEvent;
    public sealed record ItemConsumed(long AtMs, EntityId Actor, ItemDefId Item) : IGameEvent;
    public sealed record ProjectileSpawned(long AtMs, EntityId Projectile, EntityId Owner, ActionId Action) : IGameEvent;
    public sealed record EffectRequested(long AtMs, string EffectId, EntityId Anchor, string Status) : IGameEvent; // Status: PLACEHOLDER_NOT_ALPHA …

    public interface IEventBus
    {
        void Publish(IGameEvent e);                     // только владельцы состояния (§3 архитектуры)
        IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent;
    }
}
```

## 4. Действие как данные
```csharp
namespace CubeGame.Features.Actions
{
    public enum ActionCategory { Melee, Ranged, Spell, Consumable, Tool, Movement, HeldState }
    public enum ActionPhase { Windup, Strike, Recover, Done }
    public readonly record struct ActionPhases(int WindupMs, int StrikeMs, int RecoverMs);

    public sealed record ActionDef(
        ActionId Id, ActionCategory Category,
        ItemRequirement RequiredItem,            // подтип оружия / расходник / инструмент / нет
        ActionPhases Phases,                     // из формул Alpha (base_ms/(haste·speed)) или DeviationRegistry
        int CooldownMs, float ResourceCost,
        string AnimKey,                          // ключ клипа S05; пусто → ANIM_MISSING, правило работает
        EquipmentSlot? HeldItemSlot,             // предмет в руке на время действия (зелье, инструмент)
        string? ProjectileDef, string? EffectId,
        string Provenance);                      // TRANSFERRED_FROM_ALPHA / EMULATED_ALPHA_CODE / ORIGINAL_NOT_ALPHA …

    public interface IActionRegistry
    {
        /// Только действия, разрешённые этому виду/классу/оружию. У STATIC_OBJECT пусто (D69).
        IReadOnlyList<ActionDef> Allowed(KindId kind, CharacterClass? cls, ItemDefId? mainHand);
    }
}
```

## 5. Анимация — подписчик (Presentation)
```csharp
// Presentation: правила не меняются анимацией; контроллер S05 в режиме externalClock.
sealed class ActionAnimationBridge : IDisposable
{
    readonly IDisposable _sub;
    public ActionAnimationBridge(IEventBus bus, IEntityViews views)
        => _sub = bus.Subscribe<ActionStarted>(e =>
        {
            var view = views.Find(e.Actor);
            if (view == null) return;
            if (!view.Animation.Has(e.Action)) { Log.Warn($"ANIM_MISSING {e.Action}"); return; }
            view.Animation.Play(e.Action, startMs: e.AtMs);   // время берётся из GameClock, не из Time.deltaTime
        });
    public void Dispose() => _sub.Dispose();
}
```

## 6. Перевод ID Alpha (только `AlphaModels.Import`)
```csharp
namespace CubeGame.AlphaModels.Import
{
    public static class AlphaIds
    {
        /// Alpha +0x140 = classIdx + 1 (R-ID-1). 3 → Mage.
        public static CharacterClass ClassFromByte(byte raw) => raw switch
        {
            1 => CharacterClass.Warrior, 2 => CharacterClass.Ranger,
            3 => CharacterClass.Mage,    4 => CharacterClass.Rogue,
            _ => throw new ArgumentOutOfRangeException(nameof(raw), raw, "UNSUPPORTED Alpha class byte"),
        };
        public static byte ClassToByte(CharacterClass c) => (byte)((int)c + 1);
    }
}
// Тест M0-01A: ClassFromByte(3) == Mage; ClassToByte(Mage) == 3; 0 и 5 → исключение.
```

## 7. Сейв: секции и миграции
```csharp
namespace CubeGame.Persistence
{
    public interface ISaveMigration { int From { get; } int To { get; } SaveDocument Apply(SaveDocument doc); }

    public static class SaveMigrator
    {
        /// v1 (S04: персонаж + предметы) → v2 (секции session/hero/inventory+equipment/world.deltas/entities).
        public static SaveDocument Upgrade(SaveDocument doc, IReadOnlyList<ISaveMigration> chain, int target)
        {
            while (doc.Version < target)
            {
                var m = chain.SingleOrDefault(x => x.From == doc.Version)
                        ?? throw new SaveMigrationException($"MigrationMissing v{doc.Version}");
                doc = m.Apply(doc);
            }
            return doc;
        }
    }
}
// Тесты M0-01D: сейв v1 из S04 грузится; запись атомарна (tmp → replace, .bak); экипировка переживает перезапуск.
```

## 8. Реестр отклонений
```json
{ "format": "cubegame.deviations/1",
  "entries": [
    { "id": "movement.jump_speed", "value": 7.0, "unit": "block/s", "status": "PLACEHOLDER_NOT_ALPHA",
      "source": "OQ-23 open; PlayerMotor.JumpSpeed", "used_by": ["Features.Hero.Movement"], "test": "DEV-01" },
    { "id": "render.voxels_per_block", "value": null, "unit": "voxel/block", "status": "PLACEHOLDER_NOT_ALPHA",
      "source": "R-SCALE; research by drawModel placement", "used_by": ["Presentation.EntityView"], "test": "DEV-02" },
    { "id": "anim.pose_return_ms", "value": 150, "unit": "ms", "status": "PLACEHOLDER_NOT_ALPHA",
      "source": "UNITY_TRANSITION (B C-30)", "used_by": ["AlphaModels.Controller"], "test": "DEV-03" }
  ] }
```
Значения `null`/150 — места для решения, не утверждённые числа.
