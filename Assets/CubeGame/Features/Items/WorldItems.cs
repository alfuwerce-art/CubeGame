using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;

namespace CubeGame.Features.Items
{
    /// <summary>
    /// Items lying in the world (loot). Pickup is automatic when the hero comes within <see cref="PickupRadius"/>
    /// and the bags have room (CubeGame rule, PLACEHOLDER_NOT_ALPHA: Alpha's pickup interaction is not restored).
    /// Ground items are runtime-only and not saved (PLACEHOLDER_NOT_ALPHA).
    /// </summary>
    public sealed class WorldItems
    {
        /// <summary>Pickup distance in blocks (PLACEHOLDER_NOT_ALPHA).</summary>
        public const double PickupRadius = 1.6;

        private readonly List<Ground> _items = new List<Ground>();
        private readonly GameEventBus _events;
        private ulong _serial = 0x10000;

        public WorldItems(GameEventBus events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public int Count => _items.Count;

        public IEnumerable<(StableEntityId Id, InventoryEntry Entry, WorldPosition Position)> All
        {
            get
            {
                foreach (Ground g in _items)
                {
                    yield return (g.Id, g.Entry, g.Position);
                }
            }
        }

        public StableEntityId Drop(InventoryEntry entry, WorldPosition position, long nowMs)
        {
            var ground = new Ground { Id = StableEntityId.ForRuntime(++_serial), Entry = entry, Position = position };
            _items.Add(ground);
            _events.Publish(new WorldItemDropped
            {
                TimeMs = nowMs, Item = ground.Id, Raw = entry.Raw, Count = entry.Count, Position = position,
                Material = entry.Core != null ? (int)entry.Core.Modifier : 0,
                Rarity = entry.Core?.Rarity ?? 0,
                Seed = entry.Core?.Seed ?? 0,
            });
            return ground.Id;
        }

        /// <summary>Moves every item within reach into the hero's bags; items that do not fit stay on the ground.</summary>
        public int PickupNear(HeroState hero, long nowMs)
        {
            int picked = 0;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                Ground g = _items[i];
                double dx = g.Position.X - hero.Position.X;
                double dz = g.Position.Z - hero.Position.Z;
                double dy = g.Position.Y - hero.Position.Y;
                if (dx * dx + dz * dz > PickupRadius * PickupRadius || Math.Abs(dy) > 3.0)
                {
                    continue;
                }

                if (!hero.Inventory.TryAdd(g.Entry))
                {
                    continue;
                }

                _items.RemoveAt(i);
                picked++;
                _events.Publish(new WorldItemRemoved { TimeMs = nowMs, Item = g.Id, PickedBy = hero.Id });
            }

            return picked;
        }

        private sealed class Ground
        {
            public StableEntityId Id;
            public InventoryEntry Entry;
            public WorldPosition Position;
        }
    }
}
