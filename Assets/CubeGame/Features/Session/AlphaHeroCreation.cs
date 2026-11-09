using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Session
{
    /// <summary>One item of a class start kit: a Core item (equipped or into the bags) or a raw Alpha item.</summary>
    public sealed class StarterKitItem
    {
        public ItemInstance Core { get; set; }

        public RawAlphaItemId Raw { get; set; }

        public int Count { get; set; } = 1;

        /// <summary>Alpha equipment slot the kit writes directly; null = bags (or no CubeGame slot yet, C5).</summary>
        public EquipmentSlot? Equip { get; set; }

        /// <summary>Alpha material byte +0x0D of the kit item (may differ from Core.Modifier, see Weapon()).</summary>
        public int AlphaMaterial { get; set; }

        /// <summary>Alpha slot offset of the creature, for the record (0xAA8 right weapon, 0x990 left, 0xBC0/0xCD8 rings, 0xDF0 light).</summary>
        public int AlphaSlotOffset { get; set; }
    }

    /// <summary>
    /// Hero editor rules of Alpha 2013 (C1). All numbers read from the bytes of <c>Cube.exe</c>
    /// (evidence: docs/research/alpha2013/C1_ALPHA_HERO_CREATION.md); status DIRECT.
    /// </summary>
    public static class AlphaHeroCreation
    {
        /// <summary>Race names of the editor, index = widget field +0x18C (drawCharacterAppearancePanel 0x428E40).</summary>
        public static readonly string[] Races = { "Human", "Elf", "Dwarf", "Orc", "Goblin", "Lizard", "Undead", "Frogman" };

        /// <summary>Species of race + 0 (male) / + 1 (female): CharCreation_refresh_model 0x42C080 switch on +0x18C.</summary>
        private static readonly int[] RaceBaseKind = { 0x02, 0x00, 0x09, 0x0B, 0x04, 0x07, 0x0F, 0x0D };

        /// <summary>Default hair colour bytes (CharCreation_reset_defaults 0x42BD90: FF C8 32).</summary>
        public static readonly (byte R, byte G, byte B) DefaultHairColor = (0xFF, 0xC8, 0x32);

        /// <summary>
        /// Name length: the creature name field is 16 bytes (+0x1168; copy loop 0x60E62D refuses length ≥ 0x10),
        /// so a name keeps at most 15 characters.
        /// </summary>
        public const int MaxNameLength = 15;

        public const int RaceCount = 8;

        public const int PaletteColumns = 16;

        public const int PaletteRows = 7;

        /// <summary>Head and hair model id ranges (inclusive) per species: switch table 0x42C41C (default = species 2).</summary>
        private static readonly Dictionary<int, (int HeadFirst, int HeadLast, int HairFirst, int HairLast)> Models =
            new Dictionary<int, (int, int, int, int)>
            {
                { 0x00, (0x4D4, 0x4D7, 0x500, 0x509) },
                { 0x01, (0x4D8, 0x4DD, 0x50A, 0x513) },
                { 0x02, (0x4DE, 0x4E3, 0x4E4, 0x4F2) },
                { 0x03, (0x4F3, 0x4F8, 0x4F9, 0x4FF) },
                { 0x04, (0x04B, 0x04F, 0x050, 0x055) },
                { 0x05, (0x056, 0x05A, 0x05B, 0x060) },
                { 0x07, (0x062, 0x063, 0x064, 0x069) },
                { 0x08, (0x06A, 0x06E, 0x064, 0x069) },
                { 0x09, (0x11A, 0x11E, 0x11F, 0x121) },
                { 0x0A, (0x122, 0x126, 0x127, 0x12B) },
                { 0x0B, (0x514, 0x518, 0x51E, 0x527) },
                { 0x0C, (0x519, 0x51D, 0x528, 0x52B) },
                { 0x0D, (0x52C, 0x530, 0x531, 0x535) },
                { 0x0E, (0x536, 0x539, 0x53A, 0x53D) },
                { 0x0F, (0x12F, 0x134, 0x135, 0x13A) },
                { 0x10, (0x13B, 0x140, 0x141, 0x146) },
            };

        /// <summary>Hue columns of the hair palette (drawCharacterAppearancePanel, 16 setVec4 pushes).</summary>
        private static readonly float[,] Hues =
        {
            { 1f, 0f, 0f }, { 1f, 0.25f, 0f }, { 1f, 0.5f, 0f }, { 1f, 1f, 0f },
            { 0.5f, 1f, 0f }, { 0f, 1f, 0f }, { 0f, 1f, 0.5f }, { 0f, 1f, 1f },
            { 0f, 0.5f, 1f }, { 0f, 0.25f, 1f }, { 0f, 0f, 1f }, { 0.25f, 0f, 1f },
            { 0.5f, 0f, 1f }, { 0.75f, 0f, 1f }, { 1f, 0f, 1f }, { 0.5f, 0.5f, 0.5f },
        };

        public static HeroAppearance Default => new HeroAppearance(0, 0, 0, 0, DefaultHairColor.R, DefaultHairColor.G, DefaultHairColor.B);

        /// <summary>Alpha species of the hero (creature +0x64).</summary>
        public static KindId KindOf(int race, int gender)
        {
            if (race < 0 || race >= RaceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(race), race, "race must be 0..7");
            }

            return new KindId(RaceBaseKind[race] + (gender != 0 ? 1 : 0));
        }

        public static KindId KindOf(HeroAppearance appearance) => KindOf(appearance.Race, appearance.Gender);

        public static (int HeadFirst, int HeadLast, int HairFirst, int HairLast) ModelRanges(KindId kind)
        {
            return Models.TryGetValue(kind.Value, out var r) ? r : Models[0x02];
        }

        public static int FaceCount(KindId kind)
        {
            var r = ModelRanges(kind);
            return r.HeadLast - r.HeadFirst + 1;
        }

        public static int HaircutCount(KindId kind)
        {
            var r = ModelRanges(kind);
            return r.HairLast - r.HairFirst + 1;
        }

        public static int HeadModel(HeroAppearance a) => ModelRanges(KindOf(a)).HeadFirst + a.Face;

        public static int HairModel(HeroAppearance a) => ModelRanges(KindOf(a)).HairFirst + a.Haircut;

        /// <summary>
        /// Index wrap of CharCreation_refresh_model: below 0 → last, at or above the count → 0. Applied every time the
        /// race/gender/face/haircut changes, so a face index of another species is brought back into range.
        /// </summary>
        public static int Wrap(int index, int count)
        {
            if (index < 0)
            {
                return count - 1;
            }

            return index >= count ? 0 : index;
        }

        /// <summary>Race/gender clamped, face/haircut wrapped into the ranges of the species.</summary>
        public static HeroAppearance Normalize(HeroAppearance a)
        {
            int race = a.Race < 0 ? RaceCount - 1 : a.Race >= RaceCount ? 0 : a.Race;
            int gender = a.Gender != 0 ? 1 : 0;
            KindId kind = KindOf(race, gender);
            return new HeroAppearance(race, gender, Wrap(a.Face, FaceCount(kind)), Wrap(a.Haircut, HaircutCount(kind)), a.HairR, a.HairG, a.HairB);
        }

        /// <summary>
        /// Hair palette cell (16 hue columns × 7 rows): rows 0–2 darken (×0.2/0.45/0.7), row 3 is the hue, rows 4–6
        /// mix toward white (+(1 − c)·0.25/0.5/0.75); bytes = (int)(c · 255) &amp; 0xFF.
        /// </summary>
        public static (byte R, byte G, byte B) PaletteColor(int column, int row)
        {
            if (column < 0 || column >= PaletteColumns || row < 0 || row >= PaletteRows)
            {
                throw new ArgumentOutOfRangeException(nameof(column), $"palette cell ({column},{row}) outside 16×7");
            }

            float r = Hues[column, 0];
            float g = Hues[column, 1];
            float b = Hues[column, 2];
            if (row < 3)
            {
                float k = row * 0.25f + 0.2f;
                r *= k;
                g *= k;
                b *= k;
            }
            else if (row > 3)
            {
                float k = (row - 3f) * 0.25f;
                r += (1f - r) * k;
                g += (1f - g) * k;
                b += (1f - b) * k;
            }

            return ((byte)((int)(r * 255f) & 0xFF), (byte)((int)(g * 255f) & 0xFF), (byte)((int)(b * 255f) & 0xFF));
        }

        /// <summary>Name as Alpha keeps it: trimmed, at most <see cref="MaxNameLength"/> characters.</summary>
        public static string NormalizeName(string name)
        {
            string trimmed = string.IsNullOrWhiteSpace(name) ? "Hero" : name.Trim();
            return trimmed.Length > MaxNameLength ? trimmed.Substring(0, MaxNameLength) : trimmed;
        }

        /// <summary>
        /// Class start kit, GameController_start_game_mode 0x4772B0 (jump table 0x4777D8 on +0x140 − 1). Item bytes:
        /// type, subtype, material +0x0D, level +0x10 = 1 (item init 0x42F3E0), rarity 0. Rings, lamp and consumables
        /// are given to every class.
        /// </summary>
        public static IReadOnlyList<StarterKitItem> StarterKit(CharacterClass heroClass)
        {
            var kit = new List<StarterKitItem>();
            switch (heroClass)
            {
                case CharacterClass.Warrior:   // 0x477435
                    kit.Add(Weapon(WeaponSubtype.Greatmace, 2, EquipmentSlot.MainHand, 0xAA8));
                    kit.Add(Weapon(WeaponSubtype.Sword, 1));
                    kit.Add(Weapon(WeaponSubtype.Sword, 1));
                    kit.Add(Weapon(WeaponSubtype.Shield, 1));
                    break;
                case CharacterClass.Ranger:    // 0x4775EF
                    kit.Add(Weapon(WeaponSubtype.Bow, 2, EquipmentSlot.MainHand, 0xAA8));
                    kit.Add(Weapon(WeaponSubtype.Crossbow, 2));
                    kit.Add(Weapon(WeaponSubtype.Wand, 2));
                    break;
                case CharacterClass.Mage:      // 0x47757B
                    kit.Add(Weapon(WeaponSubtype.Staff, 2, EquipmentSlot.MainHand, 0xAA8));
                    kit.Add(Weapon(WeaponSubtype.Bracelet, 0x0B));
                    kit.Add(Weapon(WeaponSubtype.Bracelet, 0x0B));
                    kit.Add(Weapon(WeaponSubtype.Wand, 2));
                    break;
                case CharacterClass.Rogue:     // 0x4774BB
                    kit.Add(Weapon(WeaponSubtype.Dagger, 1, EquipmentSlot.MainHand, 0xAA8));
                    kit.Add(Weapon(WeaponSubtype.Dagger, 1, EquipmentSlot.OffHand, 0x990));
                    kit.Add(Weapon(WeaponSubtype.Fist, 1));
                    kit.Add(Weapon(WeaponSubtype.Fist, 1));
                    kit.Add(Weapon(WeaponSubtype.Longsword, 1));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(heroClass), heroClass, "unknown class");
            }

            // 0x47768E…0x47774D: rings in both ring slots, five (1,1) consumables, a lamp in the light slot.
            kit.Add(new StarterKitItem { Core = new ItemInstance(ItemType.Ring, 0, 1, 0, 0, ItemModifier.M0B), AlphaSlotOffset = 0xCD8 });
            kit.Add(new StarterKitItem { Core = new ItemInstance(ItemType.Ring, 0, 1, 0, 0, ItemModifier.M0C), AlphaSlotOffset = 0xBC0 });
            kit.Add(new StarterKitItem { Raw = new RawAlphaItemId(1, 1), Count = 5 });
            kit.Add(new StarterKitItem { Raw = new RawAlphaItemId(0x18, 0), Count = 1, AlphaSlotOffset = 0xDF0 });
            return kit;
        }

        private static StarterKitItem Weapon(WeaponSubtype subtype, int material, EquipmentSlot? slot = null, int alphaSlot = 0)
        {
            // Core S04 accepts modifiers 0/1/0x0B/0x0C/… only; Alpha material 2 (wood weapons of the kit) is kept in
            // AlphaMaterial and the Core item gets None until the producer allows value 2 in Core (weapon damage does
            // not read the modifier, so combat numbers are the same).
            bool supported = ItemInstance.IsSupportedModifier(material);
            return new StarterKitItem
            {
                Core = new ItemInstance(ItemType.Weapon, (byte)subtype, 1, 0, 0, supported ? (ItemModifier)material : ItemModifier.None),
                AlphaMaterial = material,
                Equip = slot,
                AlphaSlotOffset = alphaSlot,
            };
        }
    }
}
