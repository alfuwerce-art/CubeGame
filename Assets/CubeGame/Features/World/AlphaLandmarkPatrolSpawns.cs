using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>Landmark creature generation at 0x5DC6E0 for the lake and valley compositions verified in Alpha.</summary>
    public static class AlphaLandmarkPatrolSpawns
    {
        private const int HostileGroupFlag = 0x1000;
        private const int MasterFlag = 0x200;
        private const double FixedScale = 65536.0;

        public const string BehaviourKey = "alpha.landmark.creature";

        private static readonly int[] LakeKinds = { 0x56, 0x6A };
        private static readonly int[] ValleyGroup = { 0x34 };
        private static readonly int[] ValleyPatrol = { 0x3E, 0x3C, 0x34 };
        private static readonly int[] Compositions = { 0, 1, 2, 3, 4, 5, 1, 7, 8, 6, 10 };
        private static readonly int[] AppearanceDraws = { 0x43F80D, 0x43F826, 0x43F83A, 0x43F85E, 0x43F86B };
        private static readonly int[] ItemSeedDraws = { 0x5C81A8, 0x5C81B0, 0x5C81B8, 0x5C81C0, 0x5C81C8, 0x5C81D0 };

        /// <summary>Consumes the zone CRT left by point selection. The caller owns registration of the returned spawns.</summary>
        public static List<SpawnDefinition> Generate(ZoneContentContext context, IReadOnlyList<WorldPosition> points)
        {
            var spawns = new List<SpawnDefinition>();
            if (points.Count == 0)
                return spawns;

            LandmarkColumn landmark = context.Landmark;
            int composition = SelectComposition(landmark);
            (int[] groupKinds, int[] patrolKinds) = composition switch
            {
                7 => (ValleyGroup, ValleyPatrol),
                9 => (LakeKinds, LakeKinds),
                _ => throw new NotSupportedException($"Landmark patrol composition {composition} still needs its Alpha port."),
            };
            int masterIndex = AlphaLandmarkPatrolPlacement.SelectMasterPoint(context.Zone, landmark, points);
            var region = new RegionKey(context.Zone.Zx / WorldLayout.ZonesPerRegion, context.Zone.Zz / WorldLayout.ZonesPerRegion);
            int startIndex = context.Spawns.Count;
            AlphaCrtRandom random = context.Random;
            int from = random.SiteFrom, to = random.SiteTo;
            var neighbours = new List<WorldPosition>();
            try
            {
                for (int i = 0; i < points.Count; i++)
                {
                    bool master = i == masterIndex;
                    if (!master && (Draw(random, 0x5DDD4D) & 1) != 0)
                        continue;

                    WorldPosition point = points[i];
                    if (master || (Draw(random, 0x5DDD97) & 1) != 0)
                        AddGroup(spawns, startIndex, region, landmark, point, master, groupKinds, random);
                    else
                        AddPatrol(spawns, startIndex, region, context.Zone, landmark, point, points, neighbours, patrolKinds, random);
                }
                return spawns;
            }
            finally
            {
                random.At(from, to);
            }
        }

        private static int SelectComposition(LandmarkColumn landmark)
        {
            if (landmark.Type == LandmarkType.Lake)
                return 9;

            int count = landmark.Type == LandmarkType.Mountain || landmark.Type == LandmarkType.Peak ? 9 :
                landmark.Type == LandmarkType.Forest ? 11 : 10;
            return Compositions[unchecked((uint)landmark.Id) % (uint)count];
        }

        private static void AddGroup(List<SpawnDefinition> spawns, int startIndex, RegionKey region, LandmarkColumn landmark,
            WorldPosition centre, bool masterPoint, int[] kinds, AlphaCrtRandom random)
        {
            float startAngle = Single((float)(Draw(random, 0x5DE909) * (2 * Math.PI) / AlphaCrtRandom.RandMax));
            int size = Draw(random, 0x5DE92F) % 3 + 1;
            for (int i = 0; i < size; i++)
            {
                float angle = Single((float)(i * 2 * Math.PI / size + startAngle));
                double dx = (long)Single((float)Math.Cos(angle) * 3f * (float)FixedScale) / FixedScale;
                double dz = (long)Single((float)Math.Sin(angle) * 3f * (float)FixedScale) / FixedScale;
                var position = new WorldPosition(centre.X + dx, centre.Y, centre.Z + dz);
                int kind = kinds[Draw(random, 0x5DEAAA) % kinds.Length];
                SpawnDefinition spawn = Create(region, startIndex + spawns.Count, landmark, kind, position);
                spawn.Yaw = Single((float)(Single(angle * 180f) / Math.PI + 90.0));
                spawn.Route = new[] { position };
                if (masterPoint && i == 0)
                    spawn.AlphaFlags |= MasterFlag;
                InitializeGroupMember(spawn, kind, random);
                spawns.Add(spawn);
            }
        }

        private static void AddPatrol(List<SpawnDefinition> spawns, int startIndex, RegionKey region, AlphaZone zone,
            LandmarkColumn landmark, WorldPosition start, IReadOnlyList<WorldPosition> points, List<WorldPosition> neighbours,
            int[] kinds, AlphaCrtRandom random)
        {
            // The two verified compositions have one kind in each leader/companion pool, even though Alpha still rolls both.
            int kind = kinds[Draw(random, 0x5DED67) % kinds.Length];
            int yaw = Draw(random, 0x5DEDF3) % 360;
            Draw(random, 0x5DEE5B);
            neighbours.Clear();
            foreach (WorldPosition point in points)
            {
                float dx = (float)(point.X - start.X), dz = (float)(point.Z - start.Z);
                float squaredDistance = dx * dx + dz * dz;
                if (squaredDistance > 25f && squaredDistance < 16384f)
                    neighbours.Add(point);
            }

            var route = new List<WorldPosition> { start };
            if (neighbours.Count > 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    WorldPosition next = neighbours[Draw(random, 0x5DEFE9) % neighbours.Count];
                    int x = (int)next.X, z = (int)next.Z;
                    VoxelColumn column = zone.Column(x, z);
                    route.Add(new WorldPosition(next.X, AlphaLandmarkPatrolPlacement.FindSurface(column), next.Z));
                }
            }

            SpawnDefinition leader = Create(region, startIndex + spawns.Count, landmark, kind, start);
            leader.Yaw = yaw;
            leader.Route = route;
            spawns.Add(leader);
            int count = Draw(random, 0x5DF198) % 3 + 1;
            for (int i = 0; i < count; i++)
            {
                Draw(random, 0x5DF236);
                SpawnDefinition follower = Create(region, startIndex + spawns.Count, landmark, kind, start);
                follower.LeaderSpawnIndex = leader.SpawnIndex;
                spawns.Add(follower);
            }
        }

        private static SpawnDefinition Create(RegionKey region, int index, LandmarkColumn landmark, int kind,
            WorldPosition position) => new SpawnDefinition
        {
            Region = region,
            SpawnIndex = index,
            Kind = new KindId(kind),
            Position = position,
            Faction = 1,
            Level = landmark.Level,
            Quality = landmark.LevelClass & 0xFF,
            AlphaFlags = HostileGroupFlag,
            BehaviourKey = BehaviourKey,
            Provenance = "PARTIAL: 0x5DC6E0 landmark creature, runtime and full appearance pending",
        };

        private static void InitializeGroupMember(SpawnDefinition spawn, int kind, AlphaCrtRandom random)
        {
            // 0x43F7C0 with a null appearance seed consumes five draws. The species sets additional spawn flags.
            foreach (int site in AppearanceDraws)
                Draw(random, site);

            // 0x5C7730 initializes these three stationary species. The 0x10/0x08 flags bypass humanoid gear selection.
            switch (kind)
            {
                case 0x34:
                    spawn.AlphaFlags |= 0x20;
                    AssignArmedClass(spawn, random);
                    break;
                case 0x56:
                    spawn.AlphaFlags |= 0x10;
                    spawn.ClassId = 2;
                    spawn.SpecializationId = 1;
                    break;
                case 0x6A:
                    spawn.AlphaFlags |= 0x28;
                    break;
            }
            if ((spawn.AlphaFlags & MasterFlag) != 0)
            {
                spawn.PowerScale *= 3f;
                Draw(random, 0x5C8144); // Active skill from a five-entry pool.
                Draw(random, 0x5C8164); // Attack variation.
            }
            foreach (int site in ItemSeedDraws)
                Draw(random, site);
        }

        private static void AssignArmedClass(SpawnDefinition spawn, AlphaCrtRandom random)
        {
            int classId = Draw(random, 0x5C7A0A) & 3;
            spawn.ClassId = classId;
            int main, off = -1;
            switch (classId)
            {
                case 0:
                    spawn.SpecializationId = Draw(random, 0x5C7A3C) & 1;
                    int style = Draw(random, 0x5C7A4D) % 3;
                    if (style == 0)
                    {
                        main = 15 + Draw(random, 0x5C7ABD) % 3;
                    }
                    else if (style == 1)
                    {
                        main = Draw(random, 0x5C7A8F) % 3;
                        off = Draw(random, 0x5C7AAC) % 3;
                    }
                    else
                    {
                        main = Draw(random, 0x5C7A68) % 3;
                        off = 13;
                    }
                    break;
                case 1:
                    spawn.SpecializationId = Draw(random, 0x5C7AE5) & 1;
                    main = 6 + Draw(random, 0x5C7AF9) % 3;
                    break;
                case 2:
                    spawn.SpecializationId = Draw(random, 0x5C7B18) & 1;
                    main = 10 + Draw(random, 0x5C7B29) % 3;
                    if (main == 12)
                        off = 12;
                    break;
                default:
                    spawn.SpecializationId = Draw(random, 0x5C7B88) & 1;
                    main = 3 + Draw(random, 0x5C7B9C) % 3;
                    if (main != 5)
                        off = main;
                    break;
            }

            var mainHand = new RawAlphaItemId(3, main);
            spawn.MainHandWeapon = mainHand;
            spawn.Equipment = off < 0 ? new[] { mainHand } : new[] { mainHand, new RawAlphaItemId(3, off) };
        }

        private static float Single(float value) => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(value));

        private static int Draw(AlphaCrtRandom random, int site)
        {
            random.At(site, site);
            return random.Next();
        }
    }
}
