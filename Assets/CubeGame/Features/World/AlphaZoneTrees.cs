using System;
using System.Numerics;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Ordinary voxel trees: the 14×14 placement pass in 0x5E4850 and the recovered branch families of 0x5DF960.
    /// </summary>
    internal static class AlphaZoneTrees
    {
        private static readonly Vector3 BarkNoiseColor = new Vector3(50f, 120f, 60f);

        internal static void Populate(ZoneContentContext context)
        {
            int startX = context.Zone.Zx * AlphaZone.Size;
            int startZ = context.Zone.Zz * AlphaZone.Size;
            int endX = startX + AlphaZone.Size;
            int endZ = startZ + AlphaZone.Size;
            AlphaCrtRandom random = context.Random;
            AlphaTerrainGenerator terrain = context.Terrain;
            int speciesSeedX = terrain.Seeds.At(0x800294);
            int speciesSeedZ = terrain.Seeds.At(0x800298);
            int densitySeedX = terrain.Seeds.At(0x8001F4);
            int densitySeedZ = terrain.Seeds.At(0x8001F8);
            for (int x = startX; x < startX + 14 * 18; x += 18)
            {
                for (int z = startZ; z < startZ + 14 * 18; z += 18)
                {
                    float forest = context.Landmark?.Type == LandmarkType.Forest
                        ? Math.Max(1f - terrain.LandmarkDistance2(context.Landmark, x + 8, z + 8), 0f)
                        : 0f;
                    forest *= forest;
                    float temperature = context.Zone.Column(x + 8, z + 8).Temperature;
                    int radius = (int)(random.Next() % 5 + temperature * 2f + 6f + forest * 4f);
                    int height = (int)((Unit(random) * 8f + forest * 6f + 8f) * (temperature * 0.5f + 1f));
                    int px = Math.Min(x + radius, endX - radius / 2);
                    int pz = Math.Min(z + radius, endZ - radius / 2);
                    float dx = px - WorldLayout.StartVoxel;
                    float dz = pz - WorldLayout.StartVoxel;
                    if (dx * dx + dz * dz < 400f) continue;

                    float speciesNoise = AlphaNoise.Sample(px * 0.001 + speciesSeedX, pz * 0.001 + speciesSeedZ);
                    int kind = (random.Next() & 1) * 5;
                    if ((random.Next() % 10 == 0) == (speciesNoise <= 0.3f)) kind = 1;
                    if (random.Next() % 10 == 0) kind = 2;
                    VoxelColumn column = context.Zone.Column(px, pz);
                    float humidity = column.Humidity;
                    if (humidity > 0.8f && temperature > 0.7f)
                    {
                        kind = 4 + (random.Next() & 1);
                        if ((random.Next() & 3) == 0) kind = 3;
                    }
                    float preference = AlphaNoise.Sample(px * 0.001 + 8473.0, pz * 0.001 + 9438.0);
                    if (humidity < 0.3f)
                    {
                        kind = humidity > 0.2f && preference > 0.6f && random.Next() % 5 != 0 ? 2 : 1;
                    }
                    else if (humidity < 0.7f && preference > 0.8f && random.Next() % 5 != 0)
                    {
                        kind = 2;
                    }

                    if (kind == 2) height += random.Next() % (height / 2);
                    if (kind == 1) radius = Math.Max(1, Math.Min(radius, height / 2));
                    if (px + radius > endX) px = endX - radius;
                    if (pz + radius > endZ) pz = endZ - radius;

                    column = context.Zone.Column(px, pz);
                    float density = Math.Clamp(AlphaNoise.Sample(px * 0.008 + densitySeedX, pz * 0.008 + densitySeedZ)
                        * 1.2f, 0f, 1f);
                    density = Math.Min(1f, density + column.Temperature * 0.2f * column.Temperature + 0.02f);
                    if (Unit(random) > density + forest) continue;
                    int surface = column.TopSolid();
                    if (surface < 0) continue;
                    int material = Voxel.Material(column.Read(surface));
                    if (material != 4 && material != 10 && (material != 9 || surface + 1 >= 3)) continue;
                    if (column.Humidity < 0.2f) kind = 3;

                    // Branches 3 and 5 still need their geometry and RNG recovered.
                    if (kind == 0 || kind == 1 || kind == 2 || kind == 4)
                        Grow(context, px, surface + 1, pz, radius, height, kind);
                }
            }
        }

        private static void Grow(ZoneContentContext context, int x, int y, int z, int radius, int height, int kind)
        {
            AlphaCrtRandom random = context.Random;
            VoxelColumn column = context.Zone.Column(x, z);
            float humidity = column.Humidity;
            var bark = new Vector3(240f, 120f + humidity * 50f + random.Next() % 80,
                50f + humidity * 100f + random.Next() % 50);
            var tip = new Vector3(240f, 120f + humidity * 50f + random.Next() % 80,
                50f + humidity * 100f + random.Next() % 50);
            float trunk = radius * 0.25f;
            float narrow = trunk;
            if (kind == 1)
            {
                trunk = Math.Max(trunk * 0.5f, 0.3f);
                narrow = 0.3f;
                height *= 2;
            }
            else if (kind == 2)
            {
                trunk = narrow = 1.5f;
                bark = tip = new Vector3(255f);
            }

            Vector3 lower;
            Vector3 upper;
            if (Unit(random) < (humidity - 0.5f) * 10f || kind == 4)
            {
                lower = new Vector3(random.Next() % 20, random.Next() % 155 + 50, random.Next() % 100);
                int offset = random.Next() % 100 + 50;
                upper = lower + new Vector3(offset, offset, 0f);
            }
            else if (column.Temperature > 0.5f)
            {
                lower = new Vector3(random.Next() % 55 + 200, random.Next() % 100, random.Next() % 50);
                int offset = random.Next() % 100 + 50;
                upper = lower + new Vector3(offset, offset, 0f);
            }
            else if ((random.Next() & 1) == 0)
            {
                lower = new Vector3(random.Next() % 55 + 200, 0f, random.Next() % 200);
                upper = lower + new Vector3(random.Next() % 100 + 100);
            }
            else
            {
                lower = new Vector3(random.Next() % 20, random.Next() % 100 + 120, random.Next() % 100);
                int offset = random.Next() % 150 + 100;
                upper = lower + new Vector3(offset, offset, 0f);
            }
            upper = Vector3.Min(upper, new Vector3(255f));
            if (kind == 2)
            {
                lower = new Vector3(150f, 255f, 0f);
                upper = new Vector3(244f, 255f, 0f);
            }
            if (humidity < 0.2f)
            {
                float snow = Math.Min(1f - (humidity - 0.1f) / 0.1f, 1f);
                Vector3 snowColor = SnowColor(x, z);
                lower = Vector3.Lerp(lower, snowColor, snow);
                upper = Vector3.Lerp(upper, snowColor, snow);
                bark = Vector3.Lerp(bark, new Vector3(120f, 120f, 80f), snow);
            }

            for (int px = (int)(x - trunk - 1f); px <= x + trunk + 1f; px++)
            {
                for (int pz = (int)(z - trunk - 1f); pz <= z + trunk + 1f; pz++)
                {
                    for (int py = y + height; py >= y - 2; py--)
                    {
                        float level = (py - y + 2f) / (height + 2f);
                        float taper = level * level;
                        float width = (1f - taper) * trunk + taper * narrow;
                        double dx = (px - x - (width <= 0.8f ? 0 : 0.5)) / width;
                        double dz = (pz - z - (width <= 0.8f ? 0 : 0.5)) / width;
                        if (dx * dx + dz * dz > 1.0 || !CanWrite(context, px, py, pz)) continue;
                        Vector3 color = kind == 2 && (random.Next() & 7) == 0 ? new Vector3(50f) :
                            Vector3.Lerp(bark, tip, level);
                        context.Write(px, py, pz, Pack(Shade(context, px, py, pz, color), 0x27));
                    }
                }
            }

            // The draw controls the optional branch ornament, which still needs its render path.
            random.Next();
            if (kind == 1)
            {
                GrowConifer(context, x, y, z, radius, height, humidity);
                return;
            }

            int branches = random.Next() % 5 + 4;
            float angle = Angle(random);
            float crownAspect = 0.6f + Unit(random) * 0.8f;
            float crownRadius = (2f + Unit(random) * 1.25f) * trunk;
            GrowCrown(context, x, y + height, z, crownRadius, crownRadius * crownAspect, lower, upper, kind == 4);
            for (int i = 0; i < branches; i++)
            {
                float level = 0.5f + Unit(random) * 0.5f;
                float direction = i * 3.1415927f * 2f / branches + angle;
                float cos = (float)Math.Cos(direction);
                float sin = (float)Math.Sin(direction);
                double px = x + 0.5 + cos * trunk * 0.5f;
                double pz = z + 0.5 + sin * trunk * 0.5f;
                double py = level * height + y;
                float step = trunk * 0.75f * 0.5f;
                Vector3 color = Vector3.Lerp(bark, tip, level);
                for (int j = 0; j < 6; j++)
                {
                    if (kind == 0 && trunk > 1.5f)
                        PaintRound(context, (int)px, (int)py, (int)pz, color, true, 0x27);
                    px += cos * step;
                    pz += sin * step;
                    py += Unit(random) * step;
                }
                float width = (2f + Unit(random) * 1.25f) * trunk;
                px = FitCrown(px, width, context.Zone.Zx * AlphaZone.Size);
                pz = FitCrown(pz, width, context.Zone.Zz * AlphaZone.Size);
                float crownHeight = crownAspect * width;
                if (i > 4) py += crownHeight * 0.5f;
                GrowCrown(context, (int)px, (int)(py + crownHeight * 0.25f), (int)pz, width, crownHeight,
                    lower, upper, kind == 4);
            }
        }

        private static void GrowConifer(ZoneContentContext context, int x, int y, int z, int radius, int height, float humidity)
        {
            AlphaCrtRandom random = context.Random;
            float angle = Angle(random);
            int blue = random.Next() % 100;
            int green = random.Next() % 50 + 100;
            var inner = new Vector3(random.Next() % 100, green, blue);
            blue = random.Next() % 100;
            green = random.Next() % 50 + 100;
            var outer = new Vector3(random.Next() % 100, green, blue);
            Vector3 snow = SnowColor(x, z);
            for (int i = 0; i < 30; i++)
            {
                angle += 0.9424778f;
                float level = i * 0.7f / 30f + 0.3f;
                float reach = (1f - level) * 2f;
                double endX = x + reach * (float)Math.Cos(angle) * radius;
                double endZ = z + reach * (float)Math.Sin(angle) * radius;
                double startY = level * height + y;
                double endY = startY - reach * radius * 0.3f;
                for (int j = height / 3; j > 0; j--)
                {
                    float t = j / (float)(height / 3);
                    int px = (int)(t * endX + (1f - t) * (double)x);
                    int pz = (int)(t * endZ + (1f - t) * (double)z);
                    double py = t * endY + (1f - t) * startY;
                    if (humidity < 0.3f) PaintRound(context, px, (int)(py + 1), pz, snow, false, 0x28);
                    PaintRound(context, px, (int)py, pz, Vector3.Lerp(inner, outer, t), false, 0x28);
                }
            }
            context.Write(x, y + height, z, Pack(inner, 0x28));
            context.Write(x, y + height + 1, z, Pack(inner, 0x28));
        }

        private static void GrowCrown(ZoneContentContext context, int x, int y, int z, float radius, float height,
            Vector3 lower, Vector3 upper, bool hanging)
        {
            int noiseX = context.Random.Next();
            int noiseZ = context.Random.Next();
            double cx = x + 0.5;
            double cy = y + 0.5;
            double cz = z + 0.5;
            for (int px = (int)(cx - radius - 1.0); px <= cx + radius + 1.0; px++)
            {
                for (int pz = (int)(cz - radius - 1.0); pz <= cz + radius + 1.0; pz++)
                {
                    double dx = (px - cx) / radius;
                    double dz = (pz - cz) / radius;
                    double horizontal = dx * dx + dz * dz;
                    if (horizontal > 1.0) continue;
                    float noise = (float)(AlphaNoise.Sample(px * 0.04 + noiseX, pz * 0.04 + noiseZ) * 0.5
                        + AlphaNoise.Sample(px * 0.1 + noiseX, pz * 0.1 + noiseZ) * 0.3f);
                    for (int py = (int)(cy + height + 1.0); py >= cy - height - 1.0; py--)
                    {
                        double h = (py - cy) / height * 1.8f + noise;
                        if (h * h + horizontal > 1.0 || !CanWrite(context, px, py, pz)) continue;
                        WriteLeaf(py, h);
                        if (!hanging || context.Random.Next() % 20 != 0) continue;
                        int length = context.Random.Next() % 10;
                        if ((context.Random.Next() & 3) == 0) length *= 2;
                        for (int below = py - 1; below >= py - length; below--)
                        {
                            if (CanWrite(context, px, below, pz))
                                WriteLeaf(below, (below - cy) / height * 1.8f + noise);
                        }
                    }

                    void WriteLeaf(int py, double h)
                    {
                        float t = Math.Clamp((float)((h + 1.0) * 0.5), 0f, 1f);
                        context.Write(px, py, pz, Pack(Vector3.Lerp(lower, upper, t), 0x28));
                    }
                }
            }
        }

        private static void PaintRound(ZoneContentContext context, int x, int y, int z, Vector3 color, bool sphere, int flags)
        {
            // Both original helpers are called with radius 2 and the centered flag.
            for (int px = x - 1; px <= x + 2; px++)
            {
                for (int pz = z - 1; pz <= z + 2; pz++)
                {
                    double dx = px - x - 0.5;
                    double dz = pz - z - 0.5;
                    for (int py = sphere ? y - 1 : y; py <= (sphere ? y + 2 : y); py++)
                    {
                        double dy = sphere ? py - y - 0.5 : 0;
                        if (dx * dx + dy * dy + dz * dz <= 4.0 && CanWrite(context, px, py, pz))
                            context.Write(px, py, pz, Pack(sphere ? Shade(context, px, py, pz, color) : color, flags));
                    }
                }
            }
        }

        private static bool CanWrite(ZoneContentContext context, int x, int y, int z) =>
            context.Contains(x, z) && (Voxel.Flags(context.Read(x, y, z)) & 0x40) == 0;

        private static Vector3 Shade(ZoneContentContext context, int x, int y, int z, Vector3 color)
        {
            float n = AlphaNoise.Sample(z * 0.2 + 534.0, y * 0.2 + 13.0);
            float m = AlphaNoise.Sample(x * 0.05 + 4343.0, y * 0.1 + 84734.0);
            float t = Math.Clamp(((n * 0.1f + m) * 0.7f + 0.2f) * context.Zone.Column(x, z).Temperature, 0f, 1f);
            return Vector3.Lerp(color, BarkNoiseColor, t);
        }

        private static Vector3 SnowColor(int x, int z)
        {
            float t = (AlphaNoise.Sample(x * 0.04, z * 0.04) + 1f) * 0.5f;
            return new Vector3(100f + t * 90f, 180f + t * 40f, 255f);
        }

        private static double FitCrown(double value, float radius, int zoneStart)
        {
            if (value - radius - 1.0 < zoneStart) return zoneStart + radius + 1f;
            if (value + radius + 1.0 >= zoneStart + AlphaZone.Size) return zoneStart + AlphaZone.Size - radius - 2f;
            return value;
        }

        private static uint Pack(Vector3 color, int flags) => Voxel.Pack((int)color.X, (int)color.Y, (int)color.Z, flags);
        private static float Unit(AlphaCrtRandom random) => random.Next() / (float)AlphaCrtRandom.RandMax;
        private static float Angle(AlphaCrtRandom random) => random.Next() * 6.2831855f / AlphaCrtRandom.RandMax;
    }
}
